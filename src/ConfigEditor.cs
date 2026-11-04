// 用途：将简单 JSON 和 Lua 配置转成可备份保存的原生表单。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CubicDesk {
  public sealed class ConfigEditor : Window {
    readonly DeviceClient device;readonly string appId;
    readonly ComboBox fileList=new ComboBox();readonly StackPanel fields=new StackPanel();readonly TextBlock status=new TextBlock {Margin=new Thickness(0,10,0,10)};
    readonly List<ConfigField> values=new List<ConfigField>();
    string original="",path="";bool saving;
    public ConfigEditor(DeviceClient client,string id,string title){
      device=client;appId=id;Title=title+" · 配置";Width=660;Height=700;MinWidth=500;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterOwner;
      Style=(Style)FindResource(typeof(Window));
      var root=new DockPanel {Margin=new Thickness(24)};Content=root;
      var top=new StackPanel();var heading=new StackPanel {Orientation=Orientation.Horizontal};var icon=AppIcons.Create(id,title,44,client.Address,true);icon.Margin=new Thickness(0,0,12,0);heading.Children.Add(icon);heading.Children.Add(new TextBlock {Text=title+" 的设置",FontSize=23,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});top.Children.Add(heading);top.Children.Add(new TextBlock {Text="原生表单编辑；保存前备份原文件，并检查配置是否被其他程序修改。",Foreground=new SolidColorBrush(Color.FromRgb(146,156,170)),Margin=new Thickness(0,8,0,18)});top.Children.Add(fileList);top.Children.Add(status);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
      var bottom=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,18,0,0)};var save=new Button {Content="保存配置",Style=(Style)FindResource("Primary"),Margin=new Thickness(0,0,10,0)};save.Click+=async delegate{await Save();};var restart=new Button {Content="重新打开应用"};restart.Click+=async delegate{try{await device.Request("/api/system/launch?id="+Uri.EscapeDataString(appId),"POST");status.Text="已重新打开应用。";}catch(Exception ex){status.Text=ex.Message;}};bottom.Children.Add(save);bottom.Children.Add(restart);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);root.Children.Add(new ScrollViewer {Content=fields});
      fileList.SelectionChanged+=async delegate{if(fileList.SelectedItem!=null)await LoadSelected();};GlassWindow.Attach(this,root);
    }
    public async Task LoadFiles(){
      var data=await device.Request("/api/system/fs/list?path="+Uri.EscapeDataString("/sd/apps/"+appId));
      foreach(var f in J.List(data,"entries")){string name=J.Str(f,"name");if(!J.Bool(f,"is_dir")&&(name.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||name.Equals("config.lua",StringComparison.OrdinalIgnoreCase)||name.Equals("settings.lua",StringComparison.OrdinalIgnoreCase)))fileList.Items.Add(name);}
      if(fileList.Items.Count>0)fileList.SelectedIndex=0;else status.Text="此应用没有可直接编辑的 JSON 或 config.lua 配置文件。它可能只通过设备按键配置。";
    }
    async Task LoadSelected(){
      fileList.IsEnabled=false;
      try{path="/sd/apps/"+appId+"/"+fileList.SelectedItem;original=await device.Text(device.FileRoute(path));values.Clear();fields.Children.Clear();
        if(path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)){var root=J.Parse(original);AddJson(root,new string[0]);}
        else foreach(var field in ConfigField.ParseLua(original))AddField(field);
        status.Text=values.Count>0?"已读取 "+values.Count+" 个设置。保存后可重新打开应用，使新配置生效。":"此文件没有可安全转换为表单的简单设置。原文件保持原样。";
      }catch(Exception ex){status.Text="读取失败："+ex.Message;}finally{fileList.IsEnabled=!saving;}
    }
    void AddJson(Dictionary<string,object> source,string[] prefix){foreach(var item in source){string[] keys=prefix.Concat(new[]{item.Key}).ToArray();var map=item.Value as Dictionary<string,object>;if(map!=null){AddJson(map,keys);continue;}if(item.Value is string||item.Value is bool||ConfigField.IsNumber(item.Value))AddField(new ConfigField {Key=string.Join(".",keys),Value=item.Value,JsonPath=keys});}}
    void AddField(ConfigField field){
      values.Add(field);fields.Children.Add(new TextBlock {Text=Label(field.Key),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,7)});
      if(field.Value is bool){var c=new CheckBox {Content="启用",IsChecked=(bool)field.Value};field.Control=c;fields.Children.Add(c);}else{var t=new TextBox {Text=Convert.ToString(field.Value,CultureInfo.InvariantCulture)};field.Control=t;fields.Children.Add(t);}
      fields.Children.Add(new TextBlock {Text=field.Key,FontSize=10,Foreground=new SolidColorBrush(Color.FromRgb(124,138,155)),Margin=new Thickness(0,3,0,0)});
    }
    static string Label(string key){var names=new Dictionary<string,string>{{"host","电脑地址"},{"port","端口"},{"poll_ms","轮询间隔（毫秒）"},{"timeout_ms","连接超时（毫秒）"},{"serial_log","串口日志"},{"enabled","启用功能"},{"brightness","亮度"},{"timezone","时区"},{"language","语言"},{"city","城市"},{"volume","音量"},{"interval","刷新间隔"},{"interval_ms","刷新间隔（毫秒）"},{"fps","帧率"},{"rotation","旋转角度"},{"speed","速度"},{"theme","主题"}};string last=key.Split('.').Last();return names.ContainsKey(last)?names[last]:key;}
    async Task Save(){
      if(saving||values.Count==0)return;saving=true;fileList.IsEnabled=false;
      try{
        string latest=await device.Text(device.FileRoute(path));if(latest!=original)throw new Exception("设备上的配置已发生变化。请重新选择此文件，读取最新设置后再保存。");
        string body;
        if(path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)){var root=J.Parse(latest);foreach(var field in values){var map=root;for(int i=0;i<field.JsonPath.Length-1;i++)map=J.Map(map,field.JsonPath[i]);map[field.JsonPath.Last()]=field.Read();}body=J.Stringify(root);}
        else {var edits=values.Select(f=>new ConfigEdit {Start=f.Start,Length=f.Length,Value=ConfigField.LuaLiteral(f.Read())}).ToList();body=ConfigField.ApplyEdits(latest,edits);}
        await device.BackupAndPut(path,Encoding.UTF8.GetBytes(body),path.EndsWith(".json")?"application/json":"text/plain");original=body;status.Text="已保存并备份。需要时点击“重新打开应用”。";
        await LoadSelected();status.Text="保存成功，原始配置已备份。";
      }catch(Exception ex){status.Text="保存失败："+ex.Message;}finally{saving=false;fileList.IsEnabled=true;}
    }
  }
  public sealed class ConfigEdit {public int Start,Length;public string Value;}
  public sealed class ConfigField {
    public string Key;public object Value;public int Start,Length;public string[] JsonPath;public FrameworkElement Control;
    public static bool IsNumber(object o){return o is int||o is long||o is double||o is decimal||o is float;}
public object Read(){
  if(Value is bool)return ((CheckBox)Control).IsChecked==true;
  string text=((TextBox)Control).Text;if(!IsNumber(Value))return text;
  if(text==Convert.ToString(Value,CultureInfo.InvariantCulture))return Value;
  var culture=CultureInfo.InvariantCulture;
  if(Value is int){int n;if(int.TryParse(text,NumberStyles.Integer,culture,out n))return n;}
  else if(Value is long){long n;if(long.TryParse(text,NumberStyles.Integer,culture,out n))return n;}
  else if(Value is decimal){decimal n;if(decimal.TryParse(text,NumberStyles.Float,culture,out n))return n;}
  else if(Value is float){float n;if(float.TryParse(text,NumberStyles.Float,culture,out n)&&!float.IsNaN(n)&&!float.IsInfinity(n))return n;}
  else {double n;if(double.TryParse(text,NumberStyles.Float,culture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n))return n;}
  throw new Exception(Key+" 必须填写原数值类型范围内的有效数字。");
}
    public static List<ConfigField> ParseLua(string text){
      var result=new List<ConfigField>();
      var pattern=@"(?m)^[ \t]*(?:(?:config|settings)\.)?([A-Za-z_]\w*)[ \t]*=[ \t]*(true|false|[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?|""(?:[^""\\\r\n]|\\.)*""|'(?:[^'\\\r\n]|\\.)*')(?=[ \t]*,?[ \t]*(?:--[^\r\n]*)?\r?$)";
      var code=LuaCodeMap.Create(text);
      foreach(Match m in Regex.Matches(text,pattern)){if(!code[m.Groups[1].Index])continue;string key=m.Groups[1].Value,literal=m.Groups[2].Value;if(key=="local"||key=="return")continue;object value;
        if(literal=="true"||literal=="false")value=literal=="true";
        else if(literal.StartsWith("\"")||literal.StartsWith("'")){string decoded;if(!TryLuaString(literal,out decoded))continue;value=decoded;}
        else {double n;if(!double.TryParse(literal,NumberStyles.Float,CultureInfo.InvariantCulture,out n))continue;value=n;}
        result.Add(new ConfigField {Key=key,Value=value,Start=m.Groups[2].Index,Length=m.Groups[2].Length});
      }return result;
    }
static bool TryLuaString(string literal,out string value){
  var output=new StringBuilder();value="";
  for(int i=1;i<literal.Length-1;i++){
    char c=literal[i];if(c!='\\'){output.Append(c);continue;}
    if(++i>=literal.Length-1)return false;c=literal[i];
    if(c>='0'&&c<='9'){
      int number=c-'0',digits=1;
      while(digits<3&&i+1<literal.Length-1&&literal[i+1]>='0'&&literal[i+1]<='9'){number=number*10+literal[++i]-'0';digits++;}
      // Non-ASCII Lua byte strings cannot always be represented as a .NET UTF-8 string.
      // Leave those fields untouched instead of silently changing their bytes.
      if(number>127)return false;output.Append((char)number);continue;
    }
    switch(c){case 'a':output.Append('\a');break;case 'b':output.Append('\b');break;case 'f':output.Append('\f');break;case 'n':output.Append('\n');break;case 'r':output.Append('\r');break;case 't':output.Append('\t');break;case 'v':output.Append('\v');break;case '\\':case '\'':case '"':output.Append(c);break;default:return false;}
  }value=output.ToString();return true;
}
    public static string LuaLiteral(object v){if(v is bool)return (bool)v?"true":"false";if(IsNumber(v))return Convert.ToString(v,CultureInfo.InvariantCulture);var b=new StringBuilder("\"");foreach(char c in Convert.ToString(v)){switch(c){case '\\':b.Append("\\\\");break;case '"':b.Append("\\\"");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\"+((int)c).ToString("D3"));else b.Append(c);break;}}return b.Append('"').ToString();}
    public static string ApplyEdits(string text,IEnumerable<ConfigEdit> edits){var b=new StringBuilder(text);foreach(var e in edits.OrderByDescending(e=>e.Start)){b.Remove(e.Start,e.Length);b.Insert(e.Start,e.Value);}return b.ToString();}
  }
}
