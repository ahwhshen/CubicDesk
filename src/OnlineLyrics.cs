// 用途：选择在线歌词来源并接入网易云歌词与专辑封面。
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CubicDesk {
  public static class OnlineLyrics {
    public static bool Enabled {get{return J.Get(Store.Config,"onlineLyricsEnabled")!=null?J.Bool(Store.Config,"onlineLyricsEnabled"):J.Bool(Store.Config,"qqLyricsEnabled");}}
    public static string Provider {get{string value=J.Str(Store.Config,"lyricProvider","auto");return new[]{"auto","qq","netease"}.Contains(value)?value:"auto";}}
    public static string Resolve(string preference,string source){if(preference=="qq"||preference=="netease")return preference;string app=(source??"").ToLowerInvariant();return app.Contains("qqmusic")?"qq":app.Contains("cloudmusic")||app.Contains("netease")?"netease":"";}
    public static string Label(string provider){return provider=="qq"?"QQ 音乐":provider=="netease"?"网易云音乐":"自动跟随播放器";}
    public static Task<OnlineLyricResult> Fetch(string provider,string title,string artist,double duration,CancellationToken token){if(provider=="qq")return QQMusicLyrics.Fetch(title,artist,duration,token);if(provider=="netease")return NetEaseLyrics.Fetch(title,artist,duration,token);throw new Exception("请为当前播放器选择歌词来源。");}
  }
  public static class NetEaseLyrics {
    static async Task<Dictionary<string,object>> Get(HttpClient client,string url,CancellationToken token){using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)){response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>2097152)throw new Exception("网易云返回内容过大。");string text=await response.Content.ReadAsStringAsync();token.ThrowIfCancellationRequested();if(text.Length>2097152)throw new Exception("网易云返回内容过大。");Dictionary<string,object> data;try{data=J.Parse(text);}catch{throw new Exception("网易云返回内容无法解析。");}if(J.Num(data,"code")!=200)throw new Exception("网易云接口返回："+J.Str(data,"message",J.Str(data,"code")));return data;}}
    public static Dictionary<string,object> MatchRecord(Dictionary<string,object> song){return J.D("songname",J.Str(song,"name"),"singer",J.Get(song,"artists")??J.Get(song,"ar"),"interval",J.Num(song,"duration",J.Num(song,"dt"))/1000.0);}
    public static async Task<byte[]> FetchCover(string title,string artist,double duration,CancellationToken token){using(var client=new HttpClient(new HttpClientHandler {UseProxy=true})){client.Timeout=TimeSpan.FromSeconds(12);client.DefaultRequestHeaders.Referrer=new Uri("https://music.163.com/");client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 CubicDesk/1.5.1");string query=QQMusicLyrics.BaseTitle(title).Trim()+" "+Regex.Replace(artist??"",@"[/、;]"," ").Trim();var search=await Get(client,"https://music.163.com/api/search/get?type=1&limit=20&s="+Uri.EscapeDataString(query),token);var song=J.List(J.Map(search,"result"),"songs").Select(s=>new {Song=s,Score=QQMusicLyrics.Match(MatchRecord(s),title,artist,duration)}).Where(s=>s.Score>0).OrderByDescending(s=>s.Score).Select(s=>s.Song).FirstOrDefault();if(song==null)throw new Exception("未找到匹配的网易云封面。");string url=J.Str(J.Map(song,"album"),"picUrl");if(url==""){string album=J.Str(J.Map(song,"album"),"id");if(Regex.IsMatch(album,@"^[0-9]{1,20}$")){var detail=await Get(client,"https://music.163.com/api/album/"+album,token);url=J.Str(J.Map(detail,"album"),"picUrl");}}Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||!(uri.Host.EndsWith(".126.net",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".163.com",StringComparison.OrdinalIgnoreCase)))throw new Exception("网易云未提供可用的专辑封面地址。");var secure=new UriBuilder(uri){Scheme="https",Port=-1};return await OnlineArtwork.Download(client,secure.Uri.AbsoluteUri,token);}}
    public static async Task<OnlineLyricResult> Fetch(string title,string artist,double duration,CancellationToken token){
      using(var client=new HttpClient(new HttpClientHandler {UseProxy=true})){
        client.Timeout=TimeSpan.FromSeconds(12);client.DefaultRequestHeaders.Referrer=new Uri("https://music.163.com/");client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 CubicDesk/1.4.0");
        string query=QQMusicLyrics.BaseTitle(title).Trim()+" "+Regex.Replace(artist??"",@"[/、;]"," ").Trim();
        var search=await Get(client,"https://music.163.com/api/search/get?type=1&limit=20&s="+Uri.EscapeDataString(query),token);
        var song=J.List(J.Map(search,"result"),"songs").Select(s=>new {Song=s,Score=QQMusicLyrics.Match(MatchRecord(s),title,artist,duration)}).Where(s=>s.Score>0).OrderByDescending(s=>s.Score).Select(s=>s.Song).FirstOrDefault();
        if(song==null)throw new Exception("未找到歌名、歌手和时长相符的网易云歌词；可切换来源或导入本地 LRC。");string id=J.Str(song,"id");if(!Regex.IsMatch(id,@"^[0-9]{1,20}$"))throw new Exception("网易云歌曲标识无效。");
        var response=await Get(client,"https://music.163.com/api/song/lyric?id="+id+"&lv=-1&kv=-1&tv=-1",token);var lines=MusicBridge.ParseLrc(J.Str(J.Map(response,"lrc"),"lyric"));if(lines.Count==0)throw new Exception("网易云未提供这首歌的同步歌词。");
        return new OnlineLyricResult {Provider="netease",SongId=id,Title=J.Str(song,"name"),Artist=string.Join(" / ",J.List(song,"artists").Select(a=>J.Str(a,"name"))),Lines=lines};
      }
    }
  }
}
