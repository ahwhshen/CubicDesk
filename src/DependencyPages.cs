using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CubicDesk {
  public partial class MainWindow {
    void BuildLocalTools(){
      var intro=Stack(Text("电脑与摆件之间的连接器",20,"#263C59",true),Gap(8),Text("支持 QQ 音乐、网易云音乐及其他提供 Windows 系统媒体会话的播放器。桥接可读取歌曲、封面和进度，并发送播放控制；在线歌词可选择来源。",12,"#5C7592"));page.Children.Add(Card(intro));
      var music=Stack(AppHeading("holocubic-smtc-music","Windows 音乐 · SMTC",20,48),Gap(10),dependencyStatus=Text(bridge.Installed?"组件已安装，等待启动。":"尚未安装本地桥接组件。",13,"#276FC0"),Gap(12));
      var port=Input(J.Num(Store.Config,"bridgePort",17865).ToString());var interval=Input(J.Num(Store.Config,"bridgeInterval",800).ToString());
      string host="";try{if(online)host=Native.LocalAddress(device.Address);}catch{}
      var hostInput=Input(host);var poll=Input(J.Num(Store.Config,"devicePoll",1000).ToString());var timeout=Input(J.Num(Store.Config,"deviceTimeout",2500).ToString());
      sessionPicker=Select(new[]{"自动选择 Windows 当前会话"});foreach(string s in J.Strings(bridge.Snapshot(),"sessions"))sessionPicker.Items.Add(s);string savedSource=J.Str(Store.Config,"bridgeSource");if(sessionPicker.Items.Contains(savedSource))sessionPicker.SelectedItem=savedSource;
      Field(music,"媒体会话",sessionPicker,"播放音乐后可刷新会话列表；媒体软件需要支持 Windows 系统媒体控制。");
      Field(music,"桥接监听端口",port,"使用 1024–65535 之间的空闲端口。");Field(music,"电脑采样间隔（毫秒）",interval,"250–10000；控制歌曲不需要等待下一次采样。");
      var autoStart=new CheckBox {Content="下次打开 Cubic Desk 时自动启动桥接",IsChecked=J.Bool(Store.Config,"bridgeAutoStart")};music.Children.Add(autoStart);
      Action save=delegate{Store.Config["bridgePort"]=Number(port.Text,1024,65535,"端口");Store.Config["bridgeInterval"]=Number(interval.Text,250,10000,"采样间隔");Store.Config["bridgeSource"]=sessionPicker==null||sessionPicker.SelectedIndex<1?"":Convert.ToString(sessionPicker.SelectedItem);Store.Config["bridgeAutoStart"]=autoStart.IsChecked==true;Store.Config["devicePoll"]=Number(poll.Text,250,10000,"摆件轮询间隔");Store.Config["deviceTimeout"]=Number(timeout.Text,500,30000,"摆件超时");Store.Save();};
      music.Children.Add(Gap(14));music.Children.Add(Row(Button(bridge.Installed?"重新安装组件":"安装本地组件",delegate{if(bridge.Running)bridge.Stop();bridge.Install();Notice("SMTC 桥接组件已安装，无需额外下载或安装 Node.js。");UpdateBridgeStatus();return Task.FromResult(0);}),Button("启动桥接",delegate{if(!online)throw new Exception("请先连接摆件，再启动桥接。");save();bridge.Start(device.Address,(int)J.Num(Store.Config,"bridgePort"),(int)J.Num(Store.Config,"bridgeInterval"),J.Str(Store.Config,"bridgeSource"));Notice("桥接已启动，正在读取 Windows 媒体会话。");UpdateBridgeStatus();return Task.FromResult(0);},true),Button("停止",delegate{bridge.Stop();UpdateBridgeStatus();Notice("SMTC 桥接已停止。");return Task.FromResult(0);})));
      music.Children.Add(Gap(10));music.Children.Add(Row(Button("保存偏好",delegate{save();if(bridge.Running)bridge.Send("source:"+J.Str(Store.Config,"bridgeSource"));Notice("播放器选择已保存并应用；端口和采样间隔修改后请重新启动桥接。");return Task.FromResult(0);}),Button("刷新媒体会话",delegate{var picker=sessionPicker;if(picker==null)return Task.FromResult(0);string selected=Convert.ToString(picker.SelectedItem);picker.Items.Clear();picker.Items.Add("自动选择 Windows 当前会话");foreach(string s in J.Strings(bridge.Snapshot(),"sessions"))picker.Items.Add(s);picker.SelectedItem=picker.Items.Contains(selected)?selected:picker.Items[0];Notice("媒体会话已刷新。");return Task.FromResult(0);})));
      music.Children.Add(Gap(16));music.Children.Add(Row(Button("上一首",delegate{bridge.Send("previous");Notice("已发送上一首命令。");return Task.FromResult(0);}),Button("播放 / 暂停",delegate{bridge.Send("toggle");Notice("已发送播放 / 暂停命令。");return Task.FromResult(0);}),Button("下一首",delegate{bridge.Send("next");Notice("已发送下一首命令。");return Task.FromResult(0);})));
      var devicePanel=Stack(Text("摆件端连接参数",19,"#263C59",true),Gap(8),Text("电脑桥接与摆件使用相同端口。写入后会打开摆件上的 Windows 音乐应用。",12,"#687E98"));
      Field(devicePanel,"摆件访问的电脑地址",hostInput,"自动填写通向当前摆件的本机地址。网络变化后请更新。");Field(devicePanel,"摆件轮询间隔（毫秒）",poll);Field(devicePanel,"摆件连接超时（毫秒）",timeout);
      devicePanel.Children.Add(Gap(16));devicePanel.Children.Add(Button("写入摆件并打开 Windows 音乐",async delegate{
        if(!online)throw new Exception("请先连接摆件。");save();if(!bridge.Running)throw new Exception("请先安装并启动本地桥接。");
        string address=DeviceClient.Normalize(hostInput.Text);if(address.EndsWith(".local"))throw new Exception("电脑地址请使用局域网 IPv4。");if(address.StartsWith("127."))throw new Exception("摆件无法访问电脑的回环地址。");
        if(bridge.Port!=(int)J.Num(Store.Config,"bridgePort"))throw new Exception("桥接运行端口与新设置不同，请重新启动桥接。");
        if(!J.List(state,"installed_apps").Any(a=>J.Str(a,"id")=="holocubic-smtc-music"))throw new Exception("此摆件尚未安装 Windows 音乐应用。");
        string cfg="/sd/apps/holocubic-smtc-music/config.lua";byte[] original=await device.Bytes(device.FileRoute(cfg),System.Threading.CancellationToken.None);string backup=Path.Combine(Store.Data,"backups",device.Address);Directory.CreateDirectory(backup);File.WriteAllBytes(Path.Combine(backup,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-smtc-config.lua"),original);
        if(J.Str(J.Map(state,"current_app"),"id")!="holocubic-smtc-music")await Launch("holocubic-smtc-music");
        var existing=await device.Request("/holocubic-smtc-music/api/config");existing["host"]=address;existing["port"]=bridge.Port;existing["poll_ms"]=(int)J.Num(Store.Config,"devicePoll");existing["timeout_ms"]=(int)J.Num(Store.Config,"deviceTimeout");existing["status_path"]="/status";existing["control_path"]="/control";
        await device.Request("/holocubic-smtc-music/api/config","POST",existing);Notice("SMTC 配置已保存到摆件，正在等待摆件请求电脑桥接。");
      },true));devicePanel.Children.Add(Gap(10));devicePanel.Children.Add(Button("读取摆件当前参数",async delegate{
        if(!online)throw new Exception("请先连接摆件。");if(J.Str(J.Map(state,"current_app"),"id")!="holocubic-smtc-music")throw new Exception("请先在摆件打开 Windows 音乐，再读取当前参数。");var c=await device.Request("/holocubic-smtc-music/api/config");hostInput.Text=J.Str(c,"host");port.Text=J.Str(c,"port");poll.Text=J.Str(c,"poll_ms");timeout.Text=J.Str(c,"timeout_ms");Notice("已读取摆件当前的音乐连接参数。");
      }));
      page.Children.Add(Columns(Card(music),Card(devicePanel),1.2));
      var lyricTitle=Input(J.Str(bridge.Snapshot(),"title"));var lyricStatus=Text("按歌曲标题绑定本地 LRC；切换歌曲后不会显示上一首的歌词。",12,"#687E98");
      var lyrics=Stack(Text("同步歌词",18,"#263C59",true));
      var onlineCover=new CheckBox {Content="播放器没有封面时，自动补取在线专辑封面",IsChecked=OnlineArtwork.Enabled,Margin=new Thickness(0,8,0,6)};onlineCover.Checked+=delegate{bridge.EnableOnlineCover(true);};onlineCover.Unchecked+=delegate{bridge.EnableOnlineCover(false);};lyrics.Children.Add(onlineCover);lyrics.Children.Add(Text("在线补取会向所选音乐来源查询歌名与歌手；系统媒体封面优先，失败后自动重试。",11,"#687E98"));
      var qqLyrics=new CheckBox {Content="自动匹配在线歌词",IsChecked=OnlineLyrics.Enabled,Margin=new Thickness(0,12,0,8)};
      qqLyrics.Checked+=delegate{bridge.EnableOnlineLyrics(true);Notice("已开启在线歌词匹配，将向选定来源查询歌曲名称与歌手。");UpdateBridgeStatus();};qqLyrics.Unchecked+=delegate{bridge.EnableOnlineLyrics(false);Notice("已关闭在线歌词查询。");UpdateBridgeStatus();};lyrics.Children.Add(qqLyrics);
      var lyricProvider=Select(new[]{"自动跟随播放器","QQ 音乐","网易云音乐"});string[] providerIds={"auto","qq","netease"};lyricProvider.SelectedIndex=Array.IndexOf(providerIds,OnlineLyrics.Provider);Field(lyrics,"在线歌词来源",lyricProvider);lyricProvider.SelectionChanged+=delegate{try{bridge.SetLyricProvider(providerIds[lyricProvider.SelectedIndex]);UpdateBridgeStatus();Notice("歌词来源已保存。");}catch(Exception ex){Notice(ex.Message,true);}};
      lyrics.Children.Add(Text("自动识别 QQ 音乐和网易云音乐。其他提供系统媒体会话的播放器，可手动选择歌词来源。查询会发送当前歌名与歌手；本地 LRC 优先使用。",11,"#687E98"));lyrics.Children.Add(Gap(8));lyricProviderStatus=Text("",12,"#276FC0");lyrics.Children.Add(lyricProviderStatus);lyrics.Children.Add(Gap(10));lyrics.Children.Add(Button("重新匹配当前歌曲",delegate{bridge.RetryOnlineLyrics();UpdateBridgeStatus();Notice("正在重新查询当前歌曲的同步歌词。");return Task.FromResult(0);}));
      Field(lyrics,"本地歌词对应的歌曲标题",lyricTitle);lyrics.Children.Add(lyricStatus);lyrics.Children.Add(Gap(12));lyrics.Children.Add(Button("导入 LRC 歌词",delegate{
        if(string.IsNullOrWhiteSpace(lyricTitle.Text))throw new Exception("请填写与媒体会话一致的歌曲标题。");var dialog=new OpenFileDialog {Filter="同步歌词 (*.lrc)|*.lrc",Title="选择此歌曲的 LRC 歌词"};if(dialog.ShowDialog(this)==true){bridge.LoadLyrics(dialog.FileName,lyricTitle.Text.Trim());lyricStatus.Text="已导入："+Path.GetFileName(dialog.FileName);Notice("歌词已导入，将在匹配的歌曲播放时提供给摆件。");}return Task.FromResult(0);
      }));
      var access=Stack(Text("局域网访问",18,"#263C59",true),Gap(10),Text("桥接只接受已配对摆件和本机的请求。若桥接运行但收不到摆件请求，可为该摆件添加端口放行规则。",12,"#687E98"),Gap(14),Button("允许当前摆件访问桥接",delegate{
        if(!online)throw new Exception("请先连接摆件。");int p=Number(port.Text,1024,65535,"端口");
        var start=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -File \""+Path.Combine(Store.Root,"Firewall.ps1")+"\" -Port "+p+" -RemoteAddress "+device.Address+" -ProgramPath \""+Path.Combine(Store.Root,"CubicDesk.exe")+"\""){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};using(var process=Process.Start(start)){}Notice("已交给 Windows 请求管理员授权。完成后请看桥接的摆件请求时间。");return Task.FromResult(0);
      }),Gap(10),Text("此操作由你点击后触发 Windows 管理员授权，只放行指定摆件和端口。",11,"#687E98"));page.Children.Add(Columns(Card(lyrics),Card(access)));
      UpdateBridgeStatus();
    }
    static int Number(string value,int min,int max,string label){int n;if(!int.TryParse(value,out n)||n<min||n>max)throw new Exception(label+" 范围为 "+min+"–"+max+"。");return n;}
    void UpdateBridgeStatus(){if(dependencyStatus==null)return;var s=bridge.Snapshot();if(lyricProviderStatus!=null){string status=J.Str(s,"lyrics_state");lyricProviderStatus.Text=status=="local"?"正在使用导入的本地歌词":status=="ready"?"已匹配："+J.Str(s,"lyrics_match"):status=="loading"?"正在匹配同步歌词…":status=="error"?J.Str(s,"lyrics_error"):status=="choose-provider"?"请为当前播放器选择 QQ 音乐或网易云歌词来源":OnlineLyrics.Enabled?"等待播放器提供歌曲信息":"在线歌词匹配未开启";}string text=!bridge.Installed?"○ 未安装组件":!bridge.Running?"○ 已安装 · 已停止":"● 桥接运行中 · 端口 "+bridge.Port;
      if(bridge.Running){text+="\n"+(J.Bool(s,"connected")?J.Str(s,"title","当前媒体")+"  /  "+J.Str(s,"artist")+"  ·  "+J.Str(s,"state"):"等待音乐软件提供媒体会话");text+="\n"+(bridge.LastDeviceRequest==DateTime.MinValue?"尚未收到摆件请求":"摆件最近请求："+bridge.LastDeviceRequest.ToString("HH:mm:ss")+" · "+bridge.Requests+" 次");if(J.Str(s,"cover_source")=="online")text+="\n封面：已补取匹配歌曲的在线专辑图";else if(J.Str(s,"cover_error")!="")text+="\n封面："+J.Str(s,"cover_error");}if(bridge.Error!="")text+="\n"+bridge.Error;dependencyStatus.Text=text;}
  }
}
