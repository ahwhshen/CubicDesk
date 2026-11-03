// 用途：将 Windows 系统媒体缩略图读取并转换为 88×88 JPEG。
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace CubicDesk {
  public static class SmtcThumbnail {
    [DllImport("shcore.dll",ExactSpelling=true)]
    static extern int CreateStreamOverRandomAccessStream(IntPtr randomAccessStream,ref Guid riid,out IStream stream);
    public static byte[] Jpeg(object randomAccessStream){
      if(randomAccessStream==null)throw new ArgumentNullException("randomAccessStream");
      IntPtr pointer=IntPtr.Zero,count=IntPtr.Zero;IStream stream=null;
      try{
        pointer=Marshal.GetIUnknownForObject(randomAccessStream);Guid iid=new Guid("0000000C-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CreateStreamOverRandomAccessStream(pointer,ref iid,out stream));
        stream.Seek(0,0,IntPtr.Zero);count=Marshal.AllocHGlobal(4);
        using(var data=new MemoryStream()){
          var buffer=new byte[16384];
          while(true){Marshal.WriteInt32(count,0);stream.Read(buffer,buffer.Length,count);int read=Marshal.ReadInt32(count);if(read==0)break;if(data.Length+read>10485760)throw new IOException("播放器封面超过 10 MB。");data.Write(buffer,0,read);}
          if(data.Length==0)throw new IOException("播放器封面暂时为空。");data.Position=0;
          using(var original=Image.FromStream(data))using(var small=new Bitmap(88,88))using(var graphics=Graphics.FromImage(small))using(var output=new MemoryStream()){
            graphics.Clear(Color.Black);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original,0,0,88,88);small.Save(output,ImageFormat.Jpeg);return output.ToArray();
          }
        }
      }finally{if(count!=IntPtr.Zero)Marshal.FreeHGlobal(count);if(stream!=null&&Marshal.IsComObject(stream))Marshal.ReleaseComObject(stream);if(pointer!=IntPtr.Zero)Marshal.Release(pointer);}
    }
  }
}
