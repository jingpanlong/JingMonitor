using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace JingMonitor;
public partial class MainWindow : Window
{
    readonly Settings settings=Store.Read("settings.json",new Settings());
    readonly List<Skin> skins=Skin.Builtin();
    readonly MonitorEngine engine;
    readonly Widget widget;
    readonly Forms.NotifyIcon tray;
    readonly System.Drawing.Icon trayLogo=Brand.Icon();
    readonly List<(double down,double up)> points=new();
    readonly Dictionary<string,DateTime> alertTimes=new();
    readonly Dictionary<string,TextBlock> fields=new();
    Sample sample=new();
    string page="overview", historyRange="7";
    TrafficChart? chart;
    StackPanel? sensorList,historyList;
    MonitorFace? imagePreview;
    bool exiting;
    const string RepoUrl="https://github.com/jingpanlong/JingMonitor";
    readonly System.Windows.Threading.DispatcherTimer displayTimer=new(){Interval=TimeSpan.FromSeconds(1)};
    string displayState="";
    public MainWindow()
    {
        InitializeComponent();
        Theme.Apply(settings.Theme);Icon=Brand.Image(32);ThemeToggle.Content=Theme.IsLight?"☾  深色":"☀  浅色";
        settings.Opacity=double.IsFinite(settings.Opacity)?Math.Clamp(settings.Opacity,.1,1):.94;
        settings.BackgroundImageOpacity=double.IsFinite(settings.BackgroundImageOpacity)?Math.Clamp(settings.BackgroundImageOpacity,0,1):1;
        settings.BackgroundOverlay=double.IsFinite(settings.BackgroundOverlay)?Math.Clamp(settings.BackgroundOverlay,0,.85):.45;
        if(!EdgeGeometry.IsSide(settings.DockSide))settings.DockSide="";
        settings.TaskbarOffset=Math.Clamp(settings.TaskbarOffset,0,1800);
        if(!new[]{"card","ball","hidden"}.Contains(settings.Mode))settings.Mode="card";
        if(settings.DisplayRevision<2){settings.Mode="card";settings.NativeTaskbar=false;settings.ClickThrough=false;settings.Locked=false;settings.DisplayRevision=2;settings.Left=SystemParameters.WorkArea.Right-292;settings.Top=SystemParameters.WorkArea.Top+96;}
        if(settings.DisplayRevision<3){settings.AutoHideEdges=true;settings.DisplayRevision=3;}
        foreach(var skin in Store.Read("skins.json",new List<Skin>())) { try{skin.Validate();if(!skins.Any(s=>s.Name==skin.Name))skins.Add(skin);}catch(Exception ex){Store.Log(ex.Message);} }
        engine=new MonitorEngine(settings);
        tray=new Forms.NotifyIcon { Text="景·速 Jing Monitor",Icon=trayLogo,Visible=true };
        widget=new Widget(settings);widget.OpenRequested+=Open;widget.RecoveryRequested+=RecoverWidget;widget.ThemeRequested+=ToggleTheme;widget.DisplaySettingsChanged+=()=>{if(page=="settings")ShowPage("settings");};widget.Apply(CurrentSkin);widget.Show();ApplySettings();
        tray.MouseUp+=(_,e)=>{if(e.Button==Forms.MouseButtons.Right)Dispatcher.Invoke(()=>MenuFactory.Open(BuildTrayMenu(),widget));};tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Open);
        displayTimer.Tick+=(_,_)=>RefreshMonitorWindows();displayTimer.Start();
        engine.Updated+=s=>Dispatcher.BeginInvoke(()=>OnSample(s));
        ShowPage("overview");engine.Start();
        Closing+=(_,e)=>{ if(!exiting){ e.Cancel=true;Hide();tray.ShowBalloonTip(1800,"景·速仍在监控","双击托盘图标打开；右键菜单可以退出。",Forms.ToolTipIcon.Info);} };
    }
    Skin CurrentSkin=>skins.FirstOrDefault(s=>s.Name==settings.Skin)??skins[0];
    void Open(){Show();WindowState=WindowState.Normal;Activate();}
    async Task ExitApp(){if(exiting)return;exiting=true;displayTimer.Stop();IsEnabled=false;await engine.Stop();Store.Write("settings.json",settings);widget.Close();tray.Visible=false;tray.Dispose();trayLogo.Dispose();System.Windows.Application.Current.Shutdown();}
    void ToggleTheme(){settings.Theme=Theme.IsLight?"dark":"light";Theme.Apply(settings.Theme);ThemeToggle.Content=Theme.IsLight?"☾  深色":"☀  浅色";Store.Write("settings.json",settings);ShowPage(page);}
    void ToggleThemeClick(object sender,RoutedEventArgs e)=>ToggleTheme();
    ContextMenu BuildTrayMenu()
    {
        var menu=MenuFactory.Create();MenuFactory.Item(menu,"打开控制台","\uE80F",Open);MenuFactory.Item(menu,Theme.IsLight?"切换到深色主题":"切换到浅色主题","\uE790",ToggleTheme);MenuFactory.Item(menu,"显示与提醒","\uE713",()=>{Open();ShowPage("settings");});menu.Items.Add(MenuFactory.Separator());
        var modes=MenuFactory.Item(menu,"悬浮窗显示方式","\uE8A7");foreach(var p in new[]{("悬浮卡片","card"),("悬浮球","ball"),("隐藏悬浮窗","hidden")})MenuFactory.Item(modes,p.Item1,"\uE737",()=>SetMode(p.Item2));
        MenuFactory.Check(menu,"靠边自动隐藏",settings.AutoHideEdges,v=>{settings.AutoHideEdges=v;if(!v)settings.DockSide="";ApplySettings();});MenuFactory.Item(menu,"关闭点击穿透","\uE7C3",()=>{settings.ClickThrough=false;ApplySettings();});MenuFactory.Item(menu,"恢复悬浮窗到桌面","\uE777",RecoverWidget);menu.Items.Add(MenuFactory.Separator());MenuFactory.Item(menu,"退出景·速","\uE8BB",async ()=>await ExitApp());return menu;
    }
    void ApplySettings(){Store.Write("settings.json",settings);widget.Apply(CurrentSkin);RefreshMonitorWindows();if(imagePreview!=null){imagePreview.BackgroundImage=widget.Picture;imagePreview.ImageOpacity=settings.BackgroundImageOpacity;imagePreview.ImageOverlay=settings.BackgroundOverlay;imagePreview.ImageFit=settings.BackgroundImageFit;imagePreview.InvalidateVisual();}Set("windowOpacity",$"窗口不透明度 {settings.Opacity:P0}（越低越透明）");Set("imageOpacity",$"背景图片不透明度 {settings.BackgroundImageOpacity:P0}");Set("imageOverlay",$"文字遮罩强度 {settings.BackgroundOverlay:P0}");}
    void RefreshMonitorWindows()
    {
        if(exiting)return;
        if(settings.Mode=="hidden"){widget.Hide();displayState="已隐藏 · 可点击恢复悬浮窗";}
        else {
            if(!widget.IsVisible)widget.Show();widget.EnsureDisplayPosition();displayState=settings.Mode=="ball"?"悬浮球 · 可拖动 · 已避开任务栏":"桌面悬浮卡片 · 可拖动 · 已避开任务栏";
        }
        Set("displayState",displayState);
    }
    void RecoverWidget()
    {
        settings.Mode="card";settings.AutoHideEdges=true;settings.DockSide="";settings.NativeTaskbar=false;settings.ClickThrough=false;settings.Locked=false;settings.Opacity=.94;settings.Topmost=true;
        settings.Left=SystemParameters.WorkArea.Left+32;settings.Top=SystemParameters.WorkArea.Top+80;
        ApplySettings();
    }
    void DragTitle(object s,MouseButtonEventArgs e){if(e.ClickCount==2)Maximize(s,e);else if(e.LeftButton==MouseButtonState.Pressed)DragMove();}
    void Minimize(object s,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    void Maximize(object s,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    void HideWindow(object s,RoutedEventArgs e)=>Close();
    void Navigate(object s,RoutedEventArgs e)=>ShowPage((string)((Button)s).Tag);
    void ShowPage(string name)
    {
        page=name;PageBody.Children.Clear();fields.Clear();chart=null;sensorList=null;historyList=null;imagePreview=null;
        foreach(var button in Navigation.Children.OfType<Button>()) { bool active=(string)button.Tag==name;button.Background=B(active?"#203B3C":"#111A2A");button.Foreground=B(active?"#5CE4C0":"#8D9EB5"); }
        (PageTitle.Text,PageSubtitle.Text)=name switch {
            "hardware"=>("硬件传感器","从芯片到硬盘，关注每一次性能变化。"),
            "history"=>("流量统计","每一份流量，都有迹可循。"),
            "skins"=>("皮肤工坊","你的桌面，由你定义。16 款内置皮肤，支持 JSON 自由创作。"),
            "settings"=>("显示与提醒","以你喜欢的方式，安静地陪伴桌面。"),
            "about"=>("关于景·速","轻巧的窗口，完整的掌控。"),
            _=>("实时概览","此刻的网络与系统状态，一眼尽收。") };
        switch(name){case "overview":BuildOverview();break;case "hardware":BuildHardware();break;case "history":BuildHistory();break;case "skins":BuildSkins();break;case "settings":BuildSettings();break;case "about":BuildAbout();break;}
        RefreshValues();
    }
    static SolidColorBrush B(string c)=>Theme.Brush(c);
    static TextBlock T(string text,double size=12,string color="#E8EDF5")=>new(){Text=text,FontSize=size,Foreground=B(color),TextWrapping=TextWrapping.Wrap};
    static StackPanel Stack(params UIElement[] elements){var s=new StackPanel();foreach(var el in elements)s.Children.Add(el);return s;}
    static Border Card(UIElement child,int padding=20)=>new(){Background=B("#141E2F"),BorderBrush=B("#253249"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(padding),Child=child,Margin=new Thickness(0,0,0,16)};
    static Button Btn(string label,Action action,bool primary=false){var b=new Button{Content=T(label,12,primary?"#0C332B":"#DCE5F3")};if(primary){b.Background=B("#5CE4C0");b.Foreground=B("#0C332B");}b.Click+=(_,_)=>action();return b;}
    TextBlock Field(string key,double size=30,string color="#E8EDF5"){var t=T("—",size,color);t.Margin=new Thickness(0,12,0,8);fields[key]=t;return t;}
    static Grid Columns(double[] widths,params UIElement[] children){var g=new Grid();foreach(var w in widths)g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(w,GridUnitType.Star)});for(int i=0;i<children.Length;i++){Grid.SetColumn(children[i],i);g.Children.Add(children[i]);if(i<children.Length-1&&children[i] is FrameworkElement fe)fe.Margin=new Thickness(0,0,14,16);}return g;}
    static TextBlock Note(string text){var t=T(text,11,"#8193AC");t.Margin=new Thickness(0,8,0,4);return t;}
    void BuildOverview()
    {
        Border Metric(string label,string key,string color,string sub)=>Card(Stack(T(label,12,"#8FA1BA"),Field(key,29,color),T(sub,10,"#6E829F")),18);
        PageBody.Children.Add(Columns(new double[]{1,1,1,1},Metric("↓  实时下载","down","#5CE4C0","DOWNLOAD / SEC"),Metric("↑  实时上传","up","#AB9BFF","UPLOAD / SEC"),Metric("▦  CPU 使用率","cpu","#E8EDF5","PROCESSOR LOAD"),Metric("▥  内存使用率","mem","#E8EDF5","PHYSICAL MEMORY")));
        chart=new TrafficChart{Height=218,Margin=new Thickness(0,20,0,0),Points=points};
        var graphHead=new Grid();graphHead.Children.Add(T("网络吞吐量",16));var legends=T("● 下载     ● 上传   ·   1 秒采样",10,"#8EA4BB");legends.HorizontalAlignment=HorizontalAlignment.Right;graphHead.Children.Add(legends);
        var graph=Card(Stack(graphHead,chart,Note("自动缩放 · 最近 60 个采样点")));
        var right=Card(Stack(T("今日累计",15),Field("today",28,"#5CE4C0"),Field("todayDetail",11,"#8EA1B9"),new Separator{Margin=new Thickness(0,12,0,15),Background=B("#2A374D")},T("连接状态",11,"#7187A3"),Field("network",12),Note("仅统计景·速运行期间的流量"),Btn("查看流量历史  →",()=>ShowPage("history"))));
        PageBody.Children.Add(Columns(new double[]{2.55,1},graph,right));
        var sensorHeader=new Grid();sensorHeader.Children.Add(T("硬件速览",16));var all=Btn("全部传感器  →",()=>ShowPage("hardware"));all.HorizontalAlignment=HorizontalAlignment.Right;all.Padding=new Thickness(12,5,12,5);sensorHeader.Children.Add(all);
        var temps=new UniformGrid{Columns=4,Margin=new Thickness(0,18,0,0)};
        foreach(var pair in new[]{("CPU 温度","cpuTemp"),("GPU 温度","gpuTemp"),("硬盘温度","diskTemp"),("主板温度","boardTemp")}) temps.Children.Add(Stack(T(pair.Item1,11,"#8195AD"),Field(pair.Item2,24)));
        PageBody.Children.Add(Card(Stack(sensorHeader,temps,Field("sensorStatus",10,"#7187A3"))));
        var quick=new StackPanel{Orientation=Orientation.Horizontal};quick.Children.Add(Btn("桌面悬浮卡片",()=>SetMode("card"),settings.Mode=="card"));quick.Children.Add(Btn("悬浮球",()=>SetMode("ball"),settings.Mode=="ball"));quick.Children.Add(Btn("自定义外观  ↗",()=>ShowPage("skins")));PageBody.Children.Add(quick);
    }
    void SetMode(string mode){settings.Mode=mode;ApplySettings();ShowPage(page);}
    void BuildHardware()
    {
        var top=new StackPanel{Orientation=Orientation.Horizontal};top.Children.Add(Btn("以管理员身份重启",RestartElevated,true));top.Children.Add(Btn("导出传感器 CSV",ExportSensors));top.Children.Add(Btn("传感器驱动说明",()=>Launch("https://github.com/LibreHardwareMonitor/LibreHardwareMonitor")));PageBody.Children.Add(top);
        PageBody.Children.Add(Note("温度取决于传感器、主板支持和系统权限；部分硬件需自行安装官方 PawnIO 驱动。缺失读数显示为 —，不会推测温度。"));
        sensorList=new StackPanel{Margin=new Thickness(0,16,0,0)};PageBody.Children.Add(sensorList);RenderSensors();
    }
    void RenderSensors()
    {
        if(sensorList==null)return;sensorList.Children.Clear();
        if(sample.Sensors.Count==0){sensorList.Children.Add(Card(Stack(T("暂未读取到硬件传感器",18),Note(sample.SensorStatus),Note("CPU、内存与网速仍可正常监控。硬件初始化可能需要数秒。"))));return;}
        foreach(var device in sample.Sensors.GroupBy(s=>s.Device)) {
            var rows=new StackPanel();rows.Children.Add(T(device.Key,15,"#5CE4C0"));
            foreach(var r in device) { var g=new Grid{Margin=new Thickness(0,13,0,0)};g.Children.Add(T(r.Name,12,"#9AADC5"));var value=T(r.Value.HasValue?r.Value.Value.ToString("0.0")+" "+r.Unit:"—",13);value.HorizontalAlignment=HorizontalAlignment.Right;g.Children.Add(value);rows.Children.Add(g); }
            sensorList.Children.Add(Card(rows));
        }
    }
    void BuildHistory()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};foreach(var v in new[]{("最近 7 天","7"),("最近 30 天","30"),("全部记录","all")})row.Children.Add(Btn(v.Item1,()=>{historyRange=v.Item2;ShowPage("history");},historyRange==v.Item2));row.Children.Add(Btn("导出 CSV",ExportHistory));PageBody.Children.Add(row);
        PageBody.Children.Add(Note("统计范围：所选网卡 · 仅记录软件运行期间的数据；自动汇总可能包含虚拟网卡转发流量。"));
        PageBody.Children.Add(Columns(new double[]{1,1,1},Card(Stack(T("期间下载",12,"#8398B2"),Field("historyDown",27,"#5CE4C0"))),Card(Stack(T("期间上传",12,"#8398B2"),Field("historyUp",27,"#AB9BFF"))),Card(Stack(T("累计总量",12,"#8398B2"),Field("historyTotal",27)))));
        historyList=new StackPanel();PageBody.Children.Add(Card(historyList));RenderHistory();
    }
    List<DayUsage> FilterHistory()=>engine.History().Where(d=>historyRange=="all"||string.CompareOrdinal(d.Date,DateTime.Today.AddDays(historyRange=="7"?-6:-29).ToString("yyyy-MM-dd"))>=0).OrderByDescending(d=>d.Date).ToList();
    void RenderHistory()
    {
        if(historyList==null)return;var history=FilterHistory();historyList.Children.Clear();Set("historyDown",Store.Bytes(history.Sum(d=>d.Download)));Set("historyUp",Store.Bytes(history.Sum(d=>d.Upload)));Set("historyTotal",Store.Bytes(history.Sum(d=>d.Upload+d.Download)));
        historyList.Children.Add(Columns(new double[]{1.4,1,1,2},T("日期",11,"#6D829E"),T("下载",11,"#6D829E"),T("上传",11,"#6D829E"),T("流量分布",11,"#6D829E")));
        double max=Math.Max(1,history.Count==0?1:history.Max(x=>x.Download+x.Upload));
        foreach(var d in history){var bar=new ProgressBar{Height=5,Value=100*(d.Download+d.Upload)/max,Maximum=100,Foreground=B("#5CE4C0"),Background=B("#26364D"),BorderThickness=new Thickness(0),VerticalAlignment=VerticalAlignment.Center};historyList.Children.Add(Columns(new double[]{1.4,1,1,2},T(d.Date,12),T(Store.Bytes(d.Download),12,"#5CE4C0"),T(Store.Bytes(d.Upload),12,"#AB9BFF"),bar));}
        if(history.Count==0)historyList.Children.Add(Note("还没有流量记录。开始监控后将在这里显示。"));
    }
    void BuildSkins()
    {
        var actions=new StackPanel{Orientation=Orientation.Horizontal};actions.Children.Add(Btn("＋ 导入 DIY 皮肤",ImportSkin,true));actions.Children.Add(Btn("导出当前皮肤",ExportSkin));actions.Children.Add(Btn("打开皮肤模板",ExportSkinTemplate));PageBody.Children.Add(actions);
        PageBody.Children.Add(Note("完整背景设计 · 渐变与纹理 · 多种数据布局。每张预览与真实悬浮窗使用同一套绘制逻辑，DIY 皮肤仍支持 JSON 分享与导入。"));
        var grid=new UniformGrid{Columns=3,Margin=new Thickness(0,12,0,0)};
        foreach(var skin in skins) {
            var swatch=new MonitorFace{Skin=skin,Demo=true,Height=132,Margin=new Thickness(0,0,0,14)};
            var apply=Btn(settings.Skin==skin.Name?"✓  已应用":"应用皮肤",()=>{settings.Skin=skin.Name;ApplySettings();ShowPage("skins");},settings.Skin==skin.Name);
            var name=T(skin.Name,14);name.Margin=new Thickness(0,0,0,12);var card=Card(Stack(swatch,name,apply),15);card.Margin=new Thickness(0,0,14,14);grid.Children.Add(card);
        }
        PageBody.Children.Add(grid);PageBody.Children.Add(Note("上方预览使用示例数值，仅用于展示皮肤，不代表当前系统读数。"));
    }
    void BuildSettings()
    {
        var modes=new StackPanel{Orientation=Orientation.Horizontal};foreach(var p in new[]{("悬浮卡片","card"),("悬浮球","ball"),("隐藏悬浮窗","hidden")})modes.Children.Add(Btn(p.Item1,()=>SetMode(p.Item2),settings.Mode==p.Item2));
        var appearance=Stack(T("显示方式",17),Note("独立桌面悬浮窗，不再覆盖任务栏与系统时钟。按住拖动，双击打开控制台，右键可锁定位置。"),modes);
        appearance.Children.Add(Btn(Theme.IsLight?"☾ 切换深色主题":"☀ 切换浅色主题",ToggleTheme));appearance.Children.Add(Note("主题应用于控制台和所有菜单；悬浮窗背景可独立选择皮肤或图片。"));
        appearance.Children.Add(Check("锁定悬浮窗位置",settings.Locked,v=>{settings.Locked=v;ApplySettings();}));
        appearance.Children.Add(Field("displayState",11,"#5CE4C0"));appearance.Children.Add(Btn("恢复监控窗到桌面",RecoverWidget));
        appearance.Children.Add(Field("windowOpacity",12,"#9CB4CC"));Set("windowOpacity",$"窗口不透明度 {settings.Opacity:P0}（越低越透明）");var opacity=new Slider{Minimum=.1,Maximum=1,Value=settings.Opacity,Margin=new Thickness(0,8,0,12)};opacity.ValueChanged+=(_,_)=>{settings.Opacity=opacity.Value;ApplySettings();};appearance.Children.Add(opacity);
        var top=Check("悬浮窗保持置顶",settings.Topmost,v=>{settings.Topmost=v;ApplySettings();});var through=Check("点击穿透（从托盘右键菜单可随时关闭）",settings.ClickThrough,v=>{settings.ClickThrough=v;ApplySettings();});appearance.Children.Add(top);appearance.Children.Add(through);
        appearance.Children.Add(Check("靠近屏幕边缘后自动隐藏",settings.AutoHideEdges,v=>{settings.AutoHideEdges=v;ApplySettings();}));
        appearance.Children.Add(Note("默认开启。拖到上、下、左、右边缘松开，移开鼠标约 0.75 秒后缩为提手；悬停提手即展开。窗口位置变化也会持续检测。"));
        var edges=new StackPanel{Orientation=Orientation.Horizontal};foreach(var e in new[]{("← 左边","Left"),("上边 ↑","Top"),("右边 →","Right"),("下边 ↓","Bottom")})edges.Children.Add(Btn(e.Item1,()=>{widget.DockTo(e.Item2);ShowPage("settings");}));appearance.Children.Add(edges);PageBody.Children.Add(Card(appearance));
        var background=Stack(T("自定义背景图片",17),Note("选择 PNG / JPG / BMP。图片会复制到本机数据目录，移动原图后仍可使用；仅本地保存。"));
        var imageActions=new StackPanel{Orientation=Orientation.Horizontal};imageActions.Children.Add(Btn("选择背景图片…",ImportBackground,true));imageActions.Children.Add(Btn("恢复内置背景",()=>{settings.BackgroundImage="";ApplySettings();ShowPage("settings");}));background.Children.Add(imageActions);
        imagePreview=new MonitorFace{Skin=CurrentSkin,Demo=true,BackgroundImage=widget.Picture,ImageOpacity=settings.BackgroundImageOpacity,ImageOverlay=settings.BackgroundOverlay,ImageFit=settings.BackgroundImageFit,Width=260,Height=132,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,18,0,8)};background.Children.Add(imagePreview);
        background.Children.Add(Note("预览为示例数值，实际悬浮窗显示实时数据。"));
        background.Children.Add(Field("imageOpacity",12,"#9CB4CC"));Set("imageOpacity",$"背景图片不透明度 {settings.BackgroundImageOpacity:P0}");var imageOpacity=new Slider{Minimum=0,Maximum=1,Value=settings.BackgroundImageOpacity,Margin=new Thickness(0,8,0,10)};imageOpacity.ValueChanged+=(_,_)=>{settings.BackgroundImageOpacity=imageOpacity.Value;ApplySettings();};background.Children.Add(imageOpacity);
        background.Children.Add(Field("imageOverlay",12,"#9CB4CC"));Set("imageOverlay",$"文字遮罩强度 {settings.BackgroundOverlay:P0}");var overlay=new Slider{Minimum=0,Maximum=.85,Value=settings.BackgroundOverlay,Margin=new Thickness(0,8,0,10)};overlay.ValueChanged+=(_,_)=>{settings.BackgroundOverlay=overlay.Value;ApplySettings();};background.Children.Add(overlay);
        background.Children.Add(Check("完整显示图片（关闭时居中裁切铺满）",settings.BackgroundImageFit,v=>{settings.BackgroundImageFit=v;ApplySettings();}));PageBody.Children.Add(Card(background));
        var network=Stack(T("网络采集",17),Note("选择具体网卡可避免 VPN、虚拟机等接口被重复计入。切换后首个采样用于建立计数基线。"));
        var adapter=new ComboBox{Margin=new Thickness(0,10,0,8)};adapter.Items.Add(new ComboBoxItem{Content="自动汇总所有活动网卡",Tag=""});foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.NetworkInterfaceType!=NetworkInterfaceType.Loopback))adapter.Items.Add(new ComboBoxItem{Content=nic.Name+" · "+nic.Description,Tag=nic.Id});adapter.SelectedItem=adapter.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>(string)x.Tag==settings.Adapter)??adapter.Items[0];adapter.SelectionChanged+=(_,_)=>{settings.Adapter=(string)((ComboBoxItem)adapter.SelectedItem).Tag;ApplySettings();};network.Children.Add(adapter);PageBody.Children.Add(Card(network));
        var alerts=Stack(T("阈值提醒",17),Note("超过阈值时通过系统托盘弹出提醒，同类提醒每 5 分钟最多一次；系统专注模式可能隐藏通知。"),Check("启用阈值提醒",settings.Alerts,v=>{settings.Alerts=v;ApplySettings();}));
        alerts.Children.Add(NumberSetting("内存使用率（%）",settings.MemoryLimit,1,100,v=>settings.MemoryLimit=v));alerts.Children.Add(NumberSetting("任意已读取传感器温度（°C）",settings.TemperatureLimit,20,150,v=>settings.TemperatureLimit=v));alerts.Children.Add(NumberSetting("今日累计流量（GB）",settings.TrafficLimitGB,.01,100000,v=>settings.TrafficLimitGB=v));
        alerts.Children.Add(Btn("发送测试提醒",()=>Notify("test","测试提醒","景·速的提醒已就绪。")));PageBody.Children.Add(Card(alerts));
        using var run=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");bool startup=run?.GetValue("JingMonitor")!=null;
        PageBody.Children.Add(Card(Stack(T("启动与数据",17),Check("登录 Windows 后自动启动",startup,v=>{using var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");if(v)key.SetValue("JingMonitor","\""+Environment.ProcessPath+"\"");else key.DeleteValue("JingMonitor",false);}),Note("开机启动使用当前 EXE 的位置，启用后请勿移动单文件版。"),Btn("打开本地数据目录",()=>Launch(Store.Root)),Note(Store.Root))));
    }
    CheckBox Check(string text,bool value,Action<bool> changed){var c=new CheckBox{Content=text,IsChecked=value};c.Checked+=(_,_)=>changed(true);c.Unchecked+=(_,_)=>changed(false);return c;}
    UIElement NumberSetting(string label,double value,double min,double max,Action<double> changed)
    {
        var row=new Grid{Margin=new Thickness(0,8,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(130)});row.Children.Add(T(label,12,"#A3B2C7"));var input=new TextBox{Text=value.ToString("0.##")};Grid.SetColumn(input,1);row.Children.Add(input);
        input.LostFocus+=(_,_)=>{if(double.TryParse(input.Text,out double n)&&double.IsFinite(n)&&n>=min&&n<=max){changed(n);ApplySettings();input.BorderBrush=B("#344058");}else {input.Text=value.ToString("0.##");input.BorderBrush=B("#F19085");MessageBox.Show($"请输入 {min}–{max} 范围内的数值。","数值范围");}};return row;
    }
    void BuildAbout()
    {
        var hero=Stack(new BrandLogo{Width=56,Height=56,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,14)},T("景 · 速",32),Note("JING MONITOR   /   VERSION 1.3.0"),T("让性能可见，让桌面更美。",21),Note("一款为 Windows 打造的本地网速与硬件监控工具。\n从可自由拖动的桌面卡片，到属于你的 DIY 皮肤，随时了解电脑的状态。"));PageBody.Children.Add(Card(hero,28));
        PageBody.Children.Add(Columns(new double[]{1,1,1},Card(Stack(T("实时感知",17),Note("上传 / 下载 · CPU · 内存\nGPU · 硬盘 · 主板传感器"))),Card(Stack(T("自由呈现",17),Note("悬浮卡片 · 悬浮球 · 自由拖动\n透明度 · 点击穿透 · 16 款皮肤"))),Card(Stack(T("安心记录",17),Note("本地流量历史 · CSV 导出\n内存、温度与流量阈值提醒")))));
        var contact=Stack(T("联系作者",20),Note("有想法、有建议，或想分享你的 DIY 皮肤，欢迎来找我。"));var links=new WrapPanel{Margin=new Thickness(0,18,0,8)};links.Children.Add(Btn("微信公众号：景同志  ⧉",()=>Copy("景同志"),true));links.Children.Add(Btn("小红书：景同学  ⧉",()=>Copy("景同学")));links.Children.Add(Btn("GitHub 开源主页  ⧉",()=>Launch(RepoUrl)));contact.Children.Add(links);contact.Children.Add(Note("点击复制名称，在对应平台搜索关注；或在 GitHub 查看源码、提交 Issue 与下载安装包。"));PageBody.Children.Add(Card(contact,24));
        PageBody.Children.Add(Note("硬件传感器：LibreHardwareMonitor（MPL-2.0）及其依赖。温度与部分 GPU 读数受硬件和驱动支持限制。\n本软件不会上传监控数据。社区皮肤采用本地文件导入；当前版本未连接在线皮肤市场。"));
        PageBody.Children.Add(Btn("查看第三方项目与许可证",ShowLicenses));
    }
    void OnSample(Sample s)
    {
        if(exiting)return;sample=s;points.Add((s.Download,s.Upload));if(points.Count>60)points.RemoveAt(0);widget.Update(s);
        RefreshValues();
        LiveState.Text="●  实时监控中";Clock.Text=s.Time.ToString("yyyy/MM/dd   HH:mm:ss");Status.Text=s.Network+"   ·   本地采集 / 1 秒";
        if(!settings.Alerts)return;
        if(s.Memory>=settings.MemoryLimit)Notify("memory","内存使用率较高",$"当前 {s.Memory:0}% ，超过阈值 {settings.MemoryLimit:0}%。");
        var hot=s.Sensors.FirstOrDefault(x=>x.Unit=="°C"&&x.Value>=settings.TemperatureLimit);if(hot!=null)Notify("temperature","硬件温度提醒",$"{hot.Device} / {hot.Name}：{hot.Value:0}°C");
        var day=engine.History().FirstOrDefault(x=>x.Date==DateTime.Now.ToString("yyyy-MM-dd"));if(day!=null&&day.Download+day.Upload>=settings.TrafficLimitGB*1073741824d)Notify("traffic","今日流量达到阈值",$"今日已使用 {Store.Bytes(day.Download+day.Upload)}。");
    }
    void RefreshValues()
    {
        Set("down",Store.Bytes(sample.Download)+"/s");Set("up",Store.Bytes(sample.Upload)+"/s");Set("cpu",sample.Cpu.ToString("0.0")+" %");Set("mem",sample.Memory.ToString("0")+" %");Set("network",sample.Network);Set("sensorStatus",sample.SensorStatus);
        var today=engine.History().FirstOrDefault(d=>d.Date==DateTime.Now.ToString("yyyy-MM-dd"));Set("today",Store.Bytes((today?.Download??0)+(today?.Upload??0)));Set("todayDetail","↓ "+Store.Bytes(today?.Download??0)+"\n↑ "+Store.Bytes(today?.Upload??0));
        void Temp(string key,Func<SensorRow,bool> filter){var values=sample.Sensors.Where(s=>s.Unit=="°C"&&filter(s)&&s.Value.HasValue).Select(s=>s.Value!.Value).ToArray();Set(key,values.Length==0?"—":values.Max().ToString("0")+" °C");}
        Temp("cpuTemp",s=>s.Kind=="Cpu");Temp("gpuTemp",s=>s.Kind.StartsWith("Gpu"));Temp("diskTemp",s=>s.Kind=="Storage");Temp("boardTemp",s=>s.Kind=="Motherboard"||s.Kind=="SuperIO");
        chart?.InvalidateVisual();if(page=="hardware")RenderSensors();if(page=="history")RenderHistory();
    }
    void Set(string key,string value){if(fields.TryGetValue(key,out var t))t.Text=value;}
    void Notify(string key,string title,string message){if(alertTimes.TryGetValue(key,out var last)&&DateTime.UtcNow-last<TimeSpan.FromMinutes(5))return;alertTimes[key]=DateTime.UtcNow;tray.ShowBalloonTip(5000,title,message,Forms.ToolTipIcon.Warning);}
    static void Launch(string path){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
    static void Copy(string text){System.Windows.Clipboard.SetText(text);MessageBox.Show("已复制："+text,"联系作者");}
    void ShowLicenses(){using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("JingMonitor.Notices");using var reader=new StreamReader(stream!);var text=new TextBox{Text=reader.ReadToEnd(),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(15),FontSize=12};new Window{Title="第三方许可证与源码地址",Width=820,Height=620,Background=B("#101827"),Content=text,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner}.ShowDialog();}
    async void RestartElevated(){try{var p=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",Arguments="--restart"});if(p!=null)await ExitApp();}catch(System.ComponentModel.Win32Exception ex){if(ex.NativeErrorCode!=1223)MessageBox.Show(ex.Message);}}
    void ImportSkin(){var d=new OpenFileDialog{Filter="JSON 皮肤|*.json"};if(d.ShowDialog()!=true)return;try{if(new FileInfo(d.FileName).Length>65536)throw new Exception("皮肤文件不能超过 64 KB");var skin=JsonSerializer.Deserialize<Skin>(File.ReadAllText(d.FileName),Store.Json)??throw new Exception("无效皮肤");skin.Validate();if(skins.Any(s=>s.Name==skin.Name))throw new Exception("皮肤名称已存在，请修改 JSON 中的 Name");skins.Add(skin);Store.Write("skins.json",skins.Skip(16).ToList());settings.Skin=skin.Name;ApplySettings();ShowPage("skins");}catch(Exception ex){MessageBox.Show(ex.Message,"导入失败");}}
    void ImportBackground(){var dialog=new OpenFileDialog{Filter="背景图片|*.png;*.jpg;*.jpeg;*.bmp",Title="选择悬浮窗背景图片"};if(dialog.ShowDialog()!=true)return;try{settings.BackgroundImage=BackgroundImages.Import(dialog.FileName);ApplySettings();ShowPage("settings");}catch(Exception ex){MessageBox.Show("图片未导入："+ex.Message,"背景图片");}}
    void ExportSkin()=>SaveText("我的皮肤.json","JSON 皮肤|*.json",JsonSerializer.Serialize(CurrentSkin,Store.Json));
    void ExportSkinTemplate()=>SaveText("DIY皮肤模板.json","JSON 皮肤|*.json",JsonSerializer.Serialize(new Skin(),Store.Json));
    static void SaveText(string name,string filter,string content){var d=new SaveFileDialog{FileName=name,Filter=filter};if(d.ShowDialog()==true){File.WriteAllText(d.FileName,content,new UTF8Encoding(true));MessageBox.Show("已保存到：\n"+d.FileName,"导出完成");}}
    static string Csv(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
    void ExportHistory(){var b=new StringBuilder("日期,下载字节,上传字节\r\n");foreach(var d in FilterHistory())b.AppendLine($"{d.Date},{d.Download.ToString("0",System.Globalization.CultureInfo.InvariantCulture)},{d.Upload.ToString("0",System.Globalization.CultureInfo.InvariantCulture)}");SaveText("流量统计.csv","CSV|*.csv",b.ToString());}
    void ExportSensors(){var b=new StringBuilder("设备,传感器,数值,单位\r\n");foreach(var s in sample.Sensors)b.AppendLine($"{Csv(s.Device)},{Csv(s.Name)},{s.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Csv(s.Unit)}");SaveText("传感器.csv","CSV|*.csv",b.ToString());}
    void CaptureWidget(string name)
    {
        widget.UpdateLayout();
        if(!widget.IsVisible||!widget.IsFrameVisible||!widget.IsOnScreen||!widget.HasDisplayText)throw new Exception(name+"：监控窗未处于可见屏幕范围或缺少文字");
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(widget.ActualWidth),(int)Math.Ceiling(widget.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(widget);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));Directory.CreateDirectory("smoke-results");using var file=File.Create(Path.Combine("smoke-results",name+".png"));encoder.Save(file);
    }
    void CaptureElement(FrameworkElement element,string name)
    {
        element.UpdateLayout();if(element.ActualWidth<1||element.ActualHeight<1)throw new Exception("未渲染："+name);
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(element);var e=new System.Windows.Media.Imaging.PngBitmapEncoder();e.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));Directory.CreateDirectory("smoke-results");using var file=File.Create(Path.Combine("smoke-results",name+".png"));e.Save(file);
    }
    void CaptureSkinGallery()
    {
        var grid=new UniformGrid{Columns=4,Margin=new Thickness(20,0,20,20)};
        foreach(var skin in skins.Take(16)){
            var label=T(skin.Name+"  /  "+skin.Pattern,12,"#B8C8DA");label.Margin=new Thickness(4,12,0,0);
            var panel=Stack(new MonitorFace{Skin=skin,Demo=true,Width=260,Height=132},label);panel.Margin=new Thickness(10,10,10,16);grid.Children.Add(panel);
        }
        var title=T("景 · 速   /   FLOATING COLLECTION",28);title.Margin=new Thickness(30,24,0,8);
        var note=T("16 款完整背景皮肤 · 支持卡片与悬浮球 · 预览数值为示例",13,"#8CA3BE");note.Margin=new Thickness(30,0,0,18);
        var root=new Border{Width=1200,Background=B("#0C1524"),Child=Stack(title,note,grid)};root.Measure(new Size(1200,double.PositiveInfinity));root.Arrange(new Rect(new Point(0,0),root.DesiredSize));root.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1200,(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));Directory.CreateDirectory("smoke-results");using var f=File.Create("smoke-results/skin-gallery.png");encoder.Save(f);
    }
    public async void RunSmokeTest()
    {
        var results=new List<string>();
        try {
            await Task.Delay(300);CaptureWidget("monitor-startup");results.Add("PASS: monitor visible, on-screen and populated immediately at startup");
            await Task.Delay(11700);
            if(points.Count<2)throw new Exception("实时采集没有产生足够采样");
            if(sample.TotalMemory<=0)throw new Exception("物理内存采集失败");
            results.Add("PASS: live sampling, CPU, physical memory, network counters");
            if(!settings.AutoHideEdges)throw new Exception("升级后靠边隐藏未默认启用");
            foreach(var name in new[]{"overview","hardware","history","skins","settings","about"}) {
                ShowPage(name);UpdateLayout();await Task.Delay(250);
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                Directory.CreateDirectory("smoke-results");using var file=File.Create(Path.Combine("smoke-results",name+".png"));encoder.Save(file);
                results.Add("PASS: render "+name);
            }
            int index=0;foreach(var skin in skins){skin.Validate();widget.Apply(skin);await Task.Delay(30);CaptureWidget("skin-"+(++index).ToString("00"));}CaptureSkinGallery();results.Add("PASS: render every complete-background skin and gallery");
            foreach(var mode in new[]{"card","ball","hidden"}){settings.Mode=mode;ApplySettings();await Task.Delay(150);if(mode!="hidden")CaptureWidget("monitor-"+mode);else if(widget.IsVisible)throw new Exception("隐藏模式未生效");}results.Add("PASS: all display modes; visible monitor screenshots and screen bounds verified");
            settings.Mode="card";settings.Left=-99999;settings.Top=-99999;ApplySettings();await Task.Delay(100);CaptureWidget("monitor-recovered");results.Add("PASS: off-screen saved position recovered to a visible monitor");
            settings.Mode="hidden";ApplySettings();RecoverWidget();await Task.Delay(100);CaptureWidget("monitor-rescue");if(settings.ClickThrough||settings.Mode!="card")throw new Exception("恢复监控窗失败");results.Add("PASS: restore monitor action recovers hidden mode and disables click-through");
            ShowPage("overview");
            foreach(var side in new[]{"Left","Top","Right","Bottom"}){
                settings.AutoHideEdges=true;var bounds=widget.ScreenBounds;var a=Forms.Screen.FromRectangle(bounds).WorkingArea;var centered=new System.Drawing.Rectangle(a.Left+(a.Width-bounds.Width)/2,a.Top+(a.Height-bounds.Height)/2,bounds.Width,bounds.Height);var destination=EdgeGeometry.Anchor(centered,a,side);
                widget.MoveToForTest(destination);await Task.Delay(500);
                if(settings.DockSide!=side)throw new Exception("原生窗口移到边缘后未自动识别："+side);
                var expandedBounds=widget.ScreenBounds;widget.TrackPointer(expandedBounds.Right+1000,expandedBounds.Bottom+1000,DateTime.UtcNow.AddSeconds(2));if(!widget.IsCollapsed)throw new Exception("自动识别后未缩回："+side);CaptureWidget("auto-edge-"+side);
                var strip=widget.ScreenBounds;widget.TrackPointer(strip.Left+strip.Width/2,strip.Top+strip.Height/2,DateTime.UtcNow);if(widget.IsCollapsed)throw new Exception("悬停未展开："+side);
            }results.Add("PASS: native window moved to FOUR edges; real dispatcher timer detects without DockTo; leave/hover state transitions pass");RecoverWidget();
            foreach(var side in new[]{"Left","Top","Right","Bottom"}){widget.DockTo(side);widget.Collapse();if(!widget.IsCollapsed||!widget.IsOnScreen)throw new Exception("边缘折叠失败："+side);CaptureWidget("edge-"+side+"-collapsed");widget.Reveal();if(widget.IsCollapsed||!widget.IsOnScreen)throw new Exception("边缘展开失败："+side);CaptureWidget("edge-"+side+"-expanded");}results.Add("PASS: all four edge tabs collapse and reveal inside the working area");
            widget.DockTo("Right");var tab=widget.ScreenBounds;widget.TrackPointer(tab.Left+tab.Width/2,tab.Top+tab.Height/2,DateTime.UtcNow);if(widget.IsCollapsed)throw new Exception("鼠标靠近未展开");var full=widget.ScreenBounds;widget.TrackPointer(full.Right+1000,full.Bottom+1000,DateTime.UtcNow.AddSeconds(2));if(!widget.IsCollapsed)throw new Exception("鼠标离开未缩回");results.Add("PASS: shared pointer tracking reveals on hover and collapses after leave delay");
            settings.AutoHideEdges=false;settings.DockSide="";RecoverWidget();
            var fixture=new System.Windows.Media.Imaging.RenderTargetBitmap(400,200,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new LinearGradientBrush(Colors.MidnightBlue,Colors.Coral,30),null,new Rect(0,0,400,200));dc.DrawEllipse(Brushes.Gold,null,new Point(310,60),40,40);}fixture.Render(visual);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(fixture));string input=Path.Combine(Store.Root,"fixture.png");using(var stream=File.Create(input))png.Save(stream);settings.BackgroundImage=BackgroundImages.Import(input);File.Delete(input);ApplySettings();if(widget.Picture==null)throw new Exception("自定义背景未加载");CaptureWidget("custom-image-crop");settings.BackgroundImageFit=true;settings.BackgroundImageOpacity=.65;settings.BackgroundOverlay=.55;ApplySettings();CaptureWidget("custom-image-fit");results.Add("PASS: image imported into managed storage, survives source deletion, crop/fit and opacity rendered");
            settings.Opacity=.35;ApplySettings();if(Math.Abs(widget.Opacity-.35)>.01)throw new Exception("窗口透明度未生效");CaptureWidget("opacity-35");settings.Opacity=.94;settings.BackgroundImage="";ApplySettings();if(widget.Picture!=null)throw new Exception("内置背景恢复失败");results.Add("PASS: window opacity and built-in background restore");
            settings.ClickThrough=true;ApplySettings();if(!widget.IsClickThrough)throw new Exception("点击穿透原生样式未设置");settings.ClickThrough=false;ApplySettings();if(widget.IsClickThrough)throw new Exception("点击穿透未解除");results.Add("PASS: click-through style and recovery");
            foreach(var theme in new[]{"light","dark"}){
                if(Theme.IsLight!=(theme=="light"))ThemeToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                foreach(var p in new[]{"overview","hardware","history","skins","settings","about"}){ShowPage(p);await Task.Delay(70);CaptureElement(this,p+"-"+theme);}
                foreach(var kind in new[]{"widget","tray"}){var menu=kind=="widget"?widget.BuildMenu():BuildTrayMenu();menu.Placement=PlacementMode.AbsolutePoint;menu.HorizontalOffset=260;menu.VerticalOffset=100;menu.IsOpen=true;await Task.Delay(120);if(menu.ActualWidth<200||menu.Items.Count<5)throw new Exception("菜单未正常打开："+kind);CaptureElement(menu,"menu-"+kind+"-"+theme);var sub=menu.Items.OfType<MenuItem>().FirstOrDefault(i=>i.HasItems);if(sub!=null){sub.IsSubmenuOpen=true;await Task.Delay(120);var popup=sub.Template.FindName("PART_Popup",sub) as Popup;if(popup==null||!popup.IsOpen||popup.Child is not FrameworkElement child||child.ActualWidth<150)throw new Exception("子菜单未展开："+kind);CaptureElement(child,"submenu-"+kind+"-"+theme);sub.IsSubmenuOpen=false;}menu.IsOpen=false;}
            }results.Add("PASS: six pages in BOTH themes and both actual WPF popup menus rendered");
            if(!object.ReferenceEquals(tray.Icon,trayLogo)||Icon==null||widget.Icon==null)throw new Exception("Logo 未应用到图标");results.Add("PASS: brand icon assigned to tray and both application windows");
            var actionMenu=widget.BuildMenu();var autoItem=actionMenu.Items.OfType<MenuItem>().First(i=>(i.Header as TextBlock)?.Text=="靠边自动隐藏");bool originalAuto=settings.AutoHideEdges;autoItem.IsChecked=!originalAuto;autoItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(settings.AutoHideEdges==originalAuto)throw new Exception("右键自动隐藏操作未生效");autoItem.IsChecked=originalAuto;autoItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));if(settings.AutoHideEdges!=originalAuto)throw new Exception("菜单恢复失败");results.Add("PASS: actual menu Click routing changes and restores application setting; theme toolbar Click routing passes");
            using(var fileIcon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!))using(var expected=Brand.Icon())using(var actual=fileIcon!.ToBitmap())using(var target=new System.Drawing.Bitmap(expected.ToBitmap(),actual.Size)){
                var ac=actual.GetPixel(actual.Width/2,actual.Height/2);var ex=target.GetPixel(target.Width/2,target.Height/2);if(Math.Abs(ac.R-ex.R)+Math.Abs(ac.G-ex.G)+Math.Abs(ac.B-ex.B)>24)throw new Exception("EXE 原生图标与品牌不一致");
            }results.Add("PASS: EXE native Win32 icon matches the new brand");
            engine.Save();if(engine.History().Count==0)throw new Exception("历史记录未生成");results.Add("PASS: history persisted");
            results.Add("SENSORS: "+sample.Sensors.Count+"; "+sample.SensorStatus);
            results.Add($"CPU={sample.Cpu:0.0}% RAM={sample.Memory:0}% TotalMemory={sample.TotalMemory:0}");
            File.WriteAllLines("smoke-results/result.txt",results);await ExitApp();
        } catch(Exception ex){Directory.CreateDirectory("smoke-results");File.WriteAllText("smoke-results/FAILED.txt",ex.ToString());await ExitApp();}
    }
}
