// 用途：提供电脑热点、摆件 Wi-Fi、应用管理与设备设置界面。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CubicDesk {
  public partial class MainWindow {
    void BuildNetwork(){
      var hostState=Text("正在读取 Windows 热点…",13,"#276FC0");var ssid=Input();var password=new PasswordBox();var sources=Select(new string[0]);
      var syncDevice=new CheckBox {Content="保存热点时，同时更新已连接摆件的 Wi-Fi",IsChecked=false};
      var hostPanel=Stack(Text("电脑移动热点",19,"#263C59",true),Gap(8),hostState);
      Field(hostPanel,"热点的上网来源",sources,"这里选择电脑用来上网的连接，例如以太网；本机热点是提供给摆件连接的目标。");Field(hostPanel,"热点名称",ssid);Field(hostPanel,"热点密码",password,"留空保留现有密码；新密码为 8–63 个字符。固定使用 2.4 GHz。");hostPanel.Children.Add(syncDevice);
      string originalSsid="";
      Func<Task> load=async delegate{var h=await Native.Hotspot("Status");hostState.Text=(J.Str(h,"state")=="On"?"● 已开启":"○ 已关闭")+" · "+J.Num(h,"clients")+" 台设备 · 2.4 GHz";originalSsid=J.Str(h,"ssid");ssid.Text=originalSsid;sources.Items.Clear();foreach(string s in J.Strings(h,"profiles"))sources.Items.Add(s);sources.SelectedItem=J.Str(h,"profile");syncDevice.IsChecked=online&&J.Str(J.Map(state,"wifi"),"sta_ssid")==originalSsid;};
      hostPanel.Children.Add(Gap(12));hostPanel.Children.Add(Row(Button("开启热点",async delegate{await Native.Hotspot("Start",J.D("profile",sources.SelectedItem));await load();Notice("电脑热点已开启。");},true),Button("关闭热点",async delegate{if(MessageBox.Show("关闭热点后，连接此热点的设备会断网。继续？","关闭热点",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;await Native.Hotspot("Stop");await load();Notice("电脑热点已关闭。");}),Button("刷新",async delegate{await load();Notice("热点状态已刷新。");})));
      hostPanel.Children.Add(Gap(10));hostPanel.Children.Add(Button("保存配置并开启热点",async delegate{
        string newSsid=ssid.Text.Trim(),pwd=password.Password;
        if(string.IsNullOrWhiteSpace(newSsid)||Encoding.UTF8.GetByteCount(newSsid)>32)throw new Exception("热点名称需为 1–32 个 UTF-8 字节。");
        if(string.IsNullOrEmpty(pwd)){var credentials=await Native.Hotspot("Credentials");pwd=J.Str(credentials,"password");}
        if(pwd.Length<8||pwd.Length>63)throw new Exception("密码长度需为 8–63 个字符。");
        if(MessageBox.Show("应用热点设置会短暂断开已连接的设备。"+(syncDevice.IsChecked==true?"将同时为当前摆件保存新热点信息。":""),"应用热点配置",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
        if(syncDevice.IsChecked==true){if(!online)throw new Exception("请先连接摆件。");await device.Request("/api/system/wifi/connect","POST",J.D("ssid",newSsid,"pwd",pwd));}
        await Native.Hotspot("Configure",J.D("profile",sources.SelectedItem,"ssid",newSsid,"password",pwd,"restart",true));password.Clear();await load();Notice("电脑热点配置已保存并开启。摆件重连可能需要数秒。");
      },true));
      var wifiPanel=Stack(Text("摆件 Wi-Fi",19,"#263C59",true),Gap(8),Text(online?"当前网络："+J.Str(J.Map(state,"wifi"),"sta_ssid","未连接"):"连接摆件后，可搜索它附近的 Wi-Fi。",12,"#687E98"));
      var networkName=Input();var networkPassword=new PasswordBox();var list=new ListBox {Height=145,Background=Brush("#E9FFFFFF"),Foreground=Brush("#263C59"),BorderBrush=Brush("#CCDCEB"),Margin=new Thickness(0,10,0,5)};
      list.SelectionChanged+=delegate{if(list.SelectedItem!=null)networkName.Text=Convert.ToString(((ListBoxItem)list.SelectedItem).Tag);};
      wifiPanel.Children.Add(Gap(12));wifiPanel.Children.Add(Button("扫描摆件附近的 Wi-Fi",async delegate{
        if(!online)throw new Exception("请先连接摆件。");var s=await device.Request("/api/system/wifi/scan","POST");for(int i=0;i<8&&J.Bool(J.Map(s,"wifi"),"scan_running");i++){await Task.Delay(500);s=await device.Request("/api/system/state");}
        list.Items.Clear();foreach(var n in J.List(J.Map(s,"wifi"),"scans").GroupBy(x=>J.Str(x,"ssid")).Select(g=>g.OrderByDescending(x=>J.Num(x,"rssi")).First()).OrderByDescending(x=>J.Num(x,"rssi"))){if(J.Str(n,"ssid")=="")continue;list.Items.Add(new ListBoxItem {Content=J.Str(n,"ssid")+"  ·  "+J.Num(n,"rssi")+" dBm",Tag=J.Str(n,"ssid")});}Notice(list.Items.Count+" 个可用网络。也可以手动输入名称。");
      }));wifiPanel.Children.Add(list);Field(wifiPanel,"Wi-Fi 名称",networkName);Field(wifiPanel,"Wi-Fi 密码",networkPassword);
      Func<string,string,Task> connectWifi=async (name,pwd)=>{
        if(!online)throw new Exception("请先连接摆件。");if(string.IsNullOrWhiteSpace(name))throw new Exception("请输入 Wi-Fi 名称。");await device.Request("/api/system/wifi/connect","POST",J.D("ssid",name,"pwd",pwd));
        string last="";for(int i=0;i<24;i++){await Task.Delay(1000);try{var s=await device.Request("/api/system/state");var w=J.Map(s,"wifi");last=J.Str(w,"last_error");if(J.Bool(w,"sta_connected")&&J.Str(w,"sta_ssid")==name){state=s;string ip=J.Str(w,"sta_ip");if(ip!=""&&ip!=device.Address)await Connect(ip);else await Refresh();Notice("摆件已连接 "+name+" · "+ip);return;}}catch{}}
        throw new Exception("连接信息已提交，尚未确认重连。请在“发现设备”重新搜索。"+(last==""?"":"设备提示："+last));
      };
      wifiPanel.Children.Add(Gap(12));wifiPanel.Children.Add(Button("连接此 Wi-Fi",async delegate{await connectWifi(networkName.Text.Trim(),networkPassword.Password);networkPassword.Clear();},true));wifiPanel.Children.Add(Gap(10));wifiPanel.Children.Add(Button("让摆件连接本机热点",async delegate{
        if(!online){ShowPage("发现设备");Notice("联网目标已默认为本机移动热点。扫描摆件自带热点，再点击“连接并配网”即可。");return;}await Native.Hotspot("Start",J.D("profile",sources.SelectedItem));var h=await Native.Hotspot("Credentials",J.D("profile",sources.SelectedItem));await connectWifi(J.Str(h,"ssid"),J.Str(h,"password"));
      }));
      page.Children.Add(Columns(Card(hostPanel),Card(wifiPanel),1.15));LoadHotspot(load);
    }
    async void LoadHotspot(Func<Task> load){try{await load();}catch(Exception ex){Notice("热点信息："+ex.Message,true);}}
    void BuildApps(){
      var apps=J.List(state,"installed_apps").Where(a=>J.Str(a,"kind","app")=="app").ToList();var search=Input();var list=new WrapPanel();
      Action render=delegate{list.Children.Clear();foreach(var app in apps.Where(a=>(Name(a)+J.Str(a,"id")).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0)){
        string id=J.Str(app,"id");bool running=J.Str(J.Map(state,"current_app"),"id")==id;
        var tile=Stack(Row(AppIcon(id,Name(app),64),Text(running?"正在运行":"已安装",11,running?"#276FC0":"#687E98")),Gap(13),Text(Name(app),18,"#263C59",true),Gap(5),Text(J.Str(app,"version",id),11,"#687E98"),Gap(17));
        var launch=Button(running?"重新打开":"打开应用",async delegate{await Launch(id);ShowPage("应用管理");},!running);
        tile.Children.Add(Row(launch,Button("配置",async delegate{if(id=="holocubic-smtc-music"){ShowPage("本机工具");Notice("在本机 SMTC 工具区域配置已有桥接与摆件参数。");}else{var editor=new ConfigEditor(device,id,Name(app));editor.Owner=this;await editor.LoadFiles();editor.ShowDialog();}})));
        tile.Children.Add(Gap(8));tile.Children.Add(Button("依赖与下载入口 →",delegate{ShowAppDependencies(id);return Task.FromResult(0);}));
        var card=Card(tile,18);card.Width=272;card.Margin=new Thickness(0,0,14,14);list.Children.Add(card);
      }};search.TextChanged+=delegate{render();};Field(page,"搜索应用",search);page.Children.Add(Row(Button("更新应用图标",async delegate{AppIcons.Invalidate(device.Address);var entries=J.List(state,"installed_apps");var loaded=await Task.WhenAll(entries.Select(a=>AppIcons.Fetch(J.Str(a,"id"),device.Address,true,"",J.Str(a,"version"))));ShowPage("应用管理");Notice(AppIcons.CatalogStatus+"。已显示 "+loaded.Count(a=>a!=null)+" 个应用图标，其余使用功能占位图标。");})));page.Children.Add(Gap(12));page.Children.Add(list);render();
      var services=J.List(state,"installed_apps").Where(a=>J.Str(a,"kind")=="service").ToList();
      if(services.Count>0){page.Children.Add(Gap(10));page.Children.Add(Text("设备服务",19,"#263C59",true));page.Children.Add(Gap(12));
        bool supported;Version v;supported=Version.TryParse(J.Str(J.Map(state,"firmware_update"),"current_version"),out v)&&v>=new Version(1,204);
        foreach(var service in services){string id=J.Str(service,"id");var start=Button("启动",async delegate{await device.Request("/api/system/services/start?id="+Uri.EscapeDataString(id),"POST");Notice("已提交服务启动命令。");});var stop=Button("停止",async delegate{await device.Request("/api/system/services/stop?id="+Uri.EscapeDataString(id),"POST");Notice("已提交服务停止命令。");});start.IsEnabled=stop.IsEnabled=supported;page.Children.Add(Card(Row(AppIcon(id,Name(service),36),Text(Name(service),15,"#263C59",true),start,stop,Button("配置",async delegate{var e=new ConfigEditor(device,id,Name(service));e.Owner=this;await e.LoadFiles();e.ShowDialog();})),14));}
        if(!supported)page.Children.Add(Text("当前固件未提供服务启停接口；此功能需要固件 1.204 或以上。",12,"#687E98"));
      }
    }
    async void BuildSettings(){
      var container=new StackPanel();page.Children.Add(container);container.Children.Add(Text("正在读取摆件设置…",13,"#687E98"));
      try{
        var config=J.Parse(await device.Text(device.FileRoute("/sd/apps/settings.json")));var live=new Dictionary<string,object>();try{live=await device.Request("/display/api/settings");}catch{}if(current!="设备设置")return;container.Children.Clear();
        var display=Stack(AppHeading("settings","显示与偏好",19,36));
        var brightness=new Slider {Minimum=1,Maximum=100,Value=J.Num(live,"brightness",J.Num(config,"brightness",80)),TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,8,0,8)};var value=Text(((int)brightness.Value)+"%",14,"#276FC0");brightness.ValueChanged+=delegate{value.Text=((int)brightness.Value)+"%";};Field(display,"屏幕亮度",Stack(brightness,value));
        var timezone=Input(J.Str(config,"timezone","CST-8"));Field(display,"时区",timezone,"中国标准时间：CST-8；UTC：UTC0。");
var languageIds=new List<string>{"zh-CN","zh-TW","en","ja"};var languageNames=new List<string>{"简体中文","繁體中文","English","日本語"};
string existingLanguage=J.Str(config,"language","zh-CN");int languageIndex=languageIds.IndexOf(existingLanguage);
if(languageIndex<0){languageIds.Add(existingLanguage);languageNames.Add(existingLanguage);languageIndex=languageIds.Count-1;}
var language=Select(languageNames);language.SelectedIndex=languageIndex;Field(display,"摆件界面语言",language,"未识别的现有语言会保留原值；选择其他语言后才会替换。");
        var weather=Input(J.Str(config,"weather_address",""));Field(display,"天气位置",weather,"沿用摆件天气应用支持的城市或地址格式。");
        var startup=Stack(Text("启动与连接",19,"#263C59",true));
var auto=new ComboBox();auto.Items.Add("不自动打开应用");var ids=new List<string>{""};
var startupApps=J.List(state,"apps").Concat(J.List(state,"installed_apps").Where(a=>J.Str(a,"kind","app")=="app")).GroupBy(a=>J.Str(a,"id"),StringComparer.OrdinalIgnoreCase).Select(g=>g.First());
foreach(var a in startupApps){string id=J.Str(a,"id");if(id=="")continue;auto.Items.Add(new ComboBoxItem {Content=Row(AppIcon(id,Name(a),24),Text(Name(a)+"  ·  "+id,12))});ids.Add(id);}
string existingStartup=J.Str(config,"autostart_app_id");if(existingStartup!=""&&!ids.Contains(existingStartup)){ids.Add(existingStartup);auto.Items.Add("保留现有应用 · "+existingStartup);}
        int index=ids.IndexOf(J.Str(config,"autostart_app_id"));auto.SelectedIndex=J.Bool(config,"autostart_enabled")&&index>=0?index:0;Field(startup,"开机自动打开",auto);
        var ap=new CheckBox {Content="联网后仍保留摆件配网热点",IsChecked=J.Get(config,"ap_enabled")==null||J.Bool(config,"ap_enabled")};startup.Children.Add(Gap(12));startup.Children.Add(ap);startup.Children.Add(Text("保留热点便于网络变化时重新配置。",11,"#687E98"));
Func<Dictionary<string,object>> readForm=delegate{return J.D("brightness",(int)brightness.Value,"timezone",timezone.Text.Trim(),"language",languageIds[language.SelectedIndex],"weather_address",weather.Text.Trim(),"autostart_enabled",auto.SelectedIndex>0,"autostart_app_id",ids[auto.SelectedIndex],"ap_enabled",ap.IsChecked==true);};
var initialForm=readForm();
startup.Children.Add(Gap(20));startup.Children.Add(Button("保存到摆件",async delegate{
          var edited=readForm();bool brightnessChanged=!object.Equals(initialForm["brightness"],edited["brightness"]);
          var latest=J.Parse(await device.Text(device.FileRoute("/sd/apps/settings.json")));DeviceSettingsMerge.Apply(latest,initialForm,edited);
          await device.BackupAndPut("/sd/apps/settings.json",Encoding.UTF8.GetBytes(J.Stringify(latest)));if(brightnessChanged)await device.Request("/display/api/settings?brightness="+(int)brightness.Value,"POST");initialForm=edited;Notice("设置已保存；部分启动设置会在下次启动时生效。");
        },true));startup.Children.Add(Gap(10));startup.Children.Add(Text("每次保存会备份原始配置，并保留未编辑的字段。",11,"#687E98"));container.Children.Add(Columns(Card(display),Card(startup),1.15));
        var firmware=J.Map(state,"firmware_update");var fwStatus=Text("当前版本 v"+J.Str(firmware,"current_version","--"),14,"#276FC0");var firmwarePanel=Stack(Text("系统固件",18,"#263C59",true),Gap(10),fwStatus,Gap(15));bool canInstall=false;
        var install=Button("安装更新",async delegate{if(!canInstall)throw new Exception("请先检查并确认有可用更新。");if(MessageBox.Show("设备将下载并安装固件，完成后自动重启。请保持供电。继续？","安装固件更新",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;await device.Request("/api/system/firmware/update","POST");Notice("固件安装已开始，请保持摆件供电并等待重启。");});install.IsEnabled=false;
        firmwarePanel.Children.Add(Row(Button("检查更新",async delegate{var f=await device.Request("/api/system/firmware/check");var update=J.Map(f,"firmware_update");canInstall=J.Bool(update,"update_available");install.IsEnabled=canInstall;fwStatus.Text=canInstall?"发现版本 v"+J.Str(update,"latest_version"):"已是最新版本 · v"+J.Str(update,"current_version");Notice("固件检查完成。");}),install));container.Children.Add(Card(firmwarePanel));
      }catch(Exception ex){container.Children.Clear();container.Children.Add(Text("读取失败："+ex.Message,13,"#BA5260"));}
    }
  }
}
