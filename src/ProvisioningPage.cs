using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CubicDesk {
  public partial class MainWindow {
    void BuildDiscovery(){
      var target=new ComboBox {IsEditable=true,MinWidth=200,Margin=new Thickness(0,0,0,6)};target.Text=J.Str(Store.Config,"provisionSsid");
      var targetPassword=new PasswordBox();var apPassword=new PasswordBox();var destinationMode=Select(new[]{"本机移动热点（推荐）","其他 Wi-Fi"});
      var computerName=Text("正在读取本机热点名称…",14,"#276FC0",true);var computerSettings=Stack(Gap(12),computerName,Gap(8),Text("连接时自动使用电脑热点的名称和密码，无需手动输入；会自动开启热点。",12,"#687E98"),Gap(10),Button("修改本机热点设置",delegate{ShowPage("网络与热点");return Task.FromResult(0);}));
      var otherSettings=new StackPanel();Field(otherSettings,"目标 Wi-Fi 名称",target,"请使用摆件支持的 2.4 GHz 网络；需要网页登录的校园网无法直接认证。");Field(otherSettings,"目标 Wi-Fi 密码",targetPassword,"开放网络可留空。这里填写的是目标网络密码。");otherSettings.Visibility=Visibility.Collapsed;
      destinationMode.SelectionChanged+=delegate{bool computer=destinationMode.SelectedIndex==0;computerSettings.Visibility=computer?Visibility.Visible:Visibility.Collapsed;otherSettings.Visibility=computer?Visibility.Collapsed:Visibility.Visible;};
      var destinations=Stack(Text("2  指定摆件要连接的网络",19,"#263C59",true),Gap(8),Text("本机热点可直接指定，不需要从 Wi-Fi 扫描列表里寻找。",12,"#687E98"));Field(destinations,"摆件的联网目标",destinationMode);destinations.Children.Add(computerSettings);destinations.Children.Add(otherSettings);
      var results=new StackPanel();var status=Text("点击扫描，查找名称以 clocteck-cubic 开头的 Wi-Fi。",13,"#5C7592");
      var cancel=Button("取消");cancel.Click+=delegate{if(discoveryCancel!=null)discoveryCancel.Cancel();};cancel.IsEnabled=false;
      var scan=Button("扫描摆件热点",async delegate{
        var source=new CancellationTokenSource();discoveryCancel=source;cancel.IsEnabled=true;results.Children.Clear();status.Text="正在扫描附近的摆件热点…";
        try{var networks=await WifiRadio.Scan(source.Token);string enteredTarget=target.Text;target.Items.Clear();foreach(var n in networks.Where(n=>!WifiRadio.IsDeviceHotspot(n.Ssid)).GroupBy(n=>n.Ssid,StringComparer.Ordinal).Select(g=>g.First()))target.Items.Add(n.Ssid);target.Text=enteredTarget;
          if(string.IsNullOrEmpty(target.Text)){var currentNetwork=networks.FirstOrDefault(n=>n.Connected&&!WifiRadio.IsDeviceHotspot(n.Ssid));if(currentNetwork!=null)target.Text=currentNetwork.Ssid;}
          var devices=networks.Where(n=>WifiRadio.IsDeviceHotspot(n.Ssid)).GroupBy(n=>n.Ssid,StringComparer.Ordinal).Select(g=>g.First()).ToList();
          foreach(var network in devices){var found=network;var button=Button("连接并配网",async delegate{
              var operation=new CancellationTokenSource();discoveryCancel=operation;cancel.IsEnabled=true;string apKey=apPassword.Password,networkKey=targetPassword.Password;apPassword.Clear();targetPassword.Clear();
              try{await ProvisionHotspot(found,apKey,target.Text,networkKey,destinationMode.SelectedIndex==0,status,operation.Token);}finally{if(discoveryCancel==operation)discoveryCancel=null;operation.Dispose();cancel.IsEnabled=false;}
            },true);
            results.Children.Add(Card(Stack(Row(Text(found.Ssid,17,"#276FC0",true),Text("信号 "+found.Signal+"% · "+(found.Secured?"需要热点密码":"开放热点"),12,"#687E98")),Gap(10),button),16));
          }
          status.Text=devices.Count==0?"未发现摆件热点。请让摆件进入 Wi-Fi setup 状态，靠近电脑后重试。":"发现 "+devices.Count+" 个摆件热点。填写右侧目标网络后，点击“连接并配网”。";Notice(status.Text,devices.Count==0);
        }finally{if(discoveryCancel==source)discoveryCancel=null;source.Dispose();cancel.IsEnabled=false;}
      },true);
      var search=Stack(Text("1  查找摆件自带热点",19,"#263C59",true),Gap(8),status,Gap(12),Row(scan,cancel));Field(search,"摆件热点密码（如有）",apPassword,"仅当扫描结果显示需要密码时填写；这与右侧目标 Wi-Fi 密码不同。");search.Children.Add(Gap(8));search.Children.Add(Text("连接时电脑会临时切换 Wi-Fi；配网后恢复原网络。仅扫描不会切换网络。",12,"#687E98"));
      page.Children.Add(Columns(Card(search),Card(destinations),1));page.Children.Add(results);
      var address=Input(J.Str(Store.Config,"lastAddress","192.168.137.7"));var advanced=Stack(Text("已完成配网的摆件，也可以通过局域网地址管理。",12,"#687E98"));Field(advanced,"摆件 IP 地址",address,"支持局域网 IPv4 或 clocteck-cubic.local");advanced.Children.Add(Button("按地址连接",async delegate{await Connect(address.Text);},true));
      page.Children.Add(new Expander {Header="已联网摆件：按地址连接",Content=Card(advanced),IsExpanded=false,Margin=new Thickness(4,10,4,12)});
      ReadProvisioningHotspotName(computerName);
    }
    async void ReadProvisioningHotspotName(TextBlock label){try{var host=await Native.Hotspot("Status");label.Text="热点名称："+J.Str(host,"ssid","尚未设置")+" · "+(J.Str(host,"state")=="On"?"已开启":"当前未开启");}catch(Exception ex){label.Text="本机热点信息暂未读取："+ex.Message;}}
    static bool MatchesProvisionedDevice(Dictionary<string,object> data,string apSsid,string target){var wifi=J.Map(data,"wifi");System.Net.IPAddress address;return DeviceClient.IsDevice(data)&&J.Str(wifi,"ap_ssid")==apSsid&&J.Bool(wifi,"sta_connected")&&J.Str(wifi,"sta_ssid")==target&&System.Net.IPAddress.TryParse(J.Str(wifi,"sta_ip"),out address)&&DeviceClient.Private(address)&&!System.Net.IPAddress.IsLoopback(address);}
    async Task<string> FindSetupAddress(WifiNetwork network,CancellationToken token){
      for(int attempt=0;attempt<10;attempt++){
        token.ThrowIfCancellationRequested();var addresses=WifiRadio.Gateways(network.InterfaceId).Concat(new[]{"192.168.18.1"}).Distinct().ToList();
        foreach(string address in addresses){try{using(var client=new DeviceClient(address,2)){var data=await client.Request("/api/system/state","GET",null,token);if(DeviceClient.IsDevice(data)&&J.Str(J.Map(data,"wifi"),"ap_ssid")==network.Ssid)return address;}}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}}
        await Task.Delay(500,token);
      }throw new Exception("已连接热点，但尚未确认摆件配网页面。请检查屏幕上的 SETUP 地址后，使用下方的按地址连接。");
    }
    async Task ProvisionHotspot(WifiNetwork network,string apPassword,string target,string targetPassword,bool computerHotspot,TextBlock status,CancellationToken token){
      if(!WifiRadio.IsDeviceHotspot(network.Ssid))throw new Exception("请选择 clocteck-cubic 前缀的摆件热点。");
      string computerProfile="";if(computerHotspot){Notice("正在准备电脑移动热点…");var host=await Native.Hotspot("Start");computerProfile=J.Str(host,"profile");var credentials=await Native.Hotspot("Credentials",J.D("profile",computerProfile));target=J.Str(credentials,"ssid");targetPassword=J.Str(credentials,"password");}
      if(string.IsNullOrWhiteSpace(target)||Encoding.UTF8.GetByteCount(target)>32)throw new Exception("目标 Wi-Fi 名称需要为 1–32 个 UTF-8 字节。");if(WifiRadio.IsDeviceHotspot(target))throw new Exception("目标网络不能是摆件自己的配网热点，请填写要让摆件接入的 Wi-Fi。");if(targetPassword.Length>63)throw new Exception("目标 Wi-Fi 密码不能超过 63 个字符。");
      var previous=WifiRadio.Current(network.InterfaceId);string stationAddress="",failure="",restoreFailure="";bool submitted=false,acknowledged=false,confirmed=false;
      try{
        token.ThrowIfCancellationRequested();status.Text="正在临时连接摆件热点 "+network.Ssid+"…";Notice(status.Text);await WifiRadio.Join(network,apPassword,token);
        status.Text="正在读取摆件配网信息…";Notice(status.Text);string setupAddress=await FindSetupAddress(network,token);
        using(var client=new DeviceClient(setupAddress,2)){
          var before=await client.Request("/api/system/state","GET",null,token);var wifi=J.Map(before,"wifi");string backup=Path.Combine(Store.Data,"backups",setupAddress,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-wifi-before.json");Directory.CreateDirectory(Path.GetDirectoryName(backup));File.WriteAllText(backup,J.Stringify(J.D("ap_ssid",J.Str(wifi,"ap_ssid"),"sta_ssid",J.Str(wifi,"sta_ssid"),"sta_connected",J.Bool(wifi,"sta_connected"),"sta_ip",J.Str(wifi,"sta_ip"))),new UTF8Encoding(false));
          token.ThrowIfCancellationRequested();status.Text="正在让摆件连接目标网络 "+target+"…";Notice(status.Text);
          // Dispatch only the Wi-Fi command; device-side settings and unknown fields remain owned by its native API.
          submitted=true;try{var response=await client.Request("/api/system/wifi/connect","POST",J.D("ssid",target,"pwd",targetPassword),token);acknowledged=true;if(MatchesProvisionedDevice(response,network.Ssid,target)){confirmed=true;stationAddress=J.Str(J.Map(response,"wifi"),"sta_ip");}}catch(System.Net.Http.HttpRequestException){}catch(TaskCanceledException){if(token.IsCancellationRequested)throw;}
          for(int i=0;i<18&&!confirmed;i++){
            await Task.Delay(1000,token);Dictionary<string,object> next=null;try{next=await client.Request("/api/system/state","GET",null,token);}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}
            if(next==null)continue;if(!DeviceClient.IsDevice(next))throw new Exception("配网地址已不再返回兼容的摆件信息。");var w=J.Map(next,"wifi");string error=J.Str(w,"last_error");if(MatchesProvisionedDevice(next,network.Ssid,target)){confirmed=true;stationAddress=J.Str(w,"sta_ip");break;}if(error!=""&&!J.Bool(w,"sta_connecting"))throw new Exception("摆件配网失败："+error);
          }
        }
      }catch(OperationCanceledException){failure=submitted?"已停止等待；连接命令可能已到达摆件，取消不会撤回已发送的信息。":"配网已取消。";}catch(Exception ex){failure=ex.Message;}
      // C# 5 cannot await inside finally; both catch branches retain the error and reach restoration here.
      {
        status.Text="正在恢复电脑原来的网络…";Notice(status.Text);try{await WifiRadio.Restore(previous);if(computerHotspot)await Native.Hotspot("Start",J.D("profile",computerProfile));}catch(Exception ex){restoreFailure=ex.Message;}
      }
      if(failure!="")throw new Exception(failure+(restoreFailure==""?"":"；"+restoreFailure));if(restoreFailure!="")throw new Exception((confirmed?"摆件已报告连接目标网络。":acknowledged?"摆件已接收连接命令，但尚未确认联网。":"连接请求未收到确认。")+"电脑网络恢复失败："+restoreFailure);
      token.ThrowIfCancellationRequested();status.Text="电脑网络已恢复，正在确认摆件的新地址…";Notice(status.Text);
      if(stationAddress!=""){
        bool reachable=false;try{using(var client=new DeviceClient(stationAddress,3)){var data=await client.Request("/api/system/state","GET",null,token);reachable=MatchesProvisionedDevice(data,network.Ssid,target);}}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}
        if(reachable){await Connect(stationAddress);Store.Config["provisionSsid"]=target;Store.Save();Notice("配网完成：摆件已连接 "+target+" · "+stationAddress);return;}
      }
      using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
        deadline.CancelAfter(40000);try{var found=await new Discovery().Scan(delegate{},deadline.Token,true);var match=found.FirstOrDefault(d=>MatchesProvisionedDevice(d,network.Ssid,target));if(match!=null){await Connect(J.Str(match,"address"));Store.Config["provisionSsid"]=target;Store.Save();Notice("配网完成：摆件已连接 "+target+" · "+device.Address);return;}}catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
      }
      if(confirmed){Store.Config["provisionSsid"]=target;Store.Save();throw new Exception("摆件已报告连接 "+target+(stationAddress==""?"":"，地址 "+stationAddress)+"；电脑已恢复原网络，但尚无法从当前网络管理它。可在下方输入摆件屏幕上的 IP。");}
      throw new Exception((acknowledged?"摆件已接收连接命令":"连接请求未收到确认")+"，电脑已恢复原网络，但尚未确认摆件连接成功。请查看摆件屏幕，检查目标网络及密码后重试。");
    }
  }
}
