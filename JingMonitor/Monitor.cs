using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace JingMonitor;
public record SensorRow(string Device, string Kind, string Name, float? Value, string Unit);
public class Sample
{
    public double Download, Upload, Cpu, Memory, UsedMemory, TotalMemory;
    public double? Gpu;
    public List<SensorRow> Sensors = new();
    public string Network = "等待网络连接", SensorStatus = "正在初始化传感器…";
    public DateTime Time = DateTime.Now;
}
public class CounterTracker
{
    readonly Dictionary<string,(long down,long up)> previous = new();
    public (double down,double up) Tick(IEnumerable<(string id,long down,long up)> counters)
    {
        double d=0,u=0; var alive = new HashSet<string>();
        foreach(var c in counters) { alive.Add(c.id); if(previous.TryGetValue(c.id,out var p)) { d+=Math.Max(0,c.down-p.down); u+=Math.Max(0,c.up-p.up); } previous[c.id]=(c.down,c.up); }
        foreach(var id in previous.Keys.Where(x=>!alive.Contains(x)).ToArray()) previous.Remove(id);
        return(d,u);
    }
}
public sealed class MonitorEngine
{
    public event Action<Sample>? Updated;
    public Settings Settings;
    public List<DayUsage> Days;
    readonly object historyLock = new();
    readonly CancellationTokenSource stop = new();
    Task? worker,hardwareWorker;
    readonly TimeSpan hardwareStartupDelay;
    record HardwareSnapshot(List<SensorRow> Sensors,string Status,DateTime Time);
    HardwareSnapshot hardwareSnapshot=new(new List<SensorRow>(),"正在初始化传感器…网速与内存正常采集中",DateTime.UtcNow);
    readonly CounterTracker counters = new();
    readonly Computer computer = new() { IsCpuEnabled=true, IsGpuEnabled=true, IsMemoryEnabled=true, IsMotherboardEnabled=true, IsStorageEnabled=true };
    ulong lastIdle,lastKernel,lastUser;
    int count;
    public MonitorEngine(Settings settings,TimeSpan? hardwareDelay=null) { Settings=settings; hardwareStartupDelay=hardwareDelay??TimeSpan.Zero;Days=Store.Read("traffic.json",new List<DayUsage>()); }
    public List<DayUsage> History() { lock(historyLock) return Days.Select(x=>new DayUsage{Date=x.Date,Download=x.Download,Upload=x.Upload}).ToList(); }
    public void Save() { lock(historyLock) try { Store.Write("traffic.json",Days); } catch(Exception ex){Store.Log(ex.Message);} }
    public void Start() { hardwareWorker=Task.Run(RunHardware);worker=Task.Run(Run); }
    public async Task Stop() { stop.Cancel(); if(worker!=null) await worker.ConfigureAwait(false);if(hardwareWorker!=null)await Task.WhenAny(hardwareWorker,Task.Delay(2000)).ConfigureAwait(false);Save(); }
    async Task Run()
    {
        var watch=Stopwatch.StartNew(); double previousTime=watch.Elapsed.TotalSeconds;
        Cpu();
        try {
            while(!stop.IsCancellationRequested) {
                try {
                    double now=watch.Elapsed.TotalSeconds, elapsed=Math.Max(.01,now-previousTime); previousTime=now;
                    var hardware=Volatile.Read(ref hardwareSnapshot);
                    var sample=new Sample { Cpu=Cpu(), SensorStatus=hardware.Status,Sensors=hardware.Sensors };
                    if(hardware.Sensors.Count>0&&DateTime.UtcNow-hardware.Time>TimeSpan.FromSeconds(10))sample.SensorStatus="硬件更新较慢 · 网速和内存仍在独立采集";
                    var mem=new MemoryStatus(); if(GlobalMemoryStatusEx(mem)) { sample.Memory=mem.Load; sample.TotalMemory=mem.TotalPhys; sample.UsedMemory=mem.TotalPhys-mem.AvailPhys; }
                    var nics=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback&&n.NetworkInterfaceType!=NetworkInterfaceType.Tunnel&&(Settings.Adapter==""||n.Id==Settings.Adapter)).ToArray();
                    var readings=new List<(string,long,long)>();
                    foreach(var nic in nics) { try { var s=nic.GetIPStatistics(); readings.Add((nic.Id,s.BytesReceived,s.BytesSent)); } catch(NetworkInformationException){} }
                    var delta=counters.Tick(readings); sample.Download=delta.down/elapsed; sample.Upload=delta.up/elapsed;
                    sample.Network=nics.Length==0?"未连接 / 所选网卡不可用":Settings.Adapter==""?"自动汇总 · "+nics.Length+" 个接口":nics[0].Name;
                    lock(historyLock) { string day=DateTime.Now.ToString("yyyy-MM-dd"); var usage=Days.FirstOrDefault(d=>d.Date==day); if(usage==null){usage=new DayUsage { Date=day }; Days.Add(usage);} usage.Download+=delta.down; usage.Upload+=delta.up; }
                    var gpu=sample.Sensors.Where(s=>s.Kind.StartsWith("Gpu")&&s.Unit=="%"&&s.Value.HasValue&&(s.Name.Contains("Core",StringComparison.OrdinalIgnoreCase)||s.Name.Contains("D3D 3D"))).Select(s=>(double)s.Value!.Value).ToArray();
                    sample.Gpu=gpu.Length>0?gpu.Max():null;
                    Updated?.Invoke(sample);
                    if(++count%15==0) Save();
                } catch(Exception ex) { Store.Log(ex.ToString()); }
                await Task.Delay(1000,stop.Token);
            }
        } catch(OperationCanceledException){} finally { Save(); }
    }
    async Task RunHardware()
    {
        try {
            if(hardwareStartupDelay>TimeSpan.Zero)await Task.Delay(hardwareStartupDelay,stop.Token);
            if(stop.IsCancellationRequested)return;
            computer.Open();
            while(!stop.IsCancellationRequested){
                var sensors=new List<SensorRow>();
                foreach(var hw in computer.Hardware){if(stop.IsCancellationRequested)break;try{ReadHardware(hw,sensors);}catch(Exception ex){Store.Log(hw.Name+": "+ex.Message);}}
                string status=sensors.Any(x=>x.Unit=="°C"&&x.Value.HasValue)?"传感器就绪":"温度不可用 · 检查硬件支持、管理员权限与 PawnIO 驱动";
                Volatile.Write(ref hardwareSnapshot,new(sensors,status,DateTime.UtcNow));
                await Task.Delay(1000,stop.Token);
            }
        }catch(OperationCanceledException){}catch(Exception ex){Store.Log(ex.ToString());Volatile.Write(ref hardwareSnapshot,new(new List<SensorRow>(),"硬件传感器受限："+ex.Message,DateTime.UtcNow));}
        finally{try{computer.Close();}catch(Exception ex){Store.Log(ex.Message);}}
    }
    static void ReadHardware(IHardware h,List<SensorRow> rows) { h.Update(); foreach(var s in h.Sensors) { string unit=s.SensorType switch { SensorType.Temperature=>"°C",SensorType.Load=>"%",SensorType.Fan=>"RPM",SensorType.Power=>"W",SensorType.Clock=>"MHz",SensorType.Data=>"GB",SensorType.SmallData=>"MB",_=>"" }; if(unit!="") rows.Add(new(h.Name,h.HardwareType.ToString(),s.Name,s.Value,unit)); } foreach(var sub in h.SubHardware) ReadHardware(sub,rows); }
    double Cpu() { if(!GetSystemTimes(out ulong idle,out ulong kernel,out ulong user)) return 0; var total=(kernel-lastKernel)+(user-lastUser); var idleDelta=idle-lastIdle; lastIdle=idle;lastKernel=kernel;lastUser=user; return total==0?0:Math.Clamp(100.0*(total-Math.Min(total,idleDelta))/total,0,100); }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In,Out] MemoryStatus status);
    [StructLayout(LayoutKind.Sequential)] sealed class MemoryStatus { public uint Length=(uint)Marshal.SizeOf<MemoryStatus>(); public uint Load; public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended; }
}
