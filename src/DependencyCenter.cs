using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace CubicDesk {
  public partial class MainWindow {
    DependencyCatalog dependencyCatalog;
    string dependencyAppFilter="";
    DependencyCatalog DependencyIndex(){if(dependencyCatalog==null)dependencyCatalog=new DependencyCatalog();return dependencyCatalog;}
    void ShowAppDependencies(string id){dependencyAppFilter=id;ShowPage("依赖安装");}
    static void OpenDependencyPage(string url){if(!DependencyCatalog.WebUrl(url))throw new Exception("尚未提供有效的网页链接。");Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
    void EditDependency(string appId,string appName,string dependencyId=""){
      var index=DependencyIndex();var dialog=new DependencyEntryDialog(index,appId,appName,dependencyId){Owner=this};if(dialog.ShowDialog()==true){Notice("依赖信息已保存。安装状态仍由你单独确认。");ShowPage("依赖安装");}
    }
    void BuildDependencies(){
      var index=DependencyIndex();var installed=J.List(state,"installed_apps").ToDictionary(a=>J.Str(a,"id"),a=>a,StringComparer.OrdinalIgnoreCase);
      var allApps=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase);foreach(var a in index.Apps)allApps[J.Str(a,"app_id")]=a;foreach(var a in installed.Values){string id=J.Str(a,"id");if(!allApps.ContainsKey(id))allApps[id]=J.D("app_id",id,"name",Name(a),"reviewed",false);}
      var intro=Stack(Text("应用依赖与下载入口",22,"#263C59",true),Gap(8),Text("统一整理商店软件所需的运行环境、驱动和电脑桥接。根据作者说明自行下载安装，完成后手动确认状态。",12,"#687E98"),Gap(14),Row(Button("登记依赖",delegate{string id=dependencyAppFilter.StartsWith("@")?"":dependencyAppFilter;EditDependency(id,allApps.ContainsKey(id)?J.Str(allApps[id],"name"):"");return Task.FromResult(0);},true),Button("导入清单",delegate{
        var file=new OpenFileDialog {Filter="依赖清单 (*.json)|*.json",Title="导入依赖定义与软件关联"};if(file.ShowDialog(this)==true){if(new FileInfo(file.FileName).Length>2097152)throw new Exception("清单请小于 2 MB。");index.Merge(J.Parse(File.ReadAllText(file.FileName,Encoding.UTF8)));ShowPage("依赖安装");Notice("已合并依赖清单；没有导入或修改人工安装状态。");}return Task.FromResult(0);
      }),Button("导出清单",delegate{var file=new SaveFileDialog {Filter="JSON 清单|*.json",FileName="cubicdesk-dependencies.json"};if(file.ShowDialog(this)==true){File.WriteAllText(file.FileName,J.Stringify(index.Export()),new UTF8Encoding(false));Notice("已导出依赖定义和软件关联，不包含本机安装状态。");}return Task.FromResult(0);}),Button("本机工具",delegate{ShowPage("本机工具");return Task.FromResult(0);})));page.Children.Add(Card(intro));
      var active=index.Dependencies.Where(d=>index.Users(J.Str(d,"id")).Count>0).ToList();int ready=active.Count(d=>index.Status(J.Str(d,"id"))=="ready");
      var counters=new UniformGridFix(3);counters.Children.Add(Stat("已登记的软件",index.Apps.Count.ToString(),online?index.Apps.Count(a=>installed.ContainsKey(J.Str(a,"app_id")))+" 个已安装到当前摆件":"当前未连接摆件"));counters.Children.Add(Stat("去重后的依赖",active.Count.ToString(),"同一依赖可被多个软件共用"));counters.Children.Add(Stat("人工确认已准备",ready+" / "+active.Count,"仅记录手动确认，不自动检测安装"));page.Children.Add(counters);
      var filter=Select(new[]{"全部已登记软件","仅当前摆件已安装软件","待补充依赖信息"});var filterIds=new List<string>{"","@installed","@unknown"};foreach(var a in allApps.Values.OrderBy(a=>J.Str(a,"name"))){filter.Items.Add(J.Str(a,"name")+" · "+J.Str(a,"app_id"));filterIds.Add(J.Str(a,"app_id"));}
      int selected=filterIds.IndexOf(dependencyAppFilter);filter.SelectedIndex=selected>=0?selected:0;var query=Input();var list=new StackPanel();var empty=Text("",12,"#687E98");
      var filterBox=new StackPanel();Field(filterBox,"按应用筛选",filter);var searchBox=new StackPanel();Field(searchBox,"搜索软件、依赖或用途",query);page.Children.Add(Columns(filterBox,searchBox,1));page.Children.Add(Gap(8));page.Children.Add(empty);page.Children.Add(Gap(10));page.Children.Add(list);
      Action render=delegate{
        list.Children.Clear();string chosen=filterIds[Math.Max(0,filter.SelectedIndex)];dependencyAppFilter=chosen;string search=query.Text.Trim();
        var appIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(chosen=="@installed")foreach(string id in installed.Keys)appIds.Add(id);else if(chosen=="@unknown")foreach(var a in allApps.Values.Where(a=>!index.Reviewed(J.Str(a,"app_id"))))appIds.Add(J.Str(a,"app_id"));else if(chosen!="")appIds.Add(chosen);else foreach(var a in index.Apps)appIds.Add(J.Str(a,"app_id"));
        var visibleDeps=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string id in appIds)foreach(var r in index.Requirements(id))visibleDeps.Add(J.Str(r,"dependency_id"));
        foreach(var d in index.Dependencies.Where(d=>visibleDeps.Contains(J.Str(d,"id")))){
          string id=J.Str(d,"id");var users=index.Apps.Where(a=>appIds.Contains(J.Str(a,"app_id"))&&index.Requirements(J.Str(a,"app_id")).Any(r=>J.Str(r,"dependency_id").Equals(id,StringComparison.OrdinalIgnoreCase))).ToList();
          string text=J.Str(d,"name")+" "+string.Join(" ",index.Users(id))+" "+J.Str(d,"description");if(search!=""&&text.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
          var body=Stack(Text(J.Str(d,"name"),20,"#263C59",true),Gap(5),Text(J.Str(d,"kind","外部依赖")+"  ·  "+StatusLabel(index.Status(id)),12,"#276FC0"),Gap(10),Text(J.Str(d,"description"),13),Gap(7),Text("关联软件："+string.Join("、",index.Users(id)),12,"#687E98"));
          foreach(var a in users){var requirement=index.Requirements(J.Str(a,"app_id")).First(r=>J.Str(r,"dependency_id").Equals(id,StringComparison.OrdinalIgnoreCase));body.Children.Add(Row(AppIcon(J.Str(a,"app_id"),J.Str(a,"name"),24),Text(J.Str(a,"name")+" · "+(J.Bool(requirement,"optional")?"可选":"需要")+" · "+J.Str(requirement,"version","版本见作者说明"),11,"#687E98")));}
          if(J.Str(d,"instructions")!=""){body.Children.Add(Gap(12));body.Children.Add(Text(J.Str(d,"instructions"),12,"#516B88"));}
          body.Children.Add(Gap(8));body.Children.Add(Text("信息来源："+J.Str(d,"source","人工登记"),10,"#748BA4"));body.Children.Add(Gap(14));
          var download=Button("前往下载 / 项目页",delegate{OpenDependencyPage(J.Str(d,"download_url"));Notice("已打开下载页面，完成安装后可手动标记。");return Task.FromResult(0);},true);download.IsEnabled=DependencyCatalog.WebUrl(J.Str(d,"download_url"));
          var guide=Button("查看配置说明",delegate{OpenDependencyPage(J.Str(d,"guide_url"));Notice("已打开作者说明。");return Task.FromResult(0);});guide.IsEnabled=DependencyCatalog.WebUrl(J.Str(d,"guide_url"));
          string owner=users.Count>0?J.Str(users[0],"app_id"):"";string ownerName=users.Count>0?J.Str(users[0],"name"):"";
          body.Children.Add(Row(download,guide,Button("编辑信息",delegate{EditDependency(owner,ownerName,id);return Task.FromResult(0);})));
          if(!download.IsEnabled){body.Children.Add(Gap(5));body.Children.Add(Text("下载地址待补充；请根据作者说明核对后登记。",11,"#987240"));}
          var mark=Select(new[]{"未确认","已准备（人工确认）","暂不需要"});mark.SelectedIndex=index.Status(id)=="ready"?1:index.Status(id)=="skipped"?2:0;mark.Width=200;mark.Margin=new Thickness(0,0,10,0);
          body.Children.Add(Gap(12));body.Children.Add(Row(mark,Button("保存确认状态",delegate{index.Mark(id,new[]{"unconfirmed","ready","skipped"}[mark.SelectedIndex]);ShowPage("依赖安装");Notice("已保存人工确认状态；没有执行下载或安装程序。");return Task.FromResult(0);})));
          list.Children.Add(Card(body));
        }
        foreach(string id in appIds){if(!allApps.ContainsKey(id)||index.Requirements(id).Count>0)continue;var a=allApps[id];string appName=J.Str(a,"name",id);bool reviewed=index.Reviewed(id);if(search!=""&&(appName+id).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
          var body=Stack(AppHeading(id,appName,17,36),Gap(7),Text(reviewed?"已登记：无需外部电脑依赖（以登记信息为准）。":"依赖信息尚未登记。这不代表软件不需要外部依赖。",12,"#687E98"),Gap(12));
          body.Children.Add(Row(Button("登记依赖",delegate{EditDependency(id,appName);return Task.FromResult(0);},true),Button("确认无外部依赖",delegate{if(MessageBox.Show("你已核对作者说明，确认“"+appName+"”无需外部电脑依赖？","人工确认",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){index.SetNoExternalDependencies(id,appName);ShowPage("依赖安装");Notice("已记录你的确认。");}return Task.FromResult(0);})));
          list.Children.Add(Card(body));
        }
        empty.Text=list.Children.Count==0?"没有匹配的依赖。可以登记依赖，或选择“待补充依赖信息”。":"下载页和安装说明由软件作者维护；状态仅代表你的人工确认。";
      };
      filter.SelectionChanged+=delegate{render();};query.TextChanged+=delegate{render();};render();
    }
    static string StatusLabel(string status){return status=="ready"?"已准备 · 人工确认":status=="skipped"?"暂不需要 · 人工标记":"未确认";}
  }
  public sealed class DependencyEntryDialog : Window {
    readonly DependencyCatalog index;readonly string existing;
    readonly TextBox appId=new TextBox(),appName=new TextBox(),id=new TextBox(),name=new TextBox(),kind=new TextBox(),version=new TextBox(),download=new TextBox(),guide=new TextBox(),description=new TextBox(),instructions=new TextBox();
    readonly CheckBox optional=new CheckBox {Content="此软件将该依赖列为可选"};readonly TextBlock error=new TextBlock {Foreground=Brushes.IndianRed};
    public DependencyEntryDialog(DependencyCatalog catalog,string app,string title,string dependency=""){
      index=catalog;existing=dependency;Style=(Style)FindResource(typeof(Window));Title="登记应用依赖";Width=740;Height=Math.Min(910,SystemParameters.WorkArea.Height-40);MinWidth=580;MinHeight=580;WindowStartupLocation=WindowStartupLocation.CenterOwner;
      appId.Text=app;appName.Text=title;id.Text=dependency;kind.Text="桥接程序";
      var d=catalog.Dependency(dependency);if(d!=null){name.Text=J.Str(d,"name");kind.Text=J.Str(d,"kind");download.Text=J.Str(d,"download_url");guide.Text=J.Str(d,"guide_url");description.Text=J.Str(d,"description");instructions.Text=J.Str(d,"instructions");var r=catalog.Requirements(app).FirstOrDefault(x=>J.Str(x,"dependency_id").Equals(dependency,StringComparison.OrdinalIgnoreCase));version.Text=J.Str(r,"version");optional.IsChecked=J.Bool(r,"optional");}
      var root=new DockPanel {Margin=new Thickness(28,12,28,26)};var bottom=new StackPanel {Margin=new Thickness(0,14,0,0)};bottom.Children.Add(error);var save=new Button {Content="保存依赖与软件关联",Style=(Style)FindResource("Primary"),Margin=new Thickness(0,9,0,0)};save.Click+=delegate{try{var value=d==null?new Dictionary<string,object>():new Dictionary<string,object>(d);value["id"]=id.Text.Trim();value["name"]=name.Text.Trim();value["kind"]=kind.Text.Trim();value["download_url"]=download.Text.Trim();value["guide_url"]=guide.Text.Trim();value["description"]=description.Text.Trim();value["instructions"]=instructions.Text.Trim();value["source"]="用户维护";if(existing!=""&&!existing.Equals(id.Text.Trim(),StringComparison.OrdinalIgnoreCase))throw new Exception("共用依赖的标识不能在编辑时变更；请新增条目。");index.Upsert(appId.Text.Trim(),string.IsNullOrWhiteSpace(appName.Text)?appId.Text.Trim():appName.Text.Trim(),value,version.Text.Trim(),optional.IsChecked==true);DialogResult=true;}catch(Exception ex){error.Text=ex.Message;}};bottom.Children.Add(save);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
      var fields=new StackPanel();fields.Children.Add(new TextBlock {Text="整理信息并提供下载入口；此表单不会安装程序。共用依赖的说明修改会同步给引用它的软件。",Margin=new Thickness(0,0,0,12)});
      Add(fields,"设备应用标识（例如 pc_mirror）",appId);Add(fields,"应用显示名称",appName);Add(fields,"依赖标识（同一组件使用相同标识）",id);Add(fields,"依赖名称",name);Add(fields,"类别",kind);Add(fields,"此应用要求的版本",version);Add(fields,"下载或作者项目页 URL（可留空）",download);Add(fields,"作者配置说明 URL（可留空）",guide);Add(fields,"用途",description);instructions.AcceptsReturn=true;instructions.TextWrapping=TextWrapping.Wrap;instructions.MinHeight=85;Add(fields,"安装与配置说明",instructions);fields.Children.Add(optional);root.Children.Add(new ScrollViewer {Content=fields});GlassWindow.Attach(this,root);
    }
    static void Add(Panel panel,string label,UIElement control){panel.Children.Add(new TextBlock {Text=label,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,7)});panel.Children.Add(control);}
  }
}
