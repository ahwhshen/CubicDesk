// 用途：封装 JSON 数据、本地存储、设备请求与局域网发现。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CubicDesk {
  public static class J {
    public static string Stringify(object value) { return new JavaScriptSerializer { MaxJsonLength=16000000 }.Serialize(value); }
    public static Dictionary<string,object> Parse(string text) { return new JavaScriptSerializer { MaxJsonLength=16000000 }.Deserialize<Dictionary<string,object>>(text); }
    public static Dictionary<string,object> Obj(object v) { return v as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    public static object Get(object d,string key) { object v; return Obj(d).TryGetValue(key,out v)?v:null; }
    public static Dictionary<string,object> Map(object d,string key) { return Obj(Get(d,key)); }
    public static string Str(object d,string key,string fallback="") { object v=Get(d,key); return v==null?fallback:Convert.ToString(v); }
    public static bool Bool(object d,string key) { object v=Get(d,key); return v is bool && (bool)v; }
    public static double Num(object d,string key,double fallback=0) { double value; return double.TryParse(Str(d,key),out value)?value:fallback; }
    public static List<Dictionary<string,object>> List(object d,string key) { var a=Get(d,key) as IEnumerable; return a==null?new List<Dictionary<string,object>>():a.Cast<object>().Select(Obj).ToList(); }
    public static List<string> Strings(object d,string key) { var a=Get(d,key) as IEnumerable; return a==null?new List<string>():a.Cast<object>().Select(Convert.ToString).ToList(); }
    public static Dictionary<string,object> D(params object[] pairs) { var d=new Dictionary<string,object>(); for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1]; return d; }
  }
  public static class Store {
    public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Data=Path.Combine(Root,"data");
    public static Dictionary<string,object> Config=new Dictionary<string,object>();
    public static void Load() { Directory.CreateDirectory(Data); try { Config=J.Parse(File.ReadAllText(Path.Combine(Data,"preferences.json"),Encoding.UTF8)); }catch{} }
    public static void Save() { Atomic(Path.Combine(Data,"preferences.json"),J.Stringify(Config)); }
    public static void Atomic(string path,string content) { string tmp=path+".tmp";File.WriteAllText(tmp,content,new UTF8Encoding(false));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path); }
    public static void Log(string message) { try {string path=Path.Combine(Data,"activity.log");if(File.Exists(path)&&new FileInfo(path).Length>1048576)File.WriteAllText(path,"");File.AppendAllText(path,DateTime.Now.ToString("MM-dd HH:mm:ss")+"  "+message+Environment.NewLine,Encoding.UTF8);}catch{} }
  }
  public sealed class DeviceClient : IDisposable {
    readonly HttpClient client;
    public readonly string Address;
    public DeviceClient(string address,int timeout=10) {
      Address=Normalize(address);
      client=new HttpClient(new HttpClientHandler { UseProxy=false,AllowAutoRedirect=false });
      client.Timeout=TimeSpan.FromSeconds(timeout);
      client.BaseAddress=new Uri("http://"+Address);
    }
    public static bool Private(IPAddress a) { var b=a.GetAddressBytes();return b.Length==4&&(b[0]==10||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168)||(b[0]==169&&b[1]==254)||b[0]==127); }
    public static string Normalize(string value) {
      string s=(value??"").Trim(); if(s.StartsWith("http://",StringComparison.OrdinalIgnoreCase))s=s.Substring(7);s=s.TrimEnd('/');
      IPAddress ip;if(IPAddress.TryParse(s,out ip)&&Private(ip))return ip.ToString();
      if(System.Text.RegularExpressions.Regex.IsMatch(s,@"^[a-zA-Z0-9-]+\.local$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))return s.ToLowerInvariant();
      throw new ArgumentException("请输入局域网 IPv4 地址，或以 .local 结尾的设备名称。");
    }
public static bool IsDevice(Dictionary<string,object> s) {
  var wifi=J.Get(s,"wifi") as Dictionary<string,object>;object apps=J.Get(s,"installed_apps");
  if(!J.Bool(s,"ok")||J.Str(s,"main_path")!="/main"||wifi==null||(!(J.Get(wifi,"ap_ssid") is string)&&!(J.Get(wifi,"sta_ssid") is string)))return false;
  var records=apps as IEnumerable;if(records==null||apps is string||apps is IDictionary)return false;
  var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(object record in records){var app=record as Dictionary<string,object>;object id=J.Get(app,"id");if(app==null||!(id is string)||string.IsNullOrWhiteSpace((string)id)||!ids.Add((string)id))return false;}
  return true;
}
    public async Task<byte[]> Bytes(string route,CancellationToken token) { using(var r=await client.GetAsync(route,token)){r.EnsureSuccessStatusCode();return await r.Content.ReadAsByteArrayAsync();} }
    public async Task<byte[]> Download(string route) {using(var bulk=new HttpClient(new HttpClientHandler {UseProxy=false,AllowAutoRedirect=false})){bulk.Timeout=TimeSpan.FromMinutes(5);using(var r=await bulk.GetAsync(new Uri(client.BaseAddress,route))){r.EnsureSuccessStatusCode();return await r.Content.ReadAsByteArrayAsync();}}}
    public async Task<string> Text(string route) { return Encoding.UTF8.GetString(await Bytes(route,CancellationToken.None)); }
    public async Task<Dictionary<string,object>> Request(string path,string method="GET",object payload=null,CancellationToken token=default(CancellationToken)) {
      using(var req=new HttpRequestMessage(new HttpMethod(method),path)) {
        if(payload!=null)req.Content=new StringContent(J.Stringify(payload),Encoding.UTF8,"application/json");
        using(var res=await client.SendAsync(req,token)) {
          string text=await res.Content.ReadAsStringAsync();Dictionary<string,object> data;
          try{data=J.Parse(text);if(data==null||(data.ContainsKey("ok")&&!(data["ok"] is bool)))throw new FormatException();}catch{throw new Exception("设备返回了无法识别的响应（HTTP "+(int)res.StatusCode+"）。");}
          if(!res.IsSuccessStatusCode||(J.Get(data,"ok") is bool&&!J.Bool(data,"ok")))throw new Exception(J.Str(data,"error",J.Str(data,"message","HTTP "+(int)res.StatusCode)));
          return data;
        }
      }
    }
    public async Task Put(string path,byte[] bytes,string mime="application/octet-stream") {
      using(var bulk=new HttpClient(new HttpClientHandler {UseProxy=false,AllowAutoRedirect=false})){bulk.Timeout=TimeSpan.FromMinutes(5);using(var content=new ByteArrayContent(bytes)){content.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue(mime);using(var res=await bulk.PutAsync(new Uri(client.BaseAddress,path),content)) {string text=await res.Content.ReadAsStringAsync();UploadResponse.EnsureSuccess(res.StatusCode,text);}}}
    }
    public string FileRoute(string path) { return "/api/system/fs/file?path="+Uri.EscapeDataString(path); }
    public async Task BackupAndPut(string path,byte[] bytes,string mime="application/json") {
      byte[] original=await Download(FileRoute(path));
      string dir=Path.Combine(Store.Data,"backups",Address.Replace(':','_'));Directory.CreateDirectory(dir);
      File.WriteAllBytes(Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Path.GetFileName(path)),original);
      await Put("/api/system/fs/upload?path="+Uri.EscapeDataString(path),bytes,mime);
    }
    public void Dispose(){client.Dispose();}
  }
  public static class Native {
    public static async Task<Dictionary<string,object>> Hotspot(string action,object payload=null) {
      return await Task.Run(delegate {
        var start=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -NonInteractive -File \""+Path.Combine(Store.Root,"Native.ps1")+"\" -Action "+action) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        using(var p=Process.Start(start)) {
          p.StandardInput.WriteLine(payload==null?"{}":J.Stringify(payload));p.StandardInput.Close();
          var outTask=p.StandardOutput.ReadToEndAsync();var errTask=p.StandardError.ReadToEndAsync();
          if(!p.WaitForExit(45000)){p.Kill();throw new Exception("Windows 热点操作超时。");}
          string text=outTask.Result.Trim();if(string.IsNullOrEmpty(text))throw new Exception("无法读取 Windows 热点。"+errTask.Result);
          var result=J.Parse(text);if(!J.Bool(result,"ok"))throw new Exception(J.Str(result,"error"));return result;
        }
      });
    }
    public static string LocalAddress(string device) {
      using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){socket.Connect(device,80);return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();}
    }
  }
  public sealed class Discovery {
    public int Total {get;private set;}
    public async Task<List<Dictionary<string,object>>> Scan(Action<int,int,string> progress,CancellationToken token,bool full) {
      var seeds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      Action<string> add=delegate(string s){IPAddress ip;if(IPAddress.TryParse(s,out ip)&&DeviceClient.Private(ip)&&!IPAddress.IsLoopback(ip))seeds.Add(s);};
      add(J.Str(Store.Config,"lastAddress"));add("192.168.18.1");add("192.168.137.7");
      try{foreach(var ip in await Dns.GetHostAddressesAsync("clocteck-cubic.local"))add(ip.ToString());}catch{}
      try{var h=await Native.Hotspot("Status");foreach(var peer in J.List(h,"peers"))foreach(string host in J.Strings(peer,"hosts"))add(host);}catch{}
      var candidates=seeds.ToList();
      if(full) {
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()) {
          if(nic.OperationalStatus!=OperationalStatus.Up||nic.NetworkInterfaceType==NetworkInterfaceType.Loopback||nic.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
          foreach(var u in nic.GetIPProperties().UnicastAddresses){
            if(u.Address.AddressFamily!=AddressFamily.InterNetwork||!DeviceClient.Private(u.Address)||IPAddress.IsLoopback(u.Address))continue;
            byte[] bytes=u.Address.GetAddressBytes();if(bytes[0]==169)continue;
            uint ip=ToUInt(bytes),mask=ToUInt(u.IPv4Mask.GetAddressBytes());uint network=ip&mask, hosts=~mask;
            if(hosts>1023){network=ip&0xffffff00;hosts=255;}
            for(uint n=1;n<hosts;n++){var a=FromUInt(network+n);if(a!=u.Address.ToString()&&seeds.Add(a))candidates.Add(a);}
          }
        }
      }
      Total=candidates.Count;int completed=0;var found=new List<Dictionary<string,object>>();var gate=new SemaphoreSlim(20);
      var tasks=candidates.Select(async address=>{
        await gate.WaitAsync(token);
        try {
          token.ThrowIfCancellationRequested();
          using(var d=new DeviceClient(address,2)) {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
              deadline.CancelAfter(1200);var state=await d.Request("/api/system/state","GET",null,deadline.Token);
              if(DeviceClient.IsDevice(state)){state["address"]=address;lock(found)found.Add(state);}
            }
          }
        }catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}catch{}
        finally {gate.Release();int done=Interlocked.Increment(ref completed);progress(done,Total,address);}
      }).ToArray();
      try{await Task.WhenAll(tasks);}finally{gate.Dispose();}
      return found.GroupBy(s=>J.Str(s,"address")).Select(g=>g.First()).ToList();
    }
    static uint ToUInt(byte[] b){return ((uint)b[0]<<24)|((uint)b[1]<<16)|((uint)b[2]<<8)|b[3];}
    static string FromUInt(uint x){return string.Format("{0}.{1}.{2}.{3}",(x>>24)&255,(x>>16)&255,(x>>8)&255,x&255);}
  }
}
