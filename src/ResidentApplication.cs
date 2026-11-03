// 用途：管理单实例、系统托盘、后台常驻与三档开机启动。
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

namespace CubicDesk {
  public sealed class SingleInstanceGate : IDisposable {
    readonly Mutex mutex;readonly string eventName;
    public readonly bool Primary;
    public readonly EventWaitHandle ShowEvent;
    public SingleInstanceGate(string scope=null){
      string name="Local\\CubicDesk-"+(scope??WindowsIdentity.GetCurrent().User.Value);bool created;
      mutex=new Mutex(true,name,out created);Primary=created;eventName=name+"-Show";
      if(Primary)ShowEvent=new EventWaitHandle(false,EventResetMode.AutoReset,eventName);
    }
    public bool Activate(){for(int attempt=0;attempt<20;attempt++){try{using(var signal=EventWaitHandle.OpenExisting(eventName)){signal.Set();return true;}}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(50);}}return false;}
    public void Dispose(){if(ShowEvent!=null)ShowEvent.Dispose();if(Primary)mutex.ReleaseMutex();mutex.Dispose();}
  }
  public enum LoginStartupMode {Off,Frontend,Backend}
  public static class LoginStartup {
    public const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName="CubicDesk";
    public static string CommandFor(LoginStartupMode mode){if(mode==LoginStartupMode.Off)return "";if(mode!=LoginStartupMode.Frontend&&mode!=LoginStartupMode.Backend)throw new ArgumentOutOfRangeException("mode");return "\""+Path.Combine(Store.Root,"CubicDesk.exe")+"\""+(mode==LoginStartupMode.Backend?" --background":"");}
    public static string Label(LoginStartupMode mode){return mode==LoginStartupMode.Frontend?"前端":mode==LoginStartupMode.Backend?"仅后端":"关闭";}
    public static string RegisteredCommand {get{using(var key=Registry.CurrentUser.OpenSubKey(KeyPath,false))return key==null?"":Convert.ToString(key.GetValue(ValueName,""));}}
    public static LoginStartupMode Mode {get{string command=RegisteredCommand;return string.Equals(command,CommandFor(LoginStartupMode.Backend),StringComparison.OrdinalIgnoreCase)?LoginStartupMode.Backend:string.Equals(command,CommandFor(LoginStartupMode.Frontend),StringComparison.OrdinalIgnoreCase)?LoginStartupMode.Frontend:LoginStartupMode.Off;}}
    public static void SetMode(LoginStartupMode mode){
      string command=CommandFor(mode);if(command.Length>260)throw new IOException("启动路径过长，请把程序移动到较短的文件夹后重试。");
      using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){if(mode!=LoginStartupMode.Off)key.SetValue(ValueName,command,RegistryValueKind.String);else key.DeleteValue(ValueName,false);}
      if(!string.Equals(RegisteredCommand,command,StringComparison.OrdinalIgnoreCase))throw new IOException("无法确认开机启动设置，请重试。");
    }
  }
  public sealed class ResidentApplication : Application {
    readonly SingleInstanceGate gate;readonly bool background;
    Forms.NotifyIcon tray;Forms.ToolStripMenuItem deviceStatus,startupItem;
    readonly Forms.ToolStripMenuItem[] startupModes=new Forms.ToolStripMenuItem[3];
    RegisteredWaitHandle activation;DispatcherTimer statusTimer;
    public bool Exiting {get;private set;}
    public static ResidentApplication Host {get{return Current as ResidentApplication;}}
    public ResidentApplication(SingleInstanceGate instance,bool startHidden){gate=instance;background=startHidden;ShutdownMode=ShutdownMode.OnExplicitShutdown;}
    protected override void OnStartup(StartupEventArgs e){
      base.OnStartup(e);MainWindow=new MainWindow();
      var menu=new DeskTrayMenu();menu.BackColor=System.Drawing.Color.White;menu.ForeColor=System.Drawing.Color.FromArgb(38,60,89);
      menu.Items.Add("打开 Cubic Desk",null,delegate{OpenWindow();});
      deviceStatus=new Forms.ToolStripMenuItem("后台正在启动…"){Enabled=false};menu.Items.Add(deviceStatus);menu.Items.Add(new Forms.ToolStripSeparator());
      startupItem=new Forms.ToolStripMenuItem("开机启动");startupItem.DropDown=new DeskTrayMenu {ShowCheckMargin=true};for(int i=0;i<3;i++){var mode=(LoginStartupMode)i;var item=new Forms.ToolStripMenuItem(LoginStartup.Label(mode));item.Click+=delegate{try{LoginStartup.SetMode(mode);RefreshTray();}catch(Exception ex){MessageBox.Show(ex.Message,"开机启动设置",MessageBoxButton.OK,MessageBoxImage.Error);}};startupModes[i]=item;startupItem.DropDownItems.Add(item);}menu.Items.Add(startupItem);
      menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("彻底退出（停止后台）",null,delegate{ExitApplication();});foreach(Forms.ToolStripItem row in menu.Items)if(!(row is Forms.ToolStripSeparator))row.Padding=new Forms.Padding(10,7,10,7);foreach(var row in startupModes)row.Padding=new Forms.Padding(10,6,10,6);
      tray=new Forms.NotifyIcon {Text="Cubic Desk · 后台运行",ContextMenuStrip=menu};string icon=Path.Combine(Store.Root,"app.ico");tray.Icon=File.Exists(icon)?new System.Drawing.Icon(icon):System.Drawing.SystemIcons.Application;tray.Visible=true;tray.DoubleClick+=delegate{OpenWindow();};
      activation=ThreadPool.RegisterWaitForSingleObject(gate.ShowEvent,delegate{if(!Exiting&&!Dispatcher.HasShutdownStarted){try{Dispatcher.BeginInvoke(new Action(OpenWindow));}catch(InvalidOperationException){}}},null,Timeout.Infinite,false);
      statusTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3)};statusTimer.Tick+=delegate{RefreshTray();};statusTimer.Start();
      SessionEnding+=delegate{Exiting=true;};
      if(!background)OpenWindow();((MainWindow)MainWindow).StartRuntime();RefreshTray();
    }
    public void OpenWindow(){if(Exiting)return;if(!MainWindow.IsVisible)MainWindow.Show();if(MainWindow.WindowState==WindowState.Minimized)MainWindow.WindowState=WindowState.Normal;MainWindow.Activate();}
    public void HideWindow(){if(!Exiting)MainWindow.Hide();}
    void RefreshTray(){if(tray==null||Exiting)return;string summary=((MainWindow)MainWindow).BackgroundSummary;deviceStatus.Text=summary;string text="Cubic Desk · "+summary;tray.Text=text.Length>63?text.Substring(0,63):text;try{var selected=LoginStartup.Mode;startupItem.Text="开机启动："+LoginStartup.Label(selected);for(int i=0;i<3;i++)startupModes[i].Checked=i==(int)selected;}catch{startupItem.Text="开机启动：读取失败";}}
    public void ExitApplication(){if(Exiting)return;Exiting=true;((MainWindow)MainWindow).StopRuntime();Shutdown();}
    protected override void OnExit(ExitEventArgs e){Exiting=true;if(statusTimer!=null)statusTimer.Stop();if(activation!=null)activation.Unregister(null);if(MainWindow is MainWindow)((MainWindow)MainWindow).StopRuntime();if(tray!=null){tray.Visible=false;var icon=tray.Icon;tray.Dispose();if(icon!=null)icon.Dispose();}base.OnExit(e);}
  }
}
