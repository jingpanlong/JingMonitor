using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace JingMonitor;
public static class Theme
{
    public static bool IsLight { get; private set; }
    static readonly Dictionary<string,string> Light=new(StringComparer.OrdinalIgnoreCase){
        ["#0C1220"]="#F3F6FB",["#0C1524"]="#F3F6FB",["#111A2A"]="#EAF0F7",["#0F1726"]="#FFFFFF",["#141E2F"]="#FFFFFF",["#101827"]="#F6F8FC",["#182539"]="#FFFFFF",["#172335"]="#FFFFFF",
        ["#222D40"]="#E7EDF5",["#253249"]="#DAE3EF",["#2A3549"]="#CED9E7",["#29374C"]="#CAD7E7",["#35465E"]="#C3D2E2",["#344058"]="#B9CADC",["#2A374D"]="#DEE7F1",["#26364D"]="#E7EDF5",
        ["#E8EDF5"]="#1A2B41",["#DCE5F3"]="#253C54",["#D9E1EF"]="#253C54",["#EDF5FF"]="#24374D",["#203B3C"]="#D4F1E9",["#152C2A"]="#DDF5ED",["#2B4455"]="#D7EEE9",
        ["#5CE4C0"]="#0D816B",["#AB9BFF"]="#6A54C6",["#8FA1BA"]="#526C86",["#8193AC"]="#526B85",["#7187A3"]="#617790",["#71839C"]="#5A718B",["#7C8FA8"]="#5D728C",["#7588A2"]="#596E86",["#526781"]="#6A8097",["#5F738D"]="#627A94",["#74879F"]="#657B93",["#8D9EB5"]="#536A85",["#8EA1B9"]="#566D88",["#8EA4BB"]="#58738D",["#6E829F"]="#647C96",["#7F93AB"]="#596F89",["#9CB4CC"]="#526D88",["#A3B2C7"]="#526980"
    };
    public static string Resolve(string c)
    {
        if(!IsLight)return c;if(Light.TryGetValue(c,out var v))return v;
        if(c.Equals("Transparent",StringComparison.OrdinalIgnoreCase))return c;
        try{var col=(Color)ColorConverter.ConvertFromString(c);double l=(col.R*.2126+col.G*.7152+col.B*.0722)/255;
            if(l>.45)return "#516C87";if(l<.20)return "#DCE5F0";
        }catch{}return c;
    }
    public static SolidColorBrush Brush(string c)=>(SolidColorBrush)new BrushConverter().ConvertFromString(Resolve(c))!;
    public static void Apply(string theme)
    {
        IsLight=theme=="light";
        foreach(string c in Keys)Application.Current.Resources["Brush_"+c[1..]]=Brush(c);
    }
    // Every literal used by the shell resources is registered before a window is shown.
    public static string[] Keys=ThemeResources.Colors;
}
