using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace JingMonitor;
public sealed class Widget : Window
{
    readonly Settings settings;
    readonly MonitorFace face=new();
    IntPtr handle;
    bool dragging;
    bool placing;
    bool collapsed;
    System.Drawing.Rectangle expanded;
    string loadedImage="";
    DateTime lastInside=DateTime.UtcNow;
    readonly System.Windows.Threading.DispatcherTimer edgeTimer=new(){Interval=TimeSpan.FromMilliseconds(120)};
    public bool IsCollapsed=>collapsed;
    public ImageSource? Picture=>face.BackgroundImage;
    public event Action? OpenRequested;
    public event Action? RecoveryRequested;
    public event Action? ThemeRequested;
    public event Action? DisplaySettingsChanged;
    public Widget(Settings s)
    {
        settings=s;Title="景·速 · 桌面悬浮窗";Icon=Brand.Image(32);WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;Content=face;
        ToolTip="按住拖动到任意桌面位置 · 双击打开控制台 · 右键设置";
        MouseLeftButtonDown+=(_,e)=>{
            if(e.ClickCount==2){OpenRequested?.Invoke();return;}
            if(settings.Locked)return;
            Reveal();settings.DockSide="";dragging=true;try{DragMove();}finally{dragging=false;EnsureDisplayPosition();settings.Left=Left;settings.Top=Top;Store.Write("settings.json",settings);Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>DetectDock()));}
        };
        MouseEnter+=(_,_)=>Reveal();
        ContextMenu=BuildMenu();ContextMenuOpening+=(_,e)=>{e.Handled=true;ContextMenu=BuildMenu();MenuFactory.Open(ContextMenu,this);};
        SourceInitialized+=(_,_)=>{handle=new WindowInteropHelper(this).Handle;ApplyStyle();EnsureDisplayPosition();if(settings.AutoHideEdges&&EdgeGeometry.IsSide(settings.DockSide))DockTo(settings.DockSide);edgeTimer.Start();};
        edgeTimer.Tick+=(_,_)=>EdgeTick();Closed+=(_,_)=>edgeTimer.Stop();
        LocationChanged+=(_,_)=>{if(!placing&&!dragging&&handle!=IntPtr.Zero)Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>DetectDock()));};
        Left=double.IsFinite(s.Left)?s.Left:60;Top=double.IsFinite(s.Top)?s.Top:120;
    }
    internal ContextMenu BuildMenu()
    {
        var menu=MenuFactory.Create();MenuFactory.Item(menu,"打开控制台","\uE80F",()=>OpenRequested?.Invoke());MenuFactory.Item(menu,"浅色 / 深色主题","\uE790",()=>ThemeRequested?.Invoke());menu.Items.Add(MenuFactory.Separator());
        MenuFactory.Check(menu,"靠边自动隐藏",settings.AutoHideEdges,v=>{settings.AutoHideEdges=v;if(!v){Reveal();settings.DockSide="";}Changed();DetectDock();});
        var edges=MenuFactory.Item(menu,"立即隐藏到边缘","\uE8A7");foreach(var p in new[]{("左边缘","Left"),("上边缘","Top"),("右边缘","Right"),("下边缘","Bottom")})MenuFactory.Item(edges,p.Item1,"\uE76C",()=>DockTo(p.Item2));
        MenuFactory.Check(menu,"锁定悬浮窗位置",settings.Locked,v=>{settings.Locked=v;Changed();});MenuFactory.Check(menu,"保持置顶",settings.Topmost,v=>{settings.Topmost=v;Topmost=v;Changed();});
        MenuFactory.Check(menu,"点击穿透",settings.ClickThrough,v=>{settings.ClickThrough=v;ApplyStyle();Changed();});
        menu.Items.Add(MenuFactory.Separator());MenuFactory.Item(menu,"恢复悬浮窗到桌面","\uE777",()=>RecoveryRequested?.Invoke());return menu;
    }
    void Changed(){Store.Write("settings.json",settings);DisplaySettingsChanged?.Invoke();}
    internal bool DetectDock()
    {
        if(handle==IntPtr.Zero||!IsVisible||dragging||placing||!settings.AutoHideEdges||EdgeGeometry.IsSide(settings.DockSide)||ContextMenu?.IsOpen==true||(GetAsyncKeyState(1)&0x8000)!=0)return false;
        var r=Bounds();var a=System.Windows.Forms.Screen.FromRectangle(r).WorkingArea;
        var side=EdgeGeometry.Nearest(r,a,(int)Math.Ceiling(24*VisualTreeHelper.GetDpi(this).DpiScaleX));
        if(side=="")return false;DockTo(side,false);return true;
    }
    public void Apply(Skin skin)
    {
        Reveal();
        expanded=System.Drawing.Rectangle.Empty;
        face.Skin=skin;face.Ball=settings.Mode=="ball";face.Locked=settings.Locked;
        Width=face.Ball?168:260;Height=face.Ball?168:132;
        Opacity=Math.Clamp(settings.Opacity,.1,1);Topmost=settings.Topmost;
        Left=double.IsFinite(settings.Left)?settings.Left:60;Top=double.IsFinite(settings.Top)?settings.Top:120;
        SetBackground(settings.BackgroundImage);face.ImageOpacity=settings.BackgroundImageOpacity;face.ImageOverlay=settings.BackgroundOverlay;face.ImageFit=settings.BackgroundImageFit;
        face.InvalidateVisual();ApplyStyle();EnsureDisplayPosition();
        if(handle!=IntPtr.Zero&&settings.AutoHideEdges&&EdgeGeometry.IsSide(settings.DockSide))DockTo(settings.DockSide);
    }
    void SetBackground(string path){if(path==loadedImage)return;loadedImage=path;face.BackgroundImage=null;if(path!="")try{face.BackgroundImage=BackgroundImages.Load(path);}catch(Exception ex){Store.Log("Background image: "+ex.Message);}}
    public void Update(Sample sample){face.Data=sample;face.InvalidateVisual();}
    public bool IsClickThrough=>handle!=IntPtr.Zero&&(GetWindowLongPtr(handle,-20).ToInt64()&0x20)!=0;
    public bool HasDisplayText=>true;
    public bool IsFrameVisible=>face.Visibility==Visibility.Visible;
    public bool IsOnScreen
    {
        get {if(handle==IntPtr.Zero||!GetWindowRect(handle,out var r))return false;return Array.Exists(System.Windows.Forms.Screen.AllScreens,s=>{var a=s.WorkingArea;return r.Left>=a.Left&&r.Top>=a.Top&&r.Right<=a.Right&&r.Bottom<=a.Bottom;});}
    }
    public void EnsureDisplayPosition()
    {
        if(handle==IntPtr.Zero||dragging)return;
        if(settings.AutoHideEdges&&EdgeGeometry.IsSide(settings.DockSide)&&expanded.Width>0){var area=System.Windows.Forms.Screen.FromRectangle(expanded).WorkingArea;expanded=EdgeGeometry.Anchor(expanded,area,settings.DockSide);Place(collapsed?TabBounds():expanded);return;}
        if(!GetWindowRect(handle,out var r))return;
        // Physical working-area coordinates keep floating windows away from the taskbar and notification area.
        var screen=System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.Left,r.Top,Math.Max(1,r.Right-r.Left),Math.Max(1,r.Bottom-r.Top)));
        var a=screen.WorkingArea;int w=r.Right-r.Left,h=r.Bottom-r.Top;
        int x=Math.Clamp(r.Left,a.Left,Math.Max(a.Left,a.Right-w));int y=Math.Clamp(r.Top,a.Top,Math.Max(a.Top,a.Bottom-h));
        if(x!=r.Left||y!=r.Top)SetWindowPos(handle,IntPtr.Zero,x,y,w,h,0x0010|0x0004);
    }
    public void DockTo(string side,bool collapseImmediately=true)
    {
        if(handle==IntPtr.Zero||!EdgeGeometry.IsSide(side))return;Reveal();
        var r=Bounds();expanded=EdgeGeometry.Anchor(r,System.Windows.Forms.Screen.FromRectangle(r).WorkingArea,side);
        settings.AutoHideEdges=true;settings.DockSide=side;Place(expanded);settings.Left=Left;settings.Top=Top;
        Changed();if(collapseImmediately)Collapse();
    }
    public void Reveal()
    {
        lastInside=DateTime.UtcNow;if(!collapsed)return;
        collapsed=false;face.CollapsedEdge="";Opacity=Math.Clamp(settings.Opacity,.1,1);Place(expanded);ApplyStyle();face.InvalidateVisual();
    }
    public void Collapse()
    {
        if(handle==IntPtr.Zero||!settings.AutoHideEdges||!EdgeGeometry.IsSide(settings.DockSide)||expanded.Width==0||dragging)return;
        collapsed=true;face.CollapsedEdge=settings.DockSide;Opacity=Math.Max(.65,settings.Opacity);Place(TabBounds());ApplyStyle();face.InvalidateVisual();
    }
    System.Drawing.Rectangle TabBounds(){var dpi=VisualTreeHelper.GetDpi(this);return EdgeGeometry.Tab(expanded,settings.DockSide,Math.Max(6,(int)(8*dpi.DpiScaleX)),(int)(56*dpi.DpiScaleY));}
    void EdgeTick()
    {
        if(!IsVisible||!settings.AutoHideEdges||dragging||ContextMenu?.IsOpen==true)return;
        if(!EdgeGeometry.IsSide(settings.DockSide)){DetectDock();return;}
        if(!GetCursorPos(out var p))return;
        TrackPointer(p.X,p.Y,DateTime.UtcNow);
    }
    internal System.Drawing.Rectangle ScreenBounds=>Bounds();
    internal void TrackPointer(int x,int y,DateTime now)
    {
        if(collapsed){if(Bounds().Contains(x,y))Reveal();}
        else if(expanded.Contains(x,y))lastInside=now;
        else if(now-lastInside>TimeSpan.FromMilliseconds(750))Collapse();
    }
    System.Drawing.Rectangle Bounds(){GetWindowRect(handle,out var r);return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top);}
    void Place(System.Drawing.Rectangle r){if(handle==IntPtr.Zero)return;placing=true;try{var dpi=VisualTreeHelper.GetDpi(this);Width=r.Width/dpi.DpiScaleX;Height=r.Height/dpi.DpiScaleY;SetWindowPos(handle,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x0010|0x0004);}finally{placing=false;}}
    internal void MoveToForTest(System.Drawing.Rectangle r){settings.DockSide="";Reveal();expanded=System.Drawing.Rectangle.Empty;Place(r);}
    void ApplyStyle(){if(handle==IntPtr.Zero)return;long style=GetWindowLongPtr(handle,-20).ToInt64()|0x80|0x08000000;SetWindowLongPtr(handle,-20,new IntPtr(settings.ClickThrough&&!collapsed?style|0x20:style&~0x20));}
    static SolidColorBrush Brush(string c)=>(SolidColorBrush)new BrushConverter().ConvertFromString(c)!;
    [StructLayout(LayoutKind.Sequential)] struct RECT{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct POINT{public int X,Y;}
    [DllImport("user32.dll")]static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out RECT r);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr h,int i);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr h,int i,IntPtr v);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
}
