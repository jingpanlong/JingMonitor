using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
class Uninstall
{
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        if(MessageBox.Show("卸载景·速？流量历史与个人皮肤将保留。\n请先从系统托盘退出景·速。","卸载景·速",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        string folder=AppDomain.CurrentDomain.BaseDirectory;
        try {
            // Delete only files owned by this installer. Never recursively remove the selected folder.
            File.Delete(Path.Combine(folder,"JingMonitor.exe"));File.Delete(Path.Combine(folder,"使用说明.txt"));File.Delete(Path.Combine(folder,"THIRD-PARTY-NOTICES.txt"));
            using(var run=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run",true)){
                string value=run==null?"":run.GetValue("JingMonitor","").ToString();if(value.Trim('"').Equals(Path.Combine(folder,"JingMonitor.exe"),StringComparison.OrdinalIgnoreCase))run.DeleteValue("JingMonitor",false);
            }
            Registry.CurrentUser.DeleteSubKeyTree("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\JingMonitor",false);
            string start=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"景·速");File.Delete(Path.Combine(start,"景·速.lnk"));File.Delete(Path.Combine(start,"卸载景·速.lnk"));if(Directory.Exists(start)&&Directory.GetFileSystemEntries(start).Length==0)Directory.Delete(start);
            string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"景·速.lnk");if(File.Exists(desktop))File.Delete(desktop);
            MessageBox.Show("景·速已卸载。流量与设置保留在：\n"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JingMonitor")+"\n\n安装目录中仅保留卸载程序，可手动删除。","卸载完成");
        } catch(Exception ex){MessageBox.Show("卸载未完成，请确认景·速已退出。\n"+ex.Message,"卸载景·速");}
    }
}
