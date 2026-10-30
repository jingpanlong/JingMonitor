using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JingMonitor;
public static class SelfTests
{
    public static int Run()
    {
        var lines=new List<string>();
        void Test(string name,Action action){try{action();lines.Add("PASS "+name);}catch(Exception e){lines.Add("FAIL "+name+": "+e.Message);}}
        void Assert(bool v){if(!v)throw new Exception("assertion failed");}
        Test("network baseline, deltas, reset, disconnect, reconnect",()=>{
            var tracker=new CounterTracker();Assert(tracker.Tick(new[]{("eth",100L,200L)})==(0d,0d));
            Assert(tracker.Tick(new[]{("eth",130L,250L)})==(30d,50d));
            Assert(tracker.Tick(new[]{("eth",10L,20L)})==(0d,0d));
            tracker.Tick(Array.Empty<(string,long,long)>());Assert(tracker.Tick(new[]{("eth",1000L,2000L)})==(0d,0d));
        });
        Test("network multiple adapters",()=>{var t=new CounterTracker();t.Tick(new[]{("a",10L,10L),("b",20L,20L)});Assert(t.Tick(new[]{("a",15L,12L),("b",30L,25L)})==(15d,7d));});
        Test("all skins validate and round trip",()=>{var skins=Skin.Builtin();Assert(skins.Count==16);foreach(var s in skins){s.Validate();var copy=JsonSerializer.Deserialize<Skin>(JsonSerializer.Serialize(s))!;copy.Validate();Assert(copy.Accent==s.Accent);}});
        Test("sixteen distinct background designs",()=>{var s=Skin.Builtin();Assert(s.Select(x=>x.Pattern).Distinct().Count()==16);Assert(s.Select(x=>x.Background+":"+x.Background2).Distinct().Count()==16);Assert(s.Select(x=>x.Layout).Distinct().Count()==3);});
        Test("four-edge tabs remain inside work area including negative monitor coordinates",()=>{var area=new System.Drawing.Rectangle(-1920,40,1920,1040);foreach(var side in new[]{"Left","Right","Top","Bottom"}){var full=EdgeGeometry.Anchor(new System.Drawing.Rectangle(-1800,200,260,132),area,side);var tab=EdgeGeometry.Tab(full,side,8,56);Assert(area.Contains(full));Assert(area.Contains(tab));Assert(full.Contains(tab));Assert(EdgeGeometry.Nearest(full,area,16)==side);}});
        Test("interior window does not dock accidentally",()=>{Assert(EdgeGeometry.Nearest(new System.Drawing.Rectangle(300,250,260,132),new System.Drawing.Rectangle(0,0,1920,1040),16)=="");});
        Test("edge auto hide enabled by default",()=>Assert(new Settings().AutoHideEdges));
        Test("light and dark theme resources switch",()=>{try{Theme.Apply("light");Assert(Theme.IsLight);Assert(Theme.Resolve("#0C1220")=="#F3F6FB");Assert(Theme.Resolve("#E8EDF5")=="#1A2B41");Assert(System.Windows.Application.Current.Resources["Brush_141E2F"]!=null);}finally{Theme.Apply("dark");}Assert(!Theme.IsLight);});
        Test("brand icon contains valid alpha bitmap",()=>{using var icon=Brand.Icon();Assert(icon.Width>=16);using var bmp=icon.ToBitmap();Assert(bmp.GetPixel(bmp.Width/2,bmp.Height/2).A>0);});
        Test("legacy DIY skin remains importable",()=>{var s=JsonSerializer.Deserialize<Skin>("{\"Name\":\"旧皮肤\",\"Background\":\"#112233\"}",Store.Json)!;s.Validate();Assert(s.Background=="#112233");});
        Test("invalid skin rejected",()=>{bool rejected=false;try{new Skin{Accent="https://bad"}.Validate();}catch{rejected=true;}Assert(rejected);});
        Test("non-finite skin rejected",()=>{bool rejected=false;try{new Skin{Radius=double.NaN}.Validate();}catch{rejected=true;}Assert(rejected);});
        Test("usage byte formatting",()=>{Assert(Store.Bytes(1024)=="1.0 KB");Assert(Store.Bytes(0)=="0 B");Assert(Store.Bytes(1073741824)=="1.0 GB");});
        Test("atomic persistence and history round trip",()=>{string original=Store.Root;try{Store.Root=Path.Combine(Path.GetTempPath(),"JingMonitor-test-"+Guid.NewGuid().ToString("N"));var days=new List<DayUsage>{new(){Date="2026-10-24",Download=1234,Upload=4321}};Store.Write("traffic.json",days);var r=Store.Read("traffic.json",new List<DayUsage>());Assert(r.Single().Upload==4321);Assert(!File.Exists(Path.Combine(Store.Root,"traffic.json.tmp")));File.Delete(Path.Combine(Store.Root,"traffic.json"));Directory.Delete(Store.Root);}finally{Store.Root=original;}});
        Test("slow hardware startup does not block live system samples",()=>{
            string original=Store.Root;MonitorEngine? engine=null;
            try{Store.Root=Path.Combine(Path.GetTempPath(),"JingMonitor-slow-test-"+Guid.NewGuid().ToString("N"));engine=new MonitorEngine(new Settings{Alerts=false},TimeSpan.FromSeconds(20));var ready=new System.Threading.Tasks.TaskCompletionSource<Sample>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);engine.Updated+=s=>{if(s.TotalMemory>0)ready.TrySetResult(s);};engine.Start();Assert(ready.Task.Wait(4000));Assert(ready.Task.Result.SensorStatus.Contains("初始化"));}
            finally{engine?.Stop().GetAwaiter().GetResult();if(File.Exists(Path.Combine(Store.Root,"traffic.json")))File.Delete(Path.Combine(Store.Root,"traffic.json"));if(Directory.Exists(Store.Root)&&Directory.GetFileSystemEntries(Store.Root).Length==0)Directory.Delete(Store.Root);Store.Root=original;}
        });
        File.WriteAllLines("self-test-results.txt",lines);return lines.Any(s=>s.StartsWith("FAIL"))?1:0;
    }
}
