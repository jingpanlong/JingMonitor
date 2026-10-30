using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace JingMonitor;
public static class MenuFactory
{
    public static Separator Separator()=>new(){Style=(Style)Application.Current.FindResource(typeof(Separator))};
    public static ContextMenu Create()
    {
        var menu=new ContextMenu();var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,8,8,9)};
        header.Children.Add(new BrandLogo{Width=23,Height=23});var name=new TextBlock{Text="景 · 速   /   快捷操作",FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};name.SetResourceReference(TextBlock.ForegroundProperty,"Brush_8193AC");header.Children.Add(name);
        menu.Items.Add(new MenuItem{Header=header,IsEnabled=false,Padding=new Thickness(0)});return menu;
    }
    public static MenuItem Item(ItemsControl parent,string title,string icon,Action? action=null)
    {
        var text=new TextBlock{Text=title};text.SetResourceReference(TextBlock.ForegroundProperty,"Brush_E8EDF5");
        var glyph=new TextBlock{Text=icon,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=14};glyph.SetResourceReference(TextBlock.ForegroundProperty,"Brush_5CE4C0");
        var item=new MenuItem{Header=text,Icon=glyph};if(action!=null)item.Click+=(_,e)=>{e.Handled=true;action();};parent.Items.Add(item);return item;
    }
    public static MenuItem Check(ItemsControl parent,string title,bool value,Action<bool> action)
    {
        var item=Item(parent,title,"",null);item.IsCheckable=true;item.IsChecked=value;item.Click+=(_,e)=>{e.Handled=true;action(item.IsChecked);};return item;
    }
    public static void Open(ContextMenu menu,Window owner)
    {
        SetForegroundWindow(new WindowInteropHelper(owner).Handle);menu.PlacementTarget=owner;menu.Placement=PlacementMode.MousePoint;menu.IsOpen=true;
    }
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
}
