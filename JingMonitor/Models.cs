using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;

namespace JingMonitor;
public class Settings
{
    public string Mode { get; set; } = "card";
    public bool Locked { get; set; }
    public bool AutoHideEdges { get; set; } = true;
    public string Theme { get; set; } = "dark";
    public string DockSide { get; set; } = "";
    public string BackgroundImage { get; set; } = "";
    public double BackgroundImageOpacity { get; set; } = 1;
    public double BackgroundOverlay { get; set; } = .45;
    public bool BackgroundImageFit { get; set; }
    public bool NativeTaskbar { get; set; }
    public int DisplayRevision { get; set; }
    public double Opacity { get; set; } = .94;
    public bool ClickThrough { get; set; }
    public bool Topmost { get; set; } = true;
    public double Left { get; set; } = 60;
    public double Top { get; set; } = 120;
    public int TaskbarOffset { get; set; } = 200;
    public string Skin { get; set; } = "薄荷夜航";
    public string Adapter { get; set; } = "";
    public bool Alerts { get; set; } = true;
    public double MemoryLimit { get; set; } = 90;
    public double TemperatureLimit { get; set; } = 85;
    public double TrafficLimitGB { get; set; } = 50;
}
public class DayUsage { public string Date { get; set; } = ""; public double Download { get; set; } public double Upload { get; set; } }
public class Skin
{
    public string Name { get; set; } = "我的皮肤";
    public string Background { get; set; } = "#151F30";
    public string Background2 { get; set; } = "#174740";
    public string Pattern { get; set; } = "aurora";
    public string Layout { get; set; } = "split";
    public string Border { get; set; } = "#3B6F68";
    public string Foreground { get; set; } = "#EEF4FF";
    public string Accent { get; set; } = "#5CE4C0";
    public string Upload { get; set; } = "#A498FF";
    public double Radius { get; set; } = 14;
    public double FontSize { get; set; } = 13;
    public bool ShowCpu { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 40) throw new Exception("皮肤名称需为 1–40 个字符");
        foreach (string s in new[] { Background, Background2, Foreground, Accent, Upload, Border })
            if (!System.Text.RegularExpressions.Regex.IsMatch(s ?? "", "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) throw new Exception("颜色必须使用 #RRGGBB 或 #AARRGGBB");
        if (!double.IsFinite(Radius) || Radius < 0 || Radius > 50 || !double.IsFinite(FontSize) || FontSize < 10 || FontSize > 24) throw new Exception("圆角范围 0–50，字号范围 10–24");
        if(!Patterns.Contains(Pattern))throw new Exception("不支持的背景纹理");
        if(!new[]{"split","stack","terminal"}.Contains(Layout))throw new Exception("布局应为 split、stack 或 terminal");
    }
    public static readonly string[] Patterns={"aurora","stars","glacier","sunset","petals","paper","facets","mountains","waves","honeycomb","grid","clouds","linen","orbits","sonar","bloom"};
    public static List<Skin> Builtin() {
        string[] names = { "薄荷夜航", "星际紫", "冰川蓝", "日落橙", "樱花粉", "极简白", "曜石黑", "森林绿", "海盐青", "琥珀", "霓虹", "雾霾蓝", "奶油", "葡萄", "深海", "玫瑰" };
        string[] bg={"#0C252C","#171334","#DAF0FA","#462B39","#FBE4ED","#FAFBFC","#15191F","#142F29","#D7F4EE","#322419","#171329","#DCE6F0","#FFF2D9","#291C42","#081F36","#3B202D"};
        string[] end={"#205D51","#423064","#B0D5F0","#C66B4F","#F6C4D7","#E7EDF2","#303942","#345743","#9EDCD2","#775329","#34305F","#AFBED6","#F0DCB2","#603B76","#144666","#7D4352"};
        string[] fg={"#EDFDF8","#F1ECFF","#163D5A","#FFF1E5","#63354D","#283B4B","#ECF1F5","#EAF5DE","#174F52","#FFF0D0","#F4F0FF","#2C4663","#66513B","#F6EAFF","#DEEFFA","#FFE9E8"};
        string[] accent={"#7EF4D2","#C9B0FF","#146F9F","#FFE1A3","#A43F69","#245F72","#D8E8EF","#C6E9A0","#176C67","#FFCF78","#BEF55A","#365E8C","#93622D","#DCA8FF","#68DFEC","#FFD3C3"};
        string[] upload={"#9BD2FA","#E5A8D4","#385A9A","#FFD0C4","#79519B","#59699A","#A9C3CC","#B8DDD0","#39788F","#EBB69D","#F59BEA","#6B5485","#8F7350","#F4BCDD","#B3C9FF","#F5A7BF"};
        return names.Select((n,i)=>new Skin{Name=n,Background=bg[i],Background2=end[i],Foreground=fg[i],Accent=accent[i],Upload=upload[i],Border=end[i],Pattern=Patterns[i],Layout=i%3==0?"split":i%3==1?"stack":"terminal",Radius=i==5?12:i==6?8:20,FontSize=15}).ToList();
    }
}
public static class Store
{
    public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JingMonitor");
    public static JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static void Write<T>(string file, T obj) { Directory.CreateDirectory(Root); var path=Path.Combine(Root,file); File.WriteAllText(path+".tmp", JsonSerializer.Serialize(obj,Json)); File.Move(path+".tmp",path,true); }
    public static T Read<T>(string file, T fallback) { try { var p=Path.Combine(Root,file); return File.Exists(p)?JsonSerializer.Deserialize<T>(File.ReadAllText(p),Json)??fallback:fallback; } catch(Exception ex) { Log(file+": "+ex.Message); return fallback; } }
    public static void Log(string text) { try { Directory.CreateDirectory(Root); var p=Path.Combine(Root,"diagnostic.log"); if(File.Exists(p)&&new FileInfo(p).Length>2_000_000) File.Move(p,p+".old",true); File.AppendAllText(p,DateTime.Now+" "+text+Environment.NewLine); } catch { } }
    public static string Bytes(double v) { string[] u={"B","KB","MB","GB","TB"}; int i=0; while(v>=1024&&i<4){v/=1024;i++;} return v.ToString(i==0?"0":"0.0")+" "+u[i]; }
}
