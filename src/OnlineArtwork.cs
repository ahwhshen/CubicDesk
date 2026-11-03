// 用途：补取在线专辑封面、转换 88×88 JPEG 并控制重试间隔。
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CubicDesk {
  public static class OnlineArtwork {
    public static bool Enabled {get{return J.Get(Store.Config,"onlineCoverEnabled")!=null?J.Bool(Store.Config,"onlineCoverEnabled"):OnlineLyrics.Enabled;}}
    public static string Failure(Exception ex){if(ex is HttpRequestException)return "外网请求失败，请检查电脑联网；恢复后会自动重试。";if(ex is OperationCanceledException)return "在线请求超时，稍后自动重试。";return ex.Message;}
    public static TimeSpan RetryDelay(int failures){return TimeSpan.FromSeconds(Math.Min(60,5*Math.Pow(2,Math.Min(4,Math.Max(0,failures-1)))));}
    public static byte[] Jpeg(byte[] bytes){using(var memory=new MemoryStream(bytes))using(var image=Image.FromStream(memory)){if(image.Width<1||image.Height<1||image.Width>4096||image.Height>4096)throw new Exception("在线封面尺寸异常。");using(var small=new Bitmap(88,88))using(var graphics=Graphics.FromImage(small))using(var result=new MemoryStream()){graphics.Clear(Color.Black);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.DrawImage(image,0,0,88,88);small.Save(result,ImageFormat.Jpeg);return result.ToArray();}}}
    public static async Task<byte[]> Download(HttpClient client,string url,CancellationToken token){using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>10485760)throw new Exception("在线封面超过 10 MB。");using(var input=await response.Content.ReadAsStreamAsync())using(var output=new MemoryStream())using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(12000);var buffer=new byte[8192];while(true){int n=await input.ReadAsync(buffer,0,buffer.Length,timeout.Token);if(n==0)break;if(output.Length+n>10485760)throw new Exception("在线封面超过 10 MB。");output.Write(buffer,0,n);}return Jpeg(output.ToArray());}}}
  }
}
