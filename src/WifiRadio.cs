using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CubicDesk {
  public sealed class WifiNetwork {
    public Guid InterfaceId;public string Ssid="",Profile="";public uint Signal,Authentication,Cipher;public bool Secured,Connected;
  }
  public sealed class WifiLink {
    public Guid InterfaceId;public string Ssid="",Profile="";public bool Connected;
  }
  public static class WifiRadio {
    public const string DevicePrefix="clocteck-cubic";
    public static bool IsDeviceHotspot(string ssid){return (ssid??"").StartsWith(DevicePrefix,StringComparison.OrdinalIgnoreCase);}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct InterfaceInfo {public Guid Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Description;public int State;}
    [StructLayout(LayoutKind.Sequential)] struct Ssid {public uint Length;[MarshalAs(UnmanagedType.ByValArray,SizeConst=32)]public byte[] Bytes;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct AvailableNetwork {
      [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Profile;public Ssid Ssid;public int Bss;public uint Bssids;public int Connectable;public uint Reason,PhyCount;
      [MarshalAs(UnmanagedType.ByValArray,SizeConst=8)]public uint[] Phy;public int MorePhy;public uint Signal;public int Security;public uint Auth,Cipher,Flags,Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] struct ConnectionParameters {public int Mode;public IntPtr Profile,Ssid,Bssids;public int Bss;public uint Flags;}
    [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint negotiated,out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr handle,IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr handle,IntPtr reserved,out IntPtr list);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")] static extern uint WlanScan(IntPtr handle,ref Guid id,IntPtr ssid,IntPtr ie,IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanGetAvailableNetworkList(IntPtr handle,ref Guid id,uint flags,IntPtr reserved,out IntPtr list);
    [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr handle,ref Guid id,int opcode,IntPtr reserved,out uint size,out IntPtr data,out int type);
    [DllImport("wlanapi.dll")] static extern uint WlanConnect(IntPtr handle,ref Guid id,ref ConnectionParameters parameters,IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanDisconnect(IntPtr handle,ref Guid id,IntPtr reserved);
    static void Check(uint code,string action){if(code==0)return;if(code==5)throw new Exception(action+"：Windows 拒绝访问 Wi-Fi。请在系统设置中检查此桌面应用的 WLAN／位置访问权限，再重试。");throw new Exception(action+"："+new Win32Exception((int)code).Message+"（"+code+"）");}
    static IntPtr Open(){uint version;IntPtr handle;Check(WlanOpenHandle(2,IntPtr.Zero,out version,out handle),"打开 Windows Wi-Fi");return handle;}
    static List<InterfaceInfo> Interfaces(IntPtr handle){IntPtr list;Check(WlanEnumInterfaces(handle,IntPtr.Zero,out list),"读取无线网卡");try{int count=Marshal.ReadInt32(list);if(count<0||count>64)throw new Exception("无线网卡列表异常。");var result=new List<InterfaceInfo>();for(int i=0;i<count;i++)result.Add((InterfaceInfo)Marshal.PtrToStructure(IntPtr.Add(list,8+i*Marshal.SizeOf(typeof(InterfaceInfo))),typeof(InterfaceInfo)));return result;}finally{WlanFreeMemory(list);}}
    static string Text(Ssid ssid){return ssid.Length<=32&&ssid.Bytes!=null?Encoding.UTF8.GetString(ssid.Bytes,0,(int)ssid.Length):"";}
    public static async Task<List<WifiNetwork>> Scan(CancellationToken token){
      IntPtr handle=Open();try{var adapters=Interfaces(handle);if(adapters.Count==0)throw new Exception("未找到无线网卡。请开启电脑 Wi-Fi 后重新扫描。");var available=new List<InterfaceInfo>();var errors=new List<string>();
        foreach(var adapter in adapters){token.ThrowIfCancellationRequested();var id=adapter.Id;uint code=WlanScan(handle,ref id,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(code==0)available.Add(adapter);else try{Check(code,"扫描附近 Wi-Fi");}catch(Exception ex){errors.Add(ex.Message);}}
        if(available.Count==0)throw new Exception(string.Join("；",errors));await Task.Delay(3500,token);var result=new List<WifiNetwork>();
        foreach(var adapter in available){token.ThrowIfCancellationRequested();var id=adapter.Id;IntPtr list;uint code=WlanGetAvailableNetworkList(handle,ref id,0,IntPtr.Zero,out list);if(code!=0){try{Check(code,"读取附近 Wi-Fi");}catch(Exception ex){errors.Add(ex.Message);}continue;}
          try{int count=Marshal.ReadInt32(list);if(count<0||count>4096)throw new Exception("Wi-Fi 列表异常。");for(int i=0;i<count;i++){var item=(AvailableNetwork)Marshal.PtrToStructure(IntPtr.Add(list,8+i*Marshal.SizeOf(typeof(AvailableNetwork))),typeof(AvailableNetwork));string ssid=Text(item.Ssid);if(ssid!="")result.Add(new WifiNetwork {InterfaceId=id,Ssid=ssid,Profile=item.Profile,Signal=item.Signal,Secured=item.Security!=0,Authentication=item.Auth,Cipher=item.Cipher,Connected=(item.Flags&1)!=0});}}finally{WlanFreeMemory(list);}
        }if(result.Count==0&&errors.Count>0)throw new Exception(string.Join("；",errors));return result.OrderByDescending(n=>n.Signal).ToList();
      }finally{WlanCloseHandle(handle,IntPtr.Zero);}
    }
    public static WifiLink Current(Guid id){IntPtr handle=Open();try{return Current(handle,id);}finally{WlanCloseHandle(handle,IntPtr.Zero);}}
    static WifiLink Current(IntPtr handle,Guid id){uint size;IntPtr data;int type;uint code=WlanQueryInterface(handle,ref id,7,IntPtr.Zero,out size,out data,out type);if(code==5023)return new WifiLink {InterfaceId=id};Check(code,"读取当前 Wi-Fi");try{
      if(size<556)throw new Exception("Windows 返回了不完整的 Wi-Fi 连接信息。");int state=Marshal.ReadInt32(data);uint length=(uint)Marshal.ReadInt32(data,520);if(length>32)throw new Exception("Wi-Fi 名称长度异常。");var bytes=new byte[length];Marshal.Copy(IntPtr.Add(data,524),bytes,0,(int)length);return new WifiLink {InterfaceId=id,Connected=state==1,Profile=Marshal.PtrToStringUni(IntPtr.Add(data,8),256).TrimEnd('\0'),Ssid=Encoding.UTF8.GetString(bytes)};
    }finally{WlanFreeMemory(data);}}
    public static string ProfileXml(WifiNetwork network,string password){
      byte[] bytes=Encoding.UTF8.GetBytes(network.Ssid??"");if(bytes.Length==0||bytes.Length>32)throw new Exception("Wi-Fi 名称需要为 1–32 个 UTF-8 字节。");string auth="open",cipher="none",key="";
      if(network.Secured){if(network.Authentication!=7||network.Cipher!=4)throw new Exception("该摆件热点的加密方式暂不支持自动连接，请先在 Windows 中连接这个热点，再按地址配网。");if(password==null||password.Length<8||password.Length>63)throw new Exception("请输入摆件热点的 8–63 位密码。");auth="WPA2PSK";cipher="AES";key="<sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>"+SecurityElement.Escape(password)+"</keyMaterial></sharedKey>";}
      string hex=BitConverter.ToString(bytes).Replace("-","");return "<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>CubicDesk-Temporary</name><SSIDConfig><SSID><hex>"+hex+"</hex></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>"+auth+"</authentication><encryption>"+cipher+"</encryption><useOneX>false</useOneX></authEncryption>"+key+"</security></MSM></WLANProfile>";
    }
    static void BeginConnect(IntPtr handle,Guid id,string profile,bool temporary){IntPtr text=Marshal.StringToHGlobalUni(profile);try{var parameters=new ConnectionParameters {Mode=temporary?1:0,Profile=text,Bss=1};Check(WlanConnect(handle,ref id,ref parameters,IntPtr.Zero),"连接 Wi-Fi");}finally{Marshal.FreeHGlobal(text);}}
    public static async Task Join(WifiNetwork network,string password,CancellationToken token){IntPtr handle=Open();try{var current=Current(handle,network.InterfaceId);if(current.Connected&&current.Ssid==network.Ssid)return;string xml=ProfileXml(network,password);BeginConnect(handle,network.InterfaceId,xml,true);for(int i=0;i<25;i++){await Task.Delay(1000,token);var now=Current(handle,network.InterfaceId);if(now.Connected&&now.Ssid==network.Ssid)return;}throw new Exception("连接摆件热点超时，请检查热点密码、信号及 Windows 的连接提示。");}finally{WlanCloseHandle(handle,IntPtr.Zero);}}
    public static async Task Restore(WifiLink previous){IntPtr handle=Open();try{
      var now=Current(handle,previous.InterfaceId);if(now.Connected==previous.Connected&&now.Ssid==previous.Ssid)return;
      if(!previous.Connected){var id=previous.InterfaceId;Check(WlanDisconnect(handle,ref id,IntPtr.Zero),"断开临时摆件热点");return;}
      if(string.IsNullOrEmpty(previous.Profile))throw new Exception("原 Wi-Fi 没有可恢复的已保存配置，请在 Windows 中重新连接原网络。");BeginConnect(handle,previous.InterfaceId,previous.Profile,false);
      for(int i=0;i<20;i++){await Task.Delay(1000);now=Current(handle,previous.InterfaceId);if(now.Connected&&now.Ssid==previous.Ssid)return;}throw new Exception("恢复原 Wi-Fi 超时，请在 Windows 中重新连接“"+previous.Ssid+"”。");
    }finally{WlanCloseHandle(handle,IntPtr.Zero);}}
    public static IEnumerable<string> Gateways(Guid id){foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()){Guid nicId;if(!Guid.TryParse(nic.Id,out nicId)||nicId!=id)continue;foreach(var gateway in nic.GetIPProperties().GatewayAddresses)if(gateway.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&DeviceClient.Private(gateway.Address))yield return gateway.Address.ToString();}}
  }
}
