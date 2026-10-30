using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace JingMonitor;
public partial class App : System.Windows.Application
{
    private Mutex? mutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--export-brand"){Brand.Export(e.Args[1]);Shutdown();return;}
        if (Array.Exists(e.Args, x => x == "--self-test")) { Environment.Exit(SelfTests.Run()); return; }
        bool smoke=Array.Exists(e.Args,x=>x=="--smoke-test");
        if(smoke) Store.Root=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"smoke-data"));
        mutex = new Mutex(false, smoke?"Local\\JingMonitor-Smoke":"Local\\JingMonitor-1");
        bool created=false;
        try { created=mutex.WaitOne(Array.Exists(e.Args,x=>x=="--restart")?20000:0); } catch(AbandonedMutexException){created=true;}
        if (!created) { MessageBox.Show("景·速已经在运行，请从系统托盘打开。", "景·速"); Shutdown(); return; }
        DispatcherUnhandledException += (_, a) => { Store.Log(a.Exception.ToString()); MessageBox.Show("操作未完成：" + a.Exception.Message, "景·速"); a.Handled = true; };
        if(smoke) Store.Write("settings.json",new Settings{Alerts=false});
        MainWindow = new MainWindow(); MainWindow.Show();
        if(smoke) ((MainWindow)MainWindow).RunSmokeTest();
    }
    protected override void OnExit(ExitEventArgs e) { mutex?.Dispose(); base.OnExit(e); }
}
