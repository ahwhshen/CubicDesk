using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;

namespace CubicDesk {
  public static class AppIcons {
    sealed class Entry {public DateTime Expires;public Task<ImageSource> Task;}
    static readonly object sync=new object();
    static readonly Dictionary<string,Entry> cache=new Dictionary<string,Entry>();
    static readonly Dictionary<string,ImageSource> bundled=new Dictionary<string,ImageSource>();
    static DateTime refreshed=DateTime.MinValue;
    static Task<Dictionary<string,string>> catalogTask;
    static string catalogKey="";
    static DateTime catalogExpires;
    public static string CatalogStatus="尚未读取网上应用中心";
    static readonly SemaphoreSlim limit=new SemaphoreSlim(2);
    public static bool ValidId(string id){return Regex.IsMatch(id??"",@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$");}
    static ImageSource Decode(byte[] bytes){using(var memory=new MemoryStream(bytes)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=192;bitmap.StreamSource=memory;bitmap.EndInit();bitmap.Freeze();return bitmap;}}
    static ImageSource Bundled(string id){lock(sync){ImageSource source;if(bundled.TryGetValue(id,out source))return source;try{string file=Path.Combine(Store.Root,"assets","app-icons",id+".png");source=File.Exists(file)?Decode(File.ReadAllBytes(file)):null;}catch{source=null;}bundled[id]=source;return source;}}
    static string Hash(string key){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","").ToLowerInvariant();}
    public static void Invalidate(string address){lock(sync){refreshed=DateTime.UtcNow;cache.Clear();catalogTask=null;}}
    static Dictionary<string,string> IconIndex(object catalog,string server){
      var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
      foreach(var item in J.List(catalog,"items")){string id=J.Str(item,"app_id",J.Str(item,"id")),url=J.Str(item,"icon_url");Uri absolute;
        if(ValidId(id)&&Uri.TryCreate(new Uri(server.TrimEnd('/')+"/"),url,out absolute)&&url!=""&&DependencyCatalog.WebUrl(absolute.AbsoluteUri))result[id]=absolute.AbsoluteUri;
      }return result;
    }
    public static void AdoptCatalog(object catalog,string server){lock(sync){var icons=IconIndex(catalog,server);catalogKey=server.TrimEnd('/')+"|*";catalogTask=Task.FromResult(icons);catalogExpires=DateTime.UtcNow.AddMinutes(10);CatalogStatus="应用目录已读取，含 "+icons.Count+" 个图标地址";cache.Clear();}}
    static Task<Dictionary<string,string>> Catalog(string address){
      string server=J.Str(Store.Config,"storeServer","https://cubic.clocteck.com").TrimEnd('/');if(!DependencyCatalog.WebUrl(server))server="https://cubic.clocteck.com";
      string key=server+"|"+(address??"");lock(sync){if(catalogTask!=null&&catalogExpires>DateTime.UtcNow&&(catalogKey==key||catalogKey==server+"|*"))return catalogTask;catalogKey=key;catalogExpires=DateTime.UtcNow.AddMinutes(2);catalogTask=ReadCatalog(address,server);return catalogTask;}
    }
    static async Task<Dictionary<string,string>> ReadCatalog(string address,string server){
      var saved=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string file=Path.Combine(Store.Data,"icon-catalog.json");
      try{var data=J.Parse(File.ReadAllText(file));if(J.Str(data,"source")==server)saved=IconIndex(data,server);}catch{}
      if(string.IsNullOrEmpty(address)){CatalogStatus="设备未连接，使用缓存及本地图标";return saved;}
      try{using(var client=new DeviceClient(address,12)){
        var data=await client.Request("/api/system/apps/catalog?server="+Uri.EscapeDataString(server)+"&channel=stable").ConfigureAwait(false);var icons=IconIndex(data,server);
        CatalogStatus="网上应用中心已读取，含 "+icons.Count+" 个图标地址";
        try{Directory.CreateDirectory(Store.Data);Store.Atomic(file,J.Stringify(J.D("source",server,"items",J.List(data,"items"),"fetched_at",DateTime.Now.ToString("s"))));}catch{}
        return icons;
      }}catch(Exception ex){CatalogStatus="网上应用中心读取失败："+ex.Message+"；已回退缓存及本地图标";return saved;}
    }
    public static async Task<ImageSource> Fetch(string id,string address,bool installed,string iconUrl,string version){
      if(!ValidId(id))return null;
      var urls=new List<string>();var index=await Catalog(address).ConfigureAwait(false);string remote;
      if(index.TryGetValue(id,out remote))urls.Add(remote);
      if(DependencyCatalog.WebUrl(iconUrl)&&iconUrl.Length<=2048&&!urls.Contains(iconUrl))urls.Add(iconUrl);
      foreach(string url in urls){var source=await CachedRead(url,version).ConfigureAwait(false);if(source!=null)return source;}
      if(installed&&!string.IsNullOrEmpty(address)){try{var source=await CachedRead("http://"+DeviceClient.Normalize(address)+"/api/system/fs/file?path="+Uri.EscapeDataString("/sd/apps/"+id+"/main.png"),version).ConfigureAwait(false);if(source!=null)return source;}catch{}}
      return Bundled(id);
    }
    static Task<ImageSource> CachedRead(string url,string version){
      string key=url+"|"+(version??"");lock(sync){Entry existing;if(cache.TryGetValue(key,out existing)&&existing.Expires>DateTime.UtcNow)return existing.Task;var task=Read(url,key,refreshed);cache[key]=new Entry {Task=task,Expires=DateTime.UtcNow.AddMinutes(20)};return task;}
    }
    static async Task<ImageSource> Read(string url,string key,DateTime refreshAfter){
      string file=Path.Combine(Store.Data,"icons",Hash(key)+".img");
      ImageSource stale=null;try{if(File.Exists(file)){stale=Decode(File.ReadAllBytes(file));if(File.GetLastWriteTimeUtc(file)>DateTime.UtcNow.AddDays(-1)&&File.GetLastWriteTimeUtc(file)>=refreshAfter)return stale;}}catch{}
      await limit.WaitAsync().ConfigureAwait(false);try{
        using(var client=new HttpClient(new HttpClientHandler {UseProxy=false,AllowAutoRedirect=false})){
          client.Timeout=TimeSpan.FromSeconds(7);using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead)){response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>4194304)return null;
            using(var stream=await response.Content.ReadAsStreamAsync())using(var bytes=new MemoryStream())using(var deadline=new CancellationTokenSource(7000)){
              var buffer=new byte[8192];while(true){int read=await stream.ReadAsync(buffer,0,buffer.Length,deadline.Token);if(read==0)break;if(bytes.Length+read>4194304)return null;bytes.Write(buffer,0,read);}var data=bytes.ToArray();var source=Decode(data);
              try{Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,data);}catch{}return source;
            }
          }
        }
      }catch{return stale;}finally{limit.Release();}
    }
    static UIElement Fallback(string id,string title,double size){
      var panel=new Grid();string path=id=="devrun"?"M 8,7 L 3,12 L 8,17 M 16,7 L 21,12 L 16,17 M 14,5 L 10,19":id=="display_schedule"?"M 12,7 L 12,12 L 16,14 M 4,4 L 7,2 M 17,2 L 20,4":id=="hidpad"?"M 4,4 L 20,4 L 20,19 L 4,19 Z M 7,8 L 8,8 M 11,8 L 12,8 M 15,8 L 16,8 M 7,12 L 8,12 M 11,12 L 12,12 M 15,12 L 16,12 M 8,16 L 16,16":"";
      if(path!=""){var drawing=new DrawingGroup();var pen=new Pen(new SolidColorBrush(Color.FromRgb(64,117,171)),1.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(path)));if(id=="display_schedule")drawing.Children.Add(new GeometryDrawing(null,pen,new EllipseGeometry(new Point(12,12),8,8)));drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));panel.Children.Add(new Image {Source=new DrawingImage(drawing),Margin=new Thickness(size*.18),Stretch=Stretch.Uniform});}
      else{string name=string.IsNullOrWhiteSpace(title)?id:title;string text=name.Length>2?name.Substring(0,2):name;panel.Children.Add(new TextBlock {Text=text.ToUpperInvariant(),FontSize=Math.Max(10,size*.28),FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(64,117,171)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});}return panel;
    }
    public static FrameworkElement Create(string id,string title,double size=48,string address=null,bool installed=false,string iconUrl="",string version=""){
      var content=new Grid();var fallback=Fallback(id??"",title??"",size);content.Children.Add(fallback);var picture=new Image {Stretch=Stretch.Uniform,Margin=new Thickness(3)};content.Children.Add(picture);
      double radius=Math.Max(5,size*.2);var frame=new GlassSurface {Width=size,Height=size,Child=content,CornerRadius=new CornerRadius(radius),HaloSize=2,Background=new SolidColorBrush(Color.FromRgb(246,250,255)),ToolTip=title+" 图标"};content.Clip=new RectangleGeometry(new Rect(0,0,size,size),radius,radius);AutomationProperties.SetName(frame,title+" 图标");AutomationProperties.SetName(picture,title+" 图标图片");
      if(ValidId(id)){var local=Bundled(id);if(local!=null){picture.Source=local;fallback.Visibility=Visibility.Hidden;}Load(picture,fallback,Fetch(id,address,installed,iconUrl,version));}return frame;
    }
    static async void Load(Image picture,UIElement fallback,Task<ImageSource> load){try{var source=await load;if(source!=null&&!picture.Dispatcher.HasShutdownStarted){picture.Source=source;fallback.Visibility=Visibility.Hidden;}}catch{}}
  }
  public partial class MainWindow {
    FrameworkElement AppIcon(string id,string title,double size=48,string iconUrl=""){
      var app=J.List(state,"installed_apps").FirstOrDefault(a=>J.Str(a,"id").Equals(id,StringComparison.OrdinalIgnoreCase));return AppIcons.Create(id,title,size,online&&device!=null?device.Address:null,app!=null,iconUrl,J.Str(app,"version"));
    }
    FrameworkElement AppHeading(string id,string title,double font=20,double iconSize=48,string iconUrl=""){var text=Text(title,font,"#263C59",true);text.VerticalAlignment=VerticalAlignment.Center;return Row(AppIcon(id,title,iconSize,iconUrl),text);}
  }
}
