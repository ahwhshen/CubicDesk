// 用途：管理资源上传下载、图片适配与 FFmpeg 视频转码。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CubicDesk {
  public partial class MainWindow {
    void BuildMedia(){
      var categories=new Dictionary<string,string>{{"静态图片","/sd/images"},{"GIF 动画","/sd/gifs"},{"视频","/sd/videos"},{"音乐与歌词","/sd/mp3"},{"游戏 ROM","/sd/roms"}};
      var category=Select(categories.Keys);var search=Input();var list=new StackPanel();var summary=Text("请选择分类后刷新文件列表。",12,"#687E98");var location=Text("/sd/images",12,"#276FC0");string directory="/sd/images";var entries=new List<Dictionary<string,object>>();
      Action render=null;Func<Task> load=null;
      render=delegate{list.Children.Clear();foreach(var f in entries.Where(f=>J.Str(f,"name").IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0)){
        string filePath=J.Str(f,"path"),name=J.Str(f,"name");bool dir=J.Bool(f,"is_dir");
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var title=Stack(Text((dir?"▣  ":"▧  ")+name,14,"#263C59",true),Text(dir?"文件夹":MediaTools.Size(J.Num(f,"size")),11,"#687E98"));title.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(title);
        StackPanel actions;
        if(dir)actions=Row(Button("打开",async delegate{directory=filePath;await load();}));
        else actions=Row(Button("下载",async delegate{var dialog=new SaveFileDialog {FileName=name,Title="保存摆件资源"};if(dialog.ShowDialog(this)!=true)return;byte[] bytes=await device.Download(device.FileRoute(filePath));await Task.Run(delegate{File.WriteAllBytes(dialog.FileName,bytes);});Notice("已下载 "+name+"。");}),Button("删除",async delegate{if(MessageBox.Show("从摆件删除文件“"+name+"”？此操作无法撤销。","删除摆件资源",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;await device.Request("/api/system/fs/remove?path="+Uri.EscapeDataString(filePath),"DELETE");await load();Notice("已删除 "+name+"。");}));
        Grid.SetColumn(actions,1);grid.Children.Add(actions);list.Children.Add(Card(grid,14));
      }};
      load=async delegate{
        var response=await device.Request("/api/system/fs/list?path="+Uri.EscapeDataString(directory));entries=J.List(response,"entries").OrderByDescending(f=>J.Bool(f,"is_dir")).ThenBy(f=>J.Str(f,"name")).ToList();location.Text=directory;summary.Text=entries.Count+" 个项目 · "+MediaTools.Size(entries.Sum(f=>J.Num(f,"size")));render();
      };
      category.SelectionChanged+=async delegate{if(category.SelectedItem==null)return;directory=categories[Convert.ToString(category.SelectedItem)];await Run(async delegate{await load();Notice("资源列表已刷新。");},"正在读取资源…");};search.TextChanged+=delegate{render();};
      var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(180)});top.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(16)});top.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});top.Children.Add(category);Grid.SetColumn(search,2);top.Children.Add(search);page.Children.Add(top);page.Children.Add(Gap(12));
      page.Children.Add(Row(Button("上传资源",async delegate{
        string type=Convert.ToString(category.SelectedItem);string target=directory;
        if(type=="游戏 ROM"&&target=="/sd/roms")throw new Exception("请先打开对应的游戏平台文件夹，再上传 ROM。");
        var dialog=new OpenFileDialog {Multiselect=true,Title="上传到 "+target};
        dialog.Filter=type=="静态图片"?"图片|*.png;*.jpg;*.jpeg;*.bmp":type=="GIF 动画"?"GIF 动画|*.gif":type=="视频"?"视频|*.avi;*.mjpeg;*.mjpg;*.mp4;*.mov;*.m4v":type=="音乐与歌词"?"音乐与歌词|*.mp3;*.wav;*.lrc":"游戏 ROM|*.nes;*.gb;*.gbc;*.md;*.gen;*.gg;*.sms;*.pce;*.lnx;*.col;*.gw";
        if(dialog.ShowDialog(this)!=true)return;
        int done=0;var uploadedPaths=new HashSet<string>(entries.Select(f=>J.Str(f,"path")),StringComparer.OrdinalIgnoreCase);
        foreach(string source in dialog.FileNames){
          string name=Path.GetFileName(source),ext=Path.GetExtension(source).ToLowerInvariant(),prepared=source;bool temporary=false;byte[] bytes;
          Notice("正在处理 "+name+"（"+(done+1)+"/"+dialog.FileNames.Length+"）…");
          if(new FileInfo(source).Length>200L*1024*1024)throw new Exception(name+" 超过 200 MB，请先缩小文件。");
          try{
            if(type=="静态图片"){bytes=MediaTools.ResizeImage(source);name=Path.GetFileNameWithoutExtension(name)+".png";}
            else {
              if(type=="视频"&&new[]{".mp4",".mov",".m4v"}.Contains(ext)){prepared=await MediaTools.ConvertVideo(source);name=Path.GetFileNameWithoutExtension(name)+".avi";temporary=true;}
              if(type=="GIF 动画"&&new FileInfo(source).Length>50L*1024*1024)throw new Exception("GIF 最大支持 50 MB。");
              bytes=await Task.Run(delegate{return File.ReadAllBytes(prepared);});
            }
            string uploadPath=target+"/"+name;
            if(uploadedPaths.Contains(uploadPath)){if(MessageBox.Show("摆件中已有“"+name+"”。覆盖它？","覆盖资源",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)continue;await device.BackupAndPut(uploadPath,bytes,"application/octet-stream");}
            else await device.Put("/api/system/fs/upload?path="+Uri.EscapeDataString(uploadPath),bytes);
            uploadedPaths.Add(uploadPath);
            done++;
          }finally{if(temporary&&File.Exists(prepared))File.Delete(prepared);}
        }
        await load();Notice("已上传 "+done+" 个文件。");
      },true),Button("刷新列表",async delegate{await load();Notice("资源列表已刷新。");}),Button("上一级",async delegate{if(directory==categories[Convert.ToString(category.SelectedItem)])return;directory=directory.Substring(0,directory.LastIndexOf('/'));await load();})));
      page.Children.Add(Gap(16));page.Children.Add(location);page.Children.Add(summary);page.Children.Add(Gap(8));page.Children.Add(Text("图片自动适配 320 × 240；MP4 / MOV 最长 10 分钟，自动转为无声 MJPEG AVI，无需额外配置。GIF 直接上传。",11,"#687E98"));page.Children.Add(Gap(16));page.Children.Add(list);LoadMedia(load);
    }
    async void LoadMedia(Func<Task> load){try{await load();}catch(Exception ex){Notice("读取资源失败："+ex.Message,true);}}
  }
  public static class MediaTools {
    public static string Size(double bytes){return bytes<1024?bytes.ToString("0")+" B":bytes<1048576?(bytes/1024).ToString("0.#")+" KB":(bytes/1048576).ToString("0.#")+" MB";}
    public static byte[] ResizeImage(string path){
      var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=640;bitmap.UriSource=new Uri(path);bitmap.EndInit();bitmap.Freeze();
      double ratio=Math.Min(320.0/bitmap.PixelWidth,240.0/bitmap.PixelHeight);double width=bitmap.PixelWidth*ratio,height=bitmap.PixelHeight*ratio;
      var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawImage(bitmap,new Rect((320-width)/2,(240-height)/2,width,height));}
      var rendered=new RenderTargetBitmap(320,240,96,96,PixelFormats.Pbgra32);rendered.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(rendered));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
    }
    public static string FindFFmpeg(){string path=Path.Combine(Store.Root,"tools","ffmpeg","ffmpeg.exe");return File.Exists(path)?path:"";}
    public static async Task VerifyFFmpeg(string path){if(!File.Exists(path)||!Path.GetFileName(path).Equals("ffmpeg.exe",StringComparison.OrdinalIgnoreCase))throw new Exception("内置媒体组件缺失，请重新解压完整便携包。");string output=await Execute(path,"-version",10000);if(!output.StartsWith("ffmpeg version",StringComparison.OrdinalIgnoreCase))throw new Exception("内置媒体组件没有返回有效版本信息。");if(!File.Exists(Path.Combine(Path.GetDirectoryName(path),"ffprobe.exe")))throw new Exception("内置视频检测组件缺失，请重新解压完整便携包。");}
    static string Quote(string s){if(s.IndexOf('"')>=0||s.IndexOf('\n')>=0||s.IndexOf('\r')>=0)throw new Exception("文件路径包含无效字符。");return "\""+s+"\"";}
    public static async Task<string> ConvertVideo(string source){string executable=FindFFmpeg();if(executable=="")throw new Exception("内置媒体组件缺失，请重新解压完整便携包。");await VerifyFFmpeg(executable);string probe=Path.Combine(Path.GetDirectoryName(executable),"ffprobe.exe");string durationText=await Execute(probe,"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "+Quote(source),15000);double duration;if(!double.TryParse(durationText.Trim(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out duration)||duration<=0)throw new Exception("无法读取视频时长，请先转换为标准 MP4。");if(duration>600)throw new Exception("视频超过 10 分钟，请缩短后上传。");string dir=Path.Combine(Store.Data,"temp");Directory.CreateDirectory(dir);string target=Path.Combine(dir,Guid.NewGuid().ToString("N")+".avi");try{await Execute(executable,"-nostdin -hide_banner -loglevel error -i "+Quote(source)+" -vf \"scale=320:240:force_original_aspect_ratio=decrease,pad=320:240:(ow-iw)/2:(oh-ih)/2\" -r 20 -an -c:v mjpeg -q:v 6 -y "+Quote(target),180000);if(new FileInfo(target).Length>200L*1024*1024)throw new Exception("转码结果超过 200 MB，请缩短视频后重试。");return target;}catch{if(File.Exists(target))File.Delete(target);throw;}}
    public static async Task<string> Execute(string executable,string arguments,int timeout){return await Task.Run(delegate{
      var info=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var p=Process.Start(info)){var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(timeout)){p.Kill();throw new Exception("程序执行超时。");}string output=stdout.Result,error=stderr.Result;if(p.ExitCode!=0)throw new Exception("媒体处理失败："+error.Substring(0,Math.Min(500,error.Length)));return output;}
    });}
  }
}
