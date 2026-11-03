using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace CubicDesk {
  public sealed class MusicBridge : IDisposable {
    readonly object sync=new object();
    Process worker;TcpListener listener;CancellationTokenSource cancel;
    Dictionary<string,object> snapshot=J.D("ok",true,"connected",false,"state","stopped");
    byte[] cover=new byte[0];string coverId="";
    string coverOrigin="",coverTaskKey="",onlineCoverError="",coverStatus="waiting";
    bool onlineCoverEnabled=OnlineArtwork.Enabled;
    int coverFailures,lyricFailures;DateTime coverRetryAt=DateTime.MinValue,lyricRetryAt=DateTime.MinValue;
    CancellationTokenSource coverCancel;
    readonly Dictionary<string,byte[]> coverCache=new Dictionary<string,byte[]>();
    List<Dictionary<string,object>> lyrics=new List<Dictionary<string,object>>();
    string lyricsTitle="",lyricsId="";
    bool onlineLyricsEnabled=OnlineLyrics.Enabled;
    string lyricPreference=OnlineLyrics.Provider,onlineProvider="";
    string onlineKey="",onlineId="",onlineStatus="",onlineError="",onlineMatch="";
    List<Dictionary<string,object>> onlineLines=new List<Dictionary<string,object>>();
    readonly Dictionary<string,OnlineLyricResult> lyricCache=new Dictionary<string,OnlineLyricResult>();
    CancellationTokenSource lyricCancel;
    DateTime updated=DateTime.MinValue;
    public DateTime LastDeviceRequest {get;private set;}
    public int Requests {get;private set;}
    public int CoverRequests {get;private set;}
    public int LyricRequests {get;private set;}
    public string LastCoverId {get;private set;}
    public string LastLyricId {get;private set;}
    public bool Running {get{try{return listener!=null&&worker!=null&&!worker.HasExited;}catch{return false;}}}
    public string DeviceAddress {get;private set;}
    public int Port {get;private set;}
    public string Error="";
    public MusicBridge(){string file=J.Str(Store.Config,"lyricFile"),title=J.Str(Store.Config,"lyricTitle");if(file!=""&&title!=""){try{LoadLyrics(Path.Combine(Store.Data,"lyrics",Path.GetFileName(file)),title,false);}catch{}}}
    public bool Installed {get{return File.Exists(WorkerPath);}}
    public string WorkerPath {get{return Path.Combine(Store.Data,"dependencies","SMTC","SmtcWorker.ps1");}}
    public void Install(){Directory.CreateDirectory(Path.GetDirectoryName(WorkerPath));File.Copy(Path.Combine(Store.Root,"SmtcWorker.ps1"),WorkerPath,true);Store.Config["smtcInstalled"]=true;Store.Save();}
    public Dictionary<string,object> Snapshot() {
      lock(sync) {
        var value=new Dictionary<string,object>(snapshot);
        if(!Running||DateTime.UtcNow-updated>TimeSpan.FromSeconds(10)){value["connected"]=false;value["state"]=Running?"waiting":"stopped";return value;}
        if(J.Str(value,"state")=="playing")value["position_ms"]=Math.Min(J.Num(value,"duration_ms"),J.Num(value,"position_ms")+(DateTime.UtcNow-updated).TotalMilliseconds);
        if(cover.Length>0){value["cover_provider"]="local";value["cover_id_text"]=coverId;value["cover_source"]=coverOrigin;value["cover_error"]="";}
        else if(onlineCoverError!="")value["cover_error"]="播放器未提供封面；"+onlineCoverError;
        value["cover_state"]=coverStatus;
        bool hasLyrics=lyrics.Count>0&&string.Equals(J.Str(value,"title"),lyricsTitle,StringComparison.OrdinalIgnoreCase);
        bool hasOnline=onlineLyricsEnabled&&onlineKey==TrackKey(value)&&onlineLines.Count>0;
        value["lyrics_available"]=hasLyrics||hasOnline;
        if(hasLyrics){value["lyric_provider"]="local";value["lyric_id_text"]=lyricsId;}
        else if(hasOnline){value["lyric_provider"]=onlineProvider;value["lyric_id_text"]=onlineId;}
        value["lyrics_state"]=hasLyrics?"local":onlineStatus;value["lyrics_error"]=onlineError;value["lyrics_match"]=onlineMatch;
        return value;
      }
    }
    public void Start(string device,int port,int interval,string preferred) {
      if(!Installed)throw new Exception("请先安装本地 SMTC 桥接组件。");
      IPAddress ip;if(!IPAddress.TryParse(device,out ip)||!DeviceClient.Private(ip))throw new Exception("请先连接一个局域网摆件。");
      if(port<1024||port>65535)throw new Exception("端口范围为 1024–65535。");
      Stop();if(!File.ReadAllBytes(WorkerPath).SequenceEqual(File.ReadAllBytes(Path.Combine(Store.Root,"SmtcWorker.ps1"))))Install();Error="";DeviceAddress=device;Port=port;Requests=0;CoverRequests=0;LyricRequests=0;LastCoverId="";LastLyricId="";LastDeviceRequest=DateTime.MinValue;
      try {
        listener=new TcpListener(IPAddress.Any,port);listener.Start(12);cancel=new CancellationTokenSource();
        var start=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -NonInteractive -File \""+WorkerPath+"\" -Interval "+Math.Max(250,interval)+" -RuntimeAssembly \""+Path.Combine(Store.Root,"CubicDesk.exe")+"\"") {
          UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
        worker=new Process {StartInfo=start,EnableRaisingEvents=true};
        worker.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){
          if(sender!=worker)return;
          if(string.IsNullOrWhiteSpace(e.Data))return;
          try {var data=J.Parse(e.Data);lock(sync){
            if(CoverKey(data)!=CoverKey(snapshot)){cover=new byte[0];coverId="";coverOrigin="";coverTaskKey="";coverStatus="waiting";onlineCoverError="";coverFailures=0;if(coverCancel!=null)coverCancel.Cancel();}
            if(J.Get(data,"cover_b64")!=null){var bytes=Convert.FromBase64String(J.Str(data,"cover_b64"));if(bytes.Length>0){cover=bytes;coverOrigin="windows";coverId=Hash(CoverKey(data)+"|"+Convert.ToBase64String(bytes));coverStatus="ready";if(coverCancel!=null)coverCancel.Cancel();}data.Remove("cover_b64");}
            snapshot=data;updated=DateTime.UtcNow;if(!J.Bool(data,"ok"))Error=J.Str(data,"error");
          }QueueOnlineLyrics(false);QueueOnlineCover();}catch(Exception ex){Error="SMTC 数据读取失败："+ex.Message;}
        };
        worker.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(!string.IsNullOrWhiteSpace(e.Data))Error=e.Data;};
        worker.Start();worker.BeginOutputReadLine();worker.BeginErrorReadLine();
        if(!string.IsNullOrWhiteSpace(preferred))Send("source:"+preferred);
        var listening=listener;var token=cancel.Token;
        Task.Run(async delegate {while(!token.IsCancellationRequested){try{var client=await listening.AcceptTcpClientAsync();Handle(client,token);}catch{if(!token.IsCancellationRequested)Error="SMTC 监听已中断。";break;}}});
      }catch{Stop();throw;}
    }
    public void Send(string action){if(worker==null||worker.HasExited)throw new Exception("SMTC 桥接没有运行。");worker.StandardInput.WriteLine(action.Replace("\r","").Replace("\n",""));worker.StandardInput.Flush();}
    async void Handle(TcpClient tcp,CancellationToken token) {
      using(tcp) {
        try {
          tcp.ReceiveTimeout=2500;tcp.SendTimeout=2500;
          var remote=((IPEndPoint)tcp.Client.RemoteEndPoint).Address;
          var stream=tcp.GetStream();
          if(!IPAddress.IsLoopback(remote)&&remote.ToString()!=DeviceAddress){await Reply(stream,403,"application/json",Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"device not paired\"}"));return;}
          // Bound header read, deadline, and request length prevent an idle client from occupying the bridge.
          var buffer=new byte[8192];int used=0;
          using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)) {
            timeout.CancelAfter(3000);
            while(used<buffer.Length){var read=stream.ReadAsync(buffer,used,buffer.Length-used,timeout.Token);if(await Task.WhenAny(read,Task.Delay(3000,timeout.Token))!=read)return;int n=await read;if(n==0)return;used+=n;if(Encoding.ASCII.GetString(buffer,0,used).Contains("\r\n\r\n"))break;}
          }
          string header=Encoding.ASCII.GetString(buffer,0,used);if(!header.Contains("\r\n\r\n")){await Reply(stream,400,"text/plain",Encoding.UTF8.GetBytes("header too large"));return;}
          string[] request=header.Split('\n')[0].Trim().Split(' ');if(request.Length<2||request[0]!="GET"){await Reply(stream,405,"text/plain",Encoding.UTF8.GetBytes("GET only"));return;}
          Uri uri;if(!Uri.TryCreate("http://localhost"+request[1],UriKind.Absolute,out uri)){await Reply(stream,400,"text/plain",new byte[0]);return;}
          var query=System.Web.HttpUtility.ParseQueryString(uri.Query);
          if(!IPAddress.IsLoopback(remote)){LastDeviceRequest=DateTime.Now;Requests++;}
          byte[] body;string mime="application/json; charset=utf-8";int code=200;
          switch(uri.AbsolutePath) {
            case "/":case "/health":body=Encoding.UTF8.GetBytes(J.Stringify(J.D("ok",true,"bridge","CubicDesk SMTC","paired_device",DeviceAddress,"cover_requests",CoverRequests,"lyrics_requests",LyricRequests,"last_cover_id",LastCoverId,"last_lyric_id",LastLyricId)));break;
            case "/status":body=Encoding.UTF8.GetBytes(J.Stringify(Snapshot()));break;
            case "/control":
              string action=query["action"]??"";if(action=="playpause")action="toggle";
              if(!new[]{"play","pause","toggle","next","previous"}.Contains(action)){code=400;body=Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"unsupported action\"}");}
              else {Send(action);body=Encoding.UTF8.GetBytes("{\"ok\":true,\"queued\":true}");}break;
            case "/lyrics":lock(sync){var lines=query["id"]==lyricsId?lyrics:onlineLyricsEnabled&&query["id"]==onlineId&&onlineKey==TrackKey(snapshot)?onlineLines:null;if(lines==null||lines.Count==0){code=404;body=Encoding.UTF8.GetBytes("{\"ok\":false,\"lines\":[]}");}else body=Encoding.UTF8.GetBytes(J.Stringify(J.D("ok",true,"lines",lines)));}break;
            case "/cover":lock(sync){if(query["id"]!=coverId||cover.Length==0){code=404;body=new byte[0];}else {body=cover;mime="image/jpeg";}}break;
            default:code=404;body=Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"unknown route\"}");break;
          }
          await Reply(stream,code,mime,body);
          if(!IPAddress.IsLoopback(remote)&&code==200){if(uri.AbsolutePath=="/cover"){CoverRequests++;LastCoverId=query["id"];}if(uri.AbsolutePath=="/lyrics"){LyricRequests++;LastLyricId=query["id"];}}
        }catch(Exception ex){if(!token.IsCancellationRequested)Error="请求处理："+ex.Message;}
      }
    }
    static async Task Reply(NetworkStream stream,int code,string mime,byte[] body){var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Error")+"\r\nContent-Type: "+mime+"\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");await stream.WriteAsync(header,0,header.Length);await stream.WriteAsync(body,0,body.Length);}
    static string TrackKey(Dictionary<string,object> value){return J.Str(value,"source_app")+"|"+J.Str(value,"title")+"|"+J.Str(value,"artist")+"|"+J.Num(value,"duration_ms");}
    static string CoverKey(Dictionary<string,object> value){return J.Str(value,"source_app")+"|"+J.Str(value,"title")+"|"+J.Str(value,"artist")+"|"+J.Str(value,"album");}
    public void EnableOnlineCover(bool enabled){lock(sync){onlineCoverEnabled=enabled;if(coverCancel!=null)coverCancel.Cancel();coverTaskKey="";onlineCoverError="";coverFailures=0;if(!enabled&&coverOrigin=="online"){cover=new byte[0];coverId="";coverOrigin="";}coverStatus=cover.Length>0?"ready":enabled?"waiting":"disabled";}Store.Config["onlineCoverEnabled"]=enabled;Store.Save();if(enabled)QueueOnlineCover();}
    void QueueOnlineCover(){
      string key,provider,title,artist;double duration;CancellationToken token;
      lock(sync){
        if(!onlineCoverEnabled||!Running||!J.Bool(snapshot,"connected")||J.Str(snapshot,"title")==""||cover.Length>0)return;
        provider=OnlineLyrics.Resolve(lyricPreference,J.Str(snapshot,"source_app"));if(provider!="qq"&&provider!="netease")return;key=CoverKey(snapshot);
        if(key==coverTaskKey&&(coverStatus=="loading"||DateTime.UtcNow<coverRetryAt))return;
        if(key!=coverTaskKey){coverFailures=0;coverRetryAt=DateTime.MinValue;}coverTaskKey=key;
        if(coverCancel!=null){coverCancel.Cancel();coverCancel.Dispose();}coverCancel=new CancellationTokenSource();token=coverCancel.Token;coverStatus="loading";onlineCoverError="";
        byte[] cached;if(coverCache.TryGetValue(provider+"|"+key,out cached)){cover=cached;coverId=Hash(key+"|"+Convert.ToBase64String(cached));coverOrigin="online";coverStatus="ready";return;}
        title=J.Str(snapshot,"title");artist=J.Str(snapshot,"artist");duration=J.Num(snapshot,"duration_ms");
      }
      Task.Run(async delegate{
        try{var bytes=provider=="qq"?await QQMusicLyrics.FetchCover(title,artist,duration,token):await NetEaseLyrics.FetchCover(title,artist,duration,token);
          lock(sync){if(!onlineCoverEnabled||token.IsCancellationRequested||CoverKey(snapshot)!=key||coverOrigin=="windows")return;if(coverCache.Count>=20)coverCache.Remove(coverCache.Keys.First());coverCache[provider+"|"+key]=bytes;cover=bytes;coverId=Hash(key+"|"+Convert.ToBase64String(bytes));coverOrigin="online";coverStatus="ready";onlineCoverError="";coverFailures=0;}
        }catch(Exception ex){lock(sync){if(token.IsCancellationRequested||CoverKey(snapshot)!=key)return;coverStatus="error";onlineCoverError=OnlineArtwork.Failure(ex);coverRetryAt=DateTime.UtcNow+OnlineArtwork.RetryDelay(++coverFailures);}}
      });
    }
    public void EnableOnlineLyrics(bool enabled){lock(sync){onlineLyricsEnabled=enabled;if(lyricCancel!=null)lyricCancel.Cancel();onlineKey="";onlineLines=new List<Dictionary<string,object>>();onlineStatus=enabled?"waiting":"disabled";onlineError="";onlineMatch="";}Store.Config["onlineLyricsEnabled"]=enabled;Store.Save();if(enabled)QueueOnlineLyrics(false);}
    public void SetLyricProvider(string provider){if(!new[]{"auto","qq","netease"}.Contains(provider))throw new Exception("歌词来源无效。");lock(sync){lyricPreference=provider;onlineKey="";onlineLines=new List<Dictionary<string,object>>();onlineMatch="";onlineError="";onlineStatus=onlineLyricsEnabled?"waiting":"disabled";if(lyricCancel!=null)lyricCancel.Cancel();if(coverCancel!=null)coverCancel.Cancel();coverTaskKey="";if(coverOrigin=="online"){cover=new byte[0];coverId="";coverOrigin="";}}Store.Config["lyricProvider"]=provider;Store.Save();QueueOnlineLyrics(true);QueueOnlineCover();}
    public void RetryOnlineLyrics(){if(!onlineLyricsEnabled)throw new Exception("请先启用 在线歌词匹配。");if(!Running)throw new Exception("请先启动音乐桥接。");lock(sync){if(cover.Length==0){if(coverCancel!=null)coverCancel.Cancel();coverTaskKey="";}}QueueOnlineLyrics(true);QueueOnlineCover();}
    void QueueOnlineLyrics(bool retry){
      string key,title,artist,provider;double duration;CancellationToken token;
      lock(sync){
        if(!onlineLyricsEnabled||!Running)return;
        if(!J.Bool(snapshot,"connected")||J.Str(snapshot,"title")==""){onlineStatus="waiting";return;}
        provider=OnlineLyrics.Resolve(lyricPreference,J.Str(snapshot,"source_app"));if(provider==""){onlineStatus="choose-provider";return;}
        key=TrackKey(snapshot);if(!retry&&key==onlineKey&&onlineProvider==provider&&!(onlineStatus=="error"&&DateTime.UtcNow>=lyricRetryAt))return;
        if(key!=onlineKey||provider!=onlineProvider)lyricFailures=0;
        if(lyricCancel!=null){lyricCancel.Cancel();lyricCancel.Dispose();}lyricCancel=new CancellationTokenSource();token=lyricCancel.Token;
        title=J.Str(snapshot,"title");artist=J.Str(snapshot,"artist");duration=J.Num(snapshot,"duration_ms");onlineKey=key;onlineProvider=provider;onlineId="";onlineLines=new List<Dictionary<string,object>>();onlineError="";onlineMatch="";onlineStatus="loading";
        OnlineLyricResult cached;if(!retry&&lyricCache.TryGetValue(provider+"|"+key,out cached)){ApplyOnlineLyrics(cached);return;}
      }
      Task.Run(async delegate{
        try{var result=await OnlineLyrics.Fetch(provider,title,artist,duration,token);lock(sync){if(!onlineLyricsEnabled||onlineKey!=key||onlineProvider!=provider||token.IsCancellationRequested)return;if(lyricCache.Count>=20)lyricCache.Remove(lyricCache.Keys.First());lyricCache[provider+"|"+key]=result;ApplyOnlineLyrics(result);}}
        catch(Exception ex){lock(sync){if(onlineKey==key&&onlineProvider==provider&&!token.IsCancellationRequested){onlineStatus="error";onlineError=OnlineArtwork.Failure(ex);lyricRetryAt=DateTime.UtcNow+OnlineArtwork.RetryDelay(++lyricFailures);}}}
      });
    }
    void ApplyOnlineLyrics(OnlineLyricResult result){onlineProvider=result.Provider;onlineId=result.SongId;onlineLines=result.Lines;onlineStatus="ready";onlineError="";onlineMatch=result.Title+" / "+result.Artist;lyricFailures=0;}
    public void LoadLyrics(string path,string title,bool persist=true) {
      if(new FileInfo(path).Length>1024*1024)throw new Exception("LRC 文件请小于 1 MB。");
      var lines=ParseLrc(File.ReadAllText(path,Encoding.UTF8));if(lines.Count==0)throw new Exception("此文件没有带时间戳的 LRC 歌词。");
      lock(sync){lyrics=lines;lyricsTitle=title;lyricsId=Hash(title+File.GetLastWriteTimeUtc(path).Ticks);}
      if(persist){string dir=Path.Combine(Store.Data,"lyrics");Directory.CreateDirectory(dir);string name=Hash(title)+".lrc",dest=Path.Combine(dir,name);if(!string.Equals(Path.GetFullPath(path),Path.GetFullPath(dest),StringComparison.OrdinalIgnoreCase))File.Copy(path,dest,true);Store.Config["lyricFile"]=name;Store.Config["lyricTitle"]=title;Store.Save();}
    }
    public static List<Dictionary<string,object>> ParseLrc(string text) {
      var lines=new List<Dictionary<string,object>>();int offset=0;
      var off=Regex.Match(text,@"\[offset:([+-]?\d+)\]",RegexOptions.IgnoreCase);if(off.Success)int.TryParse(off.Groups[1].Value,out offset);
      foreach(string raw in text.Split('\n')) {
        var times=Regex.Matches(raw,@"\[(\d+):(\d{2})(?:[\.:](\d{1,3}))?\]");
        string lyric=Regex.Replace(raw,@"\[[^\]]*\]","").Trim();
        foreach(Match t in times){int min,sec;if(!int.TryParse(t.Groups[1].Value,out min)||min>1000000||!int.TryParse(t.Groups[2].Value,out sec)||sec>59)continue;string fraction=t.Groups[3].Value;int ms=fraction.Length==0?0:int.Parse(fraction.PadRight(3,'0'));lines.Add(J.D("at_ms",Math.Max(0,(long)min*60000+sec*1000+ms-offset),"text",lyric));}
      }
      return lines.OrderBy(l=>J.Num(l,"at_ms")).ToList();
    }
    static string Hash(string s){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-","").Substring(0,16).ToLowerInvariant();}
    public void Stop() {
      lock(sync){if(coverCancel!=null){coverCancel.Cancel();coverCancel.Dispose();coverCancel=null;}coverTaskKey="";coverStatus="waiting";coverOrigin="";onlineCoverError="";}
      lock(sync){if(lyricCancel!=null){lyricCancel.Cancel();lyricCancel.Dispose();lyricCancel=null;}onlineKey="";onlineLines=new List<Dictionary<string,object>>();onlineId="";onlineStatus=onlineLyricsEnabled?"waiting":"disabled";onlineError="";onlineMatch="";}
      if(cancel!=null){cancel.Cancel();cancel.Dispose();cancel=null;}
      if(listener!=null){listener.Stop();listener=null;}
      if(worker!=null){try{if(!worker.HasExited){worker.StandardInput.WriteLine("quit");if(!worker.WaitForExit(2500))worker.Kill();}}catch{}worker.Dispose();worker=null;}
      lock(sync){snapshot=J.D("ok",true,"connected",false,"state","stopped");cover=new byte[0];}
    }
    public void Dispose(){Stop();}
  }
}
