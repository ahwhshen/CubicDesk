// 用途：组织主窗口导航、设备状态轮询与总览界面。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CubicDesk {
  public partial class MainWindow : Window {
    DeviceClient device;
    Dictionary<string,object> state=new Dictionary<string,object>();
    readonly MusicBridge bridge=new MusicBridge();
    StackPanel page;ScrollViewer pageScroll;TextBlock pageTitle,pageHint,sideStatus,sideAddress,banner,headerStatus,homeCurrent;
    Border bannerBox;readonly Dictionary<string,Button> nav=new Dictionary<string,Button>();
    string current="总览";bool online,busy,polling,closing,runtimeStarted;
    DateTime nextReconnect=DateTime.MinValue;
    DispatcherTimer timer;CancellationTokenSource discoveryCancel;
    Border homeIconHost;string homeIconId="";
    TextBlock dependencyStatus,lyricProviderStatus;ComboBox sessionPicker;
    public MainWindow() {
      Style=(Style)FindResource(typeof(Window));
      Title="Cubic Desk · 摆件管理";Width=Math.Min(1380,SystemParameters.WorkArea.Width-24);Height=Math.Min(960,SystemParameters.WorkArea.Height-24);MinWidth=1100;MinHeight=760;WindowStartupLocation=WindowStartupLocation.CenterScreen;
      try{Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(Path.Combine(Store.Root,"app.ico")));}catch{}
      BuildShell();
      Loaded+=delegate{StartRuntime();};
      Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(ResidentApplication.Host!=null&&!ResidentApplication.Host.Exiting){e.Cancel=true;Hide();return;}StopRuntime();};
    }
    public async void StartRuntime(){
      if(runtimeStarted||closing)return;runtimeStarted=true;
        string address=J.Str(Store.Config,"lastAddress");
        if(J.Bool(Store.Config,"bridgeAutoStart")&&address!=""){try{bridge.Start(address,(int)J.Num(Store.Config,"bridgePort",17865),(int)J.Num(Store.Config,"bridgeInterval",800),J.Str(Store.Config,"bridgeSource"));}catch(Exception ex){Notice(ex.Message,true);}}
        if(!string.IsNullOrEmpty(address))await Run(async delegate{await Connect(address);},"正在连接已记住的摆件…");
        else ShowPage("发现设备");
        if(closing)return;
        timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3)};timer.Tick+=async delegate {
          if(closing)return;UpdateBridgeStatus();if(busy||polling)return;
          if(device==null){string remembered=J.Str(Store.Config,"lastAddress");if(remembered==""||DateTime.UtcNow<nextReconnect)return;nextReconnect=DateTime.UtcNow.AddSeconds(15);polling=true;try{await Connect(remembered,false);}catch{}finally{polling=false;}return;}
          polling=true;var bound=device;try{var next=await bound.Request("/api/system/state");if(!DeviceClient.IsDevice(next))throw new Exception("设备身份未确认。");if(device==bound&&!busy&&!closing){state=next;online=true;UpdateConnection();}}catch{if(device==bound&&!busy&&!closing){online=false;UpdateConnection();}}finally{polling=false;}
        };timer.Start();
    }
    public void StopRuntime(){if(closing)return;closing=true;if(timer!=null)timer.Stop();if(discoveryCancel!=null)discoveryCancel.Cancel();bridge.Stop();if(device!=null)device.Dispose();}
    static Brush Brush(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
    TextBlock Text(string value,double size=13,string color="#263C59",bool bold=false){return new TextBlock {Text=value,FontSize=size,Foreground=Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap};}
    StackPanel Stack(params UIElement[] children){var p=new StackPanel();foreach(var c in children)p.Children.Add(c);return p;}
    StackPanel Row(params UIElement[] children){var p=new StackPanel {Orientation=Orientation.Horizontal};foreach(var c in children){var f=c as FrameworkElement;if(f!=null)f.Margin=new Thickness(0,0,10,0);p.Children.Add(c);}return p;}
    Border Card(UIElement child,double padding=22){return new GlassSurface {CornerRadius=new CornerRadius(20),Padding=new Thickness(padding),Margin=new Thickness(0,0,0,20),Child=child};}
    Button Button(string label,Func<Task> action=null,bool primary=false){var b=new Button {Content=label};if(primary)b.Style=(Style)FindResource("Primary");if(action!=null)b.Click+=async delegate{await Run(action,"正在执行…",b);};return b;}
    TextBox Input(string value=""){return new TextBox {Text=value,Margin=new Thickness(0,0,0,6)};}
    ComboBox Select(IEnumerable<string> options,string selected=""){var box=new ComboBox {Margin=new Thickness(0,0,0,6)};foreach(string o in options)box.Items.Add(o);if(box.Items.Contains(selected))box.SelectedItem=selected;else if(box.Items.Count>0)box.SelectedIndex=0;return box;}
    void Field(Panel p,string label,UIElement control,string hint=""){var t=Text(label,12,"#516B88",true);t.Margin=new Thickness(0,12,0,7);p.Children.Add(t);p.Children.Add(control);if(hint!=""){var h=Text(hint,11,"#687E98");h.Margin=new Thickness(0,2,0,6);p.Children.Add(h);}}
    Grid Columns(UIElement left,UIElement right,double leftWeight=1.4){var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(leftWeight,GridUnitType.Star)});g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(22)});g.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(right,2);g.Children.Add(left);g.Children.Add(right);return g;}
    void BuildShell(){
      var root=new Grid();root.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(224)});root.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});Content=root;
      var side=new DockPanel {Background=Brushes.Transparent,Margin=new Thickness(0)};root.Children.Add(new GlassSurface {Child=side,Margin=new Thickness(10,8,0,16),CornerRadius=new CornerRadius(23),HaloSize=9});
      var brand=Stack(Text("◇  CUBIC DESK",19,"#276FC0",true),Text("让桌面的小世界，随心而动。",11,"#687E98"));brand.Margin=new Thickness(20,24,14,32);DockPanel.SetDock(brand,Dock.Top);side.Children.Add(brand);
      var foot=Stack(Text("当前摆件",11,"#687E98"),sideStatus=Text("○  尚未连接",14,"#687E98",true),sideAddress=Text("在局域网中发现你的摆件",11,"#687E98"),Text("Cubic Desk  /  1.5.1",10,"#7890AA"));foot.Margin=new Thickness(22,0,18,24);foreach(FrameworkElement f in foot.Children)f.Margin=new Thickness(0,6,0,0);DockPanel.SetDock(foot,Dock.Bottom);side.Children.Add(foot);
      var navigation=new StackPanel {Margin=new Thickness(12,0,12,0)};DockPanel.SetDock(navigation,Dock.Top);side.Children.Add(new ScrollViewer {Content=navigation,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
      string[] names={"总览","发现设备","网络与热点","应用管理","应用商店","资源管理","设备设置","依赖安装","本机工具","后台与启动"};string[] symbols={"◈","⌕","⌁","▦","◫","▧","☷","⊞","⌘","◌"};
      for(int i=0;i<names.Length;i++){string name=names[i];var b=new Button {Content=symbols[i]+"    "+name,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(18,12,12,12),Margin=new Thickness(0,0,0,7),Background=Brush("#75FFFFFF"),BorderThickness=new Thickness(0)};b.Click+=delegate{ShowPage(name);};nav[name]=b;navigation.Children.Add(b);}
      var main=new Grid {Margin=new Thickness(24,14,24,16)};Grid.SetColumn(main,1);root.Children.Add(main);main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
      var header=new DockPanel {Margin=new Thickness(0,0,0,22)};headerStatus=Text("○  未连接",12,"#687E98");headerStatus.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(headerStatus,Dock.Right);header.Children.Add(headerStatus);header.Children.Add(Stack(pageTitle=Text("总览",27,"#263C59",true),pageHint=Text("设备、连接与每一种可能，都在这里。",12,"#687E98")));main.Children.Add(header);
      banner=Text("",12);bannerBox=new GlassSurface {Child=banner,HaloSize=5,Background=Brush("#DDF2F7FF"),CornerRadius=new CornerRadius(12),Padding=new Thickness(14,10,14,10),Margin=new Thickness(8,0,8,14),Visibility=Visibility.Collapsed};Grid.SetRow(bannerBox,1);main.Children.Add(bannerBox);
      page=new StackPanel {Margin=new Thickness(8,9,8,6)};pageScroll=new ScrollViewer {Content=page};Grid.SetRow(pageScroll,2);main.Children.Add(pageScroll);
      var footer=Text("本地直连  ·  设置保存在摆件  ·  热点密码不会写入日志",10,"#718AA4");footer.Margin=new Thickness(8,10,0,0);Grid.SetRow(footer,3);main.Children.Add(footer);ShowPage("总览");GlassWindow.Attach(this,root);
    }
    void ShowPage(string name){
      current=name;pageTitle.Text=name;page.Children.Clear();dependencyStatus=null;sessionPicker=null;lyricProviderStatus=null;homeCurrent=null;homeIconHost=null;
      foreach(var item in nav){item.Value.Background=Brush(item.Key==name?"#EFFFFFFF":"#75FFFFFF");item.Value.Foreground=Brush(item.Key==name?"#276FC0":"#5C7592");}
      var hints=new Dictionary<string,string>{{"总览","你的桌面伙伴，已准备就绪。"},{"发现设备","查找摆件自带热点，连接后为摆件指定目标 Wi-Fi。"},{"网络与热点","共享电脑网络，让摆件轻松连接。"},{"应用管理","直接打开、切换和配置摆件上的应用。"},{"应用商店","安装设备应用前，查看作者要求的电脑端依赖。"},{"资源管理","管理摆件 SD 卡中的图片、音乐与游戏。"},{"设备设置","显示、开机行为和系统偏好，一处设置。"},{"依赖安装","整理依赖、打开下载入口、手动确认准备状态。"},{"本机工具","已有的本机桥接与转码工具。"},{"后台与启动","关闭窗口继续运行，登录 Windows 后自动启动。"}};pageHint.Text=hints[name];
      if(name=="发现设备")BuildDiscovery();else if(name=="网络与热点")BuildNetwork();else if(name=="依赖安装")BuildDependencies();else if(name=="本机工具")BuildLocalTools();else if(name=="后台与启动")BuildBackground();else if(name=="总览")BuildHome();else if(!online)Offline();else if(name=="应用商店")BuildAppStore();else if(name=="应用管理")BuildApps();else if(name=="设备设置")BuildSettings();else if(name=="资源管理")BuildMedia();
      if(pageScroll!=null)pageScroll.ScrollToTop();
    }
    void Offline(){var b=Button("发现并连接摆件",delegate{ShowPage("发现设备");return Task.FromResult(0);},true);page.Children.Add(Card(Stack(Text("先连接你的摆件",22,"#263C59",true),Text("扫描 clocteck-cubic 前缀的摆件热点，再为它指定目标 Wi-Fi；已配网的摆件可按地址连接。",13,"#687E98"),Gap(20),b)));}
    FrameworkElement Gap(double height){return new Border {Height=height};}
    void Notice(string message,bool error=false){banner.Text=message;banner.Foreground=Brush(error?"#BA5260":"#356B9D");bannerBox.Background=Brush(error?"#FFF0F2":"#DDF2F7FF");bannerBox.Visibility=Visibility.Visible;}
    async Task Run(Func<Task> action,string message,Button button=null){if(busy){Notice("请等待当前操作完成。");return;}busy=true;if(button!=null)button.IsEnabled=false;Notice(message);try{await action();if(banner.Text==message)bannerBox.Visibility=Visibility.Collapsed;}catch(OperationCanceledException){Notice("已取消搜索。");}catch(Exception ex){Notice(ex.Message,true);Store.Log("操作失败："+ex.Message);}finally{busy=false;if(button!=null)button.IsEnabled=true;}}
    async Task Connect(string address,bool navigate=true){
      address=DeviceClient.Normalize(address);if(address.EndsWith(".local",StringComparison.OrdinalIgnoreCase)){var resolved=await System.Net.Dns.GetHostAddressesAsync(address);var ip=resolved.FirstOrDefault(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&DeviceClient.Private(a));if(ip==null)throw new Exception("此设备名称没有解析到局域网 IPv4 地址。");address=ip.ToString();}
      var next=new DeviceClient(address);Dictionary<string,object> s;
      try{s=await next.Request("/api/system/state");if(!DeviceClient.IsDevice(s))throw new Exception("这个地址没有返回兼容的 Cubic 摆件信息。");}catch{next.Dispose();throw;}
      if(closing){next.Dispose();return;}
      if(bridge.Running&&bridge.DeviceAddress!=next.Address)bridge.Stop();if(device!=null)device.Dispose();device=next;state=s;online=true;Store.Config["lastAddress"]=device.Address;Store.Save();UpdateConnection();Notice("已连接摆件 · "+device.Address);if(navigate||current=="总览")ShowPage("总览");
    }
    void UpdateConnection(){sideStatus.Text=online?"●  Cubic 已连接":"○  设备离线";sideStatus.Foreground=Brush(online?"#276FC0":"#BA5260");sideAddress.Text=device==null?"在局域网中发现你的摆件":device.Address;headerStatus.Text=online?"●  本地连接正常":"○  等待连接";headerStatus.Foreground=Brush(online?"#276FC0":"#687E98");if(homeCurrent!=null)homeCurrent.Text=online?Name(J.Map(state,"current_app")):"设备离线";if(homeIconHost!=null&&online){string id=J.Str(J.Map(state,"current_app"),"id");if(id!=homeIconId){homeIconId=id;homeIconHost.Child=AppIcon(id,Name(J.Map(state,"current_app")),48);}}}
    new string Name(Dictionary<string,object> app){string id=J.Str(app,"id");var names=new Dictionary<string,string>{{"holocubic-smtc-music","Windows 音乐"},{"holo_pc_monitor","电脑性能监控"},{"NixieClock","全息时钟"},{"FluidPendant","流体时钟"},{"Spectrum","频谱时钟"},{"weather","天气"},{"mp3_player","本地音乐"},{"photos","相册"},{"videos","动图播放器"},{"mjpeg_player","视频播放器"},{"holo-retro-go","复古游戏"},{"wifi_guide","配网指南"},{"settings","设备设置"},{"BTC","股票行情"},{"ebook","电子书"},{"launcher","启动器"},{"devtools","开发工具"},{"display_schedule","息屏与闹钟"},{"hidpad","HID 按键"}};return names.ContainsKey(id)?names[id]:J.Str(app,"name",id);}
    async Task Refresh(){state=await device.Request("/api/system/state");online=true;UpdateConnection();}
    async Task Launch(string id){await device.Request("/api/system/launch?id="+Uri.EscapeDataString(id),"POST");await Task.Delay(450);await Refresh();Notice("已打开 "+Name(J.Map(state,"current_app")));}
    void BuildHome(){
      if(!online){Offline();return;}
      var w=J.Map(state,"wifi");var fw=J.Map(state,"firmware_update");
      var heroText=Stack(Text("CONNECTED / 已连接",11,"#276FC0",true),Gap(18),Text("小小摆件，\n大有可玩。",32,"#263C59",true),Gap(14),Text("从时钟到音乐，让它成为你的桌面伙伴。",12,"#5C7592"),Gap(20),Row(Button("应用管理  →",delegate{ShowPage("应用管理");return Task.FromResult(0);},true),Button("连接设置",delegate{ShowPage("网络与热点");return Task.FromResult(0);})));
      var illustration=(UIElement)XamlReader.Parse("<Viewbox xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Height='235'><Canvas Width='270' Height='235'><Ellipse Width='215' Height='40' Fill='#193D85CF' Canvas.Left='26' Canvas.Top='183'/><Path Data='M 42,64 L 153,28 L 241,79 L 130,119 Z' Fill='#DDF7FBFF' Stroke='#8FBCE8' StrokeThickness='1.4'/><Path Data='M 42,64 L 130,119 L 130,192 L 42,139 Z' Fill='#C8DCEEFF' Stroke='#8FBCE8' StrokeThickness='1.4'/><Path Data='M 130,119 L 241,79 L 241,152 L 130,192 Z' Fill='#ADCCE5FA' Stroke='#8FBCE8' StrokeThickness='1.4'/><Path Data='M 72,70 L 151,46 L 213,82 L 134,107 Z' Fill='#EFFFFFFF' Stroke='#276FC0'/><TextBlock Text='CUBIC' Foreground='#276FC0' FontSize='21' FontFamily='Segoe UI' FontWeight='Bold' Canvas.Left='112' Canvas.Top='65'/><Path Data='M 42,148 L 130,201 L 241,161 L 241,179 L 131,220 L 42,167 Z' Fill='#DAEAF6FF' Stroke='#B1C9E2'/><Path Data='M 134,204 L 155,197' Stroke='#276FC0' StrokeThickness='3'/><Ellipse Width='5' Height='5' Fill='#276FC0' Canvas.Left='225' Canvas.Top='44'/><Ellipse Width='4' Height='4' Fill='#B7D5F3' Canvas.Left='30' Canvas.Top='30'/></Canvas></Viewbox>");
      var hero=Card(Columns(heroText,illustration,1.6),28);hero.Background=Brush("#D6E8F4FF");page.Children.Add(hero);
      var metrics=new UniformGridFix(3);metrics.Children.Add(Stat("设备地址",device.Address,"局域网 HTTP 直连"));metrics.Children.Add(Stat("连接的 Wi-Fi",J.Str(w,"sta_ssid","未连接"),J.Str(w,"sta_rssi","--")+" dBm"));metrics.Children.Add(Stat("设备固件","v"+J.Str(fw,"current_version","--"),J.List(state,"apps").Count+" 个已安装应用"));page.Children.Add(metrics);
      var active=Stack(Text("正在运行",11,"#687E98",true),Gap(12),Row(homeIconHost=new Border {Child=AppIcon(J.Str(J.Map(state,"current_app"),"id"),Name(J.Map(state,"current_app")),48)},homeCurrent=Text(Name(J.Map(state,"current_app")),23,"#263C59",true)),Gap(20),Row(Button("返回启动器",async delegate{await device.Request("/api/system/exit","POST");await Refresh();Notice("已返回启动器。");}),Button("刷新状态",async delegate{await Refresh();ShowPage("总览");Notice("设备状态已刷新。");})));
      var musicButton=Button("Windows 音乐桥接",delegate{ShowPage("本机工具");return Task.FromResult(0);});musicButton.Content=Row(AppIcon("holocubic-smtc-music","Windows 音乐",24),Text("Windows 音乐桥接",12));
      var quick=Stack(Text("常用入口",16,"#263C59",true),Gap(14),musicButton,Gap(10),Button("▧   上传图片与音乐",delegate{ShowPage("资源管理");return Task.FromResult(0);}));page.Children.Add(Columns(Card(active),Card(quick)));
    }
    UIElement Stat(string label,string value,string hint){var s=Stack(Text(label,11,"#687E98"),Gap(9),Text(value,18,"#263C59",true),Gap(7),Text(hint,11,"#748BA4"));var c=Card(s,18);c.Margin=new Thickness(0,0,12,16);return c;}

  }
  public sealed class UniformGridFix : System.Windows.Controls.Primitives.UniformGrid { public UniformGridFix(int columns){Columns=columns;} }
}
