using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

class Setup : Form
{
    readonly TextBox destination=new TextBox();
    readonly Label status=new Label();
    readonly Button install=new Button();
    readonly CheckBox shortcut=new CheckBox();
    static readonly string DefaultPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","JingMonitor");
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2&&args[0]=="--verify") {try{Extract(Path.GetFullPath(args[1]));return 0;}catch(Exception e){File.WriteAllText("installer-verify-error.txt",e.ToString());return 1;}}
        Application.Run(new Setup());return 0;
    }
    public Setup()
    {
        Text="景·速 安装向导";ClientSize=new Size(620,470);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(14,22,36);ForeColor=Color.FromArgb(231,239,250);Font=new Font("Microsoft YaHei UI",10);
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);var logo=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom};logo.SetBounds(32,28,62,62);using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("brandlogo")){if(stream!=null)using(var image=Image.FromStream(stream))logo.Image=new Bitmap(image);}Controls.Add(logo);
        AddLabel("景 · 速",110,34,450,55,28,Color.FromArgb(92,228,192));
        AddLabel("让每一刻，都心中有数。",34,97,540,30,14,ForeColor);
        AddLabel("JING MONITOR  /  WINDOWS x64  /  v1.3.0",35,140,540,24,9,Color.FromArgb(130,151,176));
        AddLabel("实时网速与硬件监控 · 16 款 DIY 皮肤 · 本地流量统计",35,179,555,28,10,ForeColor);
        AddLabel("安装位置（仅为当前用户安装，无需管理员权限）",35,230,555,25,10,Color.FromArgb(151,172,197));
        destination.SetBounds(35,264,462,30);destination.Text=DefaultPath;destination.BackColor=Color.FromArgb(30,43,62);destination.ForeColor=ForeColor;destination.BorderStyle=BorderStyle.FixedSingle;Controls.Add(destination);
        var editMenu=new ContextMenuStrip{Renderer=new ToolStripProfessionalRenderer(new SetupMenuColors()),BackColor=Color.FromArgb(20,30,47),ForeColor=ForeColor,Font=new Font("Microsoft YaHei UI",10),ShowImageMargin=false,Padding=new Padding(7)};
        var cut=editMenu.Items.Add("剪切",null,delegate{destination.Cut();});var copy=editMenu.Items.Add("复制",null,delegate{destination.Copy();});var paste=editMenu.Items.Add("粘贴",null,delegate{destination.Paste();});editMenu.Items.Add(new ToolStripSeparator());editMenu.Items.Add("全选",null,delegate{destination.SelectAll();});foreach(ToolStripItem item in editMenu.Items)item.Padding=new Padding(10,6,20,6);editMenu.Opening+=delegate{cut.Enabled=copy.Enabled=destination.SelectionLength>0;paste.Enabled=Clipboard.ContainsText();};destination.ContextMenuStrip=editMenu;
        var browse=new Button{Text="浏览…",FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(34,49,69)};browse.SetBounds(508,263,77,31);browse.Click+=delegate{using(var d=new FolderBrowserDialog()){d.SelectedPath=destination.Text;if(d.ShowDialog()==DialogResult.OK)destination.Text=d.SelectedPath;}};Controls.Add(browse);
        shortcut.Text="创建桌面快捷方式";shortcut.Checked=true;shortcut.SetBounds(35,312,300,28);Controls.Add(shortcut);
        status.Text="安装前请退出正在运行的景·速。";status.SetBounds(35,354,550,38);status.ForeColor=Color.FromArgb(142,162,187);Controls.Add(status);
        install.Text="安装景·速  →";install.FlatStyle=FlatStyle.Flat;install.BackColor=Color.FromArgb(92,228,192);install.ForeColor=Color.FromArgb(12,44,36);install.SetBounds(395,402,190,42);install.Click+=Install;Controls.Add(install);
        AddLabel("微信公众号：景同志   ·   小红书：景同学",35,410,350,30,9,Color.FromArgb(112,135,162));
    }
    void AddLabel(string text,int x,int y,int w,int h,int size,Color color){var l=new Label{Text=text,Font=new Font("Microsoft YaHei UI",size),ForeColor=color};l.SetBounds(x,y,w,h);Controls.Add(l);}
    static void Resource(string name,string path)
    {
        using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using(var output=File.Create(path)){if(input==null)throw new Exception("安装资源缺失："+name);input.CopyTo(output);}
    }
    static void Extract(string path)
    {
        Directory.CreateDirectory(path);
        Resource("payload",Path.Combine(path,"JingMonitor.exe"));
        Resource("uninstall",Path.Combine(path,"Uninstall.exe"));
        Resource("readme",Path.Combine(path,"使用说明.txt"));
        Resource("notices",Path.Combine(path,"THIRD-PARTY-NOTICES.txt"));
    }
    void Install(object sender,EventArgs args)
    {
        install.Enabled=false;
        try {
            if(Environment.Is64BitOperatingSystem==false)throw new Exception("此版本仅支持 Windows x64 系统。");
            string path=Path.GetFullPath(destination.Text.Trim());
            if(Path.GetPathRoot(path).TrimEnd('\\').Equals(path.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new Exception("请选择专用安装文件夹，不能使用磁盘根目录。");
            if(File.Exists(Path.Combine(path,"JingMonitor.exe"))) { try{using(var f=File.Open(Path.Combine(path,"JingMonitor.exe"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {}}catch{throw new Exception("请先从系统托盘退出正在运行的景·速。");} }
            status.Text="正在安装…";Application.DoEvents();Extract(path);
            string exe=Path.Combine(path,"JingMonitor.exe");
            if(shortcut.Checked)CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"景·速.lnk"),exe,path);
            string start=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"景·速");Directory.CreateDirectory(start);CreateShortcut(Path.Combine(start,"景·速.lnk"),exe,path);CreateShortcut(Path.Combine(start,"卸载景·速.lnk"),Path.Combine(path,"Uninstall.exe"),path);
            using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\JingMonitor")){
                key.SetValue("DisplayName","景·速 Jing Monitor");key.SetValue("DisplayVersion","1.3.0");key.SetValue("Publisher","景·速");key.SetValue("InstallLocation",path);key.SetValue("DisplayIcon",exe);key.SetValue("UninstallString","\""+Path.Combine(path,"Uninstall.exe")+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
            }
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
            status.Text="安装完成！可从桌面或开始菜单打开景·速。";install.Text="完成并启动  →";install.Enabled=true;install.Click-=Install;install.Click+=delegate{Process.Start(exe);Close();};
        } catch(Exception ex){MessageBox.Show(ex.Message,"安装未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);status.Text="安装未完成，请检查安装路径后重试。";install.Enabled=true;}
    }
    static void CreateShortcut(string path,string target,string directory)
    {
        Type t=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(t);dynamic link=shell.CreateShortcut(path);link.TargetPath=target;link.IconLocation=target+",0";link.WorkingDirectory=directory;link.Description="景·速 Windows 实时监控工具";link.Save();
    }
    [DllImport("shell32.dll")]static extern void SHChangeNotify(uint ev,uint flags,IntPtr item1,IntPtr item2);
}
class SetupMenuColors:ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground{get{return Color.FromArgb(20,30,47);}}
    public override Color MenuItemSelected{get{return Color.FromArgb(32,59,60);}}
    public override Color MenuItemBorder{get{return Color.FromArgb(32,59,60);}}
    public override Color MenuBorder{get{return Color.FromArgb(48,66,87);}}
    public override Color SeparatorDark{get{return Color.FromArgb(48,66,87);}}
    public override Color SeparatorLight{get{return Color.FromArgb(20,30,47);}}
}
