// 用途：提供开机启动方式选择与后台连接状态界面。
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CubicDesk {
  public partial class MainWindow {
    public string BackgroundSummary {get{return online?"摆件已连接 · "+(device==null?"":device.Address):"后台运行 · 等待摆件连接";}}
    void BuildBackground(){
      var startup=LoginStartupMode.Off;string error="";try{startup=LoginStartup.Mode;}catch(Exception ex){error=ex.Message;}
      var mode=Select(new[]{"关闭","前端","仅后端"});mode.SelectedIndex=(int)startup;mode.Margin=new Thickness(0,14,0,10);
      var result=Text(error!=""?error:"已保存："+LoginStartup.Label(startup),12,"#276FC0");
      var card=Stack(Text("开机启动",20,"#263C59",true),Gap(8),Text("关闭：不随登录启动。前端：启动并打开管理窗口。仅后端：静默在托盘运行。两种启动方式都会保留桥接偏好。",12,"#687E98"),mode,result,Gap(14),Button("保存开机启动设置",delegate{LoginStartup.SetMode((LoginStartupMode)mode.SelectedIndex);result.Text="已保存："+LoginStartup.Label(LoginStartup.Mode);Notice("开机启动设置已保存并核对。");return Task.FromResult(0);},true),Gap(12),Text("移动便携程序文件夹后，请重新打开本页并保存，以更新启动位置。电脑睡眠、关机或 Windows 注销后，后台无法继续运行。",11,"#687E98"));page.Children.Add(Card(card));
      var status=Stack(Text("后台连接",20,"#263C59",true),Gap(10),Text(BackgroundSummary,14,"#276FC0"),Gap(8),Text("摆件暂时离线时，后台会继续检查已记住的地址。网络恢复后重新连接；不会自动修改热点名称、密码或防火墙。",12,"#687E98"));page.Children.Add(Card(status));
    }
  }
}
