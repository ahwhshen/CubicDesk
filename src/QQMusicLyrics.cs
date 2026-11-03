// 用途：按歌名歌手和时长匹配 QQ 音乐歌词与专辑封面。
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using System.Collections.Generic;

namespace CubicDesk {
  public sealed class OnlineLyricResult {
    public string SongId,Title,Artist,Provider;
    public List<Dictionary<string,object>> Lines;
  }
  public static class QQMusicLyrics {
    static string Plain(string value){return HttpUtility.HtmlDecode(value??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant();}
    static string Key(string value){return Regex.Replace(Plain(value),@"[^\p{L}\p{N}]","");}
    public static string BaseTitle(string value){return Regex.Replace(Plain(value),@"\([^)]*\)|（[^）]*）","");}
    public static int Match(Dictionary<string,object> song,string title,string artist,double duration){
      string name=J.Str(song,"songname");int score=Key(name)==Key(title)?100:Key(BaseTitle(name))==Key(BaseTitle(title))?80:0;if(score==0)return 0;
      var singers=J.List(song,"singer").SelectMany(s=>new[]{Key(J.Str(s,"name")),Key(BaseTitle(J.Str(s,"name")))}).Where(s=>s.Length>0).ToList();var current=Regex.Split(artist??"",@"[/、;&]").SelectMany(s=>new[]{Key(s),Key(BaseTitle(s))}).Where(s=>s.Length>0).ToList();
      if(current.Count==0||singers.Count==0||!singers.Any(s=>current.Any(c=>c==s||s.Length>1&&c.Length>1&&(c.Contains(s)||s.Contains(c)))))return 0;
      double seconds=J.Num(song,"interval");if(duration>0&&seconds>0){double difference=Math.Abs(duration-seconds*1000);if(difference>5000)return 0;score+=(int)Math.Max(0,10-difference/500);}return score;
    }
    internal static HttpClient Client(){var client=new HttpClient(new HttpClientHandler {UseProxy=true});client.Timeout=TimeSpan.FromSeconds(12);client.DefaultRequestHeaders.Referrer=new Uri("https://y.qq.com/");client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 CubicDesk/1.5.1");return client;}
    internal static async Task<Dictionary<string,object>> Get(HttpClient client,string url,CancellationToken token){
      using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){
        response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>2097152)throw new Exception("QQ 音乐返回内容过大。");string text=await response.Content.ReadAsStringAsync();token.ThrowIfCancellationRequested();if(text.Length>2097152)throw new Exception("QQ 音乐返回内容过大。");
        Dictionary<string,object> data;try{data=J.Parse(text);}catch{throw new Exception("QQ 音乐返回内容无法解析。");}
        if(J.Num(data,"code",-1)!=0)throw new Exception("QQ 音乐接口返回："+J.Str(data,"message",J.Str(data,"code")));return data;
      }
    }
    static async Task<Dictionary<string,object>> FindSong(HttpClient client,string title,string artist,double duration,CancellationToken token){
      string query=Regex.Replace(BaseTitle(title),@"\s+"," ").Trim()+" "+Regex.Replace(BaseTitle(artist),@"\s+"," ").Trim();
      var search=await Get(client,"https://c.y.qq.com/soso/fcgi-bin/client_search_cp?format=json&p=1&n=20&w="+Uri.EscapeDataString(query),token);
      var song=J.List(J.Map(J.Map(search,"data"),"song"),"list").Select(s=>new {Song=s,Score=Match(s,title,artist,duration)}).Where(s=>s.Score>0).OrderByDescending(s=>s.Score).Select(s=>s.Song).FirstOrDefault();
      if(song==null)throw new Exception("未找到歌名、歌手和时长相符的 QQ 音乐歌曲；将稍后自动重试，也可导入本地 LRC。");return song;
    }
    public static async Task<byte[]> FetchCover(string title,string artist,double duration,CancellationToken token){using(var client=Client()){var song=await FindSong(client,title,artist,duration,token);string album=J.Str(song,"albummid");if(!Regex.IsMatch(album,@"^[A-Za-z0-9]{1,50}$"))throw new Exception("QQ 音乐未提供这首歌的专辑封面。");return await OnlineArtwork.Download(client,"https://y.gtimg.cn/music/photo_new/T002R300x300M000"+album+".jpg",token);}}
    public static async Task<OnlineLyricResult> Fetch(string title,string artist,double duration,CancellationToken token){
      using(var client=Client()){
        var song=await FindSong(client,title,artist,duration,token);
        string id=J.Str(song,"songmid");if(!Regex.IsMatch(id,@"^[A-Za-z0-9]{1,50}$"))throw new Exception("QQ 音乐歌曲标识无效。");
        var response=await Get(client,"https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?format=json&nobase64=1&songmid="+id,token);string lrc=HttpUtility.HtmlDecode(J.Str(response,"lyric"));
        if(!lrc.Contains("[")){try{lrc=Encoding.UTF8.GetString(Convert.FromBase64String(lrc));}catch{}}
        var lines=MusicBridge.ParseLrc(lrc);if(lines.Count==0)throw new Exception("QQ 音乐未提供这首歌的同步歌词。");
        return new OnlineLyricResult {Provider="qq",SongId=id,Title=HttpUtility.HtmlDecode(J.Str(song,"songname")),Artist=string.Join(" / ",J.List(song,"singer").Select(s=>J.Str(s,"name"))),Lines=lines};
      }
    }
  }
}
