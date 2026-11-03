// 用途：加载应用商店目录、提示依赖并确认安装任务结果。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CubicDesk {
  public partial class MainWindow {
    List<Dictionary<string,object>> storeItems=new List<Dictionary<string,object>>();
    bool storeLoaded;
    string storeSource="尚未加载目录";
    void BuildAppStore(){
      var server=Input(J.Str(Store.Config,"storeServer","https://cubic.clocteck.com"));
      var list=new StackPanel();var summary=Text(storeSource,12,"#687E98");var query=Input();var kind=Select(new[]{"全部应用","仅未安装","仅已安装","有依赖记录","依赖信息待补充"});
      Action render=null;Func<Task> load=null;
      render=delegate{
        list.Children.Clear();var index=DependencyIndex();var installed=J.List(state,"installed_apps").ToDictionary(a=>J.Str(a,"id"),a=>a,StringComparer.OrdinalIgnoreCase);
        foreach(var raw in storeItems){string id=J.Str(raw,"app_id",J.Str(raw,"id"));if(id==""||J.Str(raw,"kind")=="launcher")continue;bool exists=installed.ContainsKey(id);string name=J.Str(raw,"name_zh_cn",J.Str(raw,"name_zh",J.Str(raw,"name",id))),description=J.Str(raw,"description_zh_cn",J.Str(raw,"description_zh",J.Str(raw,"description","暂无说明")));
          if((name+" "+description+" "+id).IndexOf(query.Text.Trim(),StringComparison.OrdinalIgnoreCase)<0)continue;
          if((kind.SelectedIndex==1&&exists)||(kind.SelectedIndex==2&&!exists)||(kind.SelectedIndex==3&&index.Requirements(id).Count==0)||(kind.SelectedIndex==4&&index.Reviewed(id)))continue;
          string dependencyText=!index.Reviewed(id)?"依赖信息待补充":index.Requirements(id).Count==0?"已登记无需外部依赖":index.Requirements(id).Count+" 项电脑端依赖 · "+index.Pending(id)+" 项未确认";
          var body=Stack(AppHeading(id,name,20,48,J.Str(raw,"icon_url")),Gap(6),Text((exists?"已安装":"未安装")+" · "+J.Str(raw,"version","版本未提供")+" · "+J.Str(raw,"catalog_scope","community"),11,"#687E98"),Gap(10),Text(description,13),Gap(10),Text(dependencyText,12,"#276FC0"),Gap(14));
          var install=Button(exists?"已安装":"安装到摆件",async delegate{await BeginStoreInstall(raw);},true);install.IsEnabled=!exists&&DependencyCatalog.WebUrl(J.Str(raw,"manifest_url"));
          body.Children.Add(Row(install,Button("依赖与下载入口",delegate{ShowAppDependencies(id);return Task.FromResult(0);}),Button("作者说明",delegate{string url=J.Str(raw,"description_page_url");if(!DependencyCatalog.WebUrl(url))throw new Exception("作者尚未提供网页说明地址。");OpenDependencyPage(url);Notice("已打开作者说明。");return Task.FromResult(0);})));
          if(!exists&&!install.IsEnabled){body.Children.Add(Gap(7));body.Children.Add(Text("当前条目没有有效的安装清单地址。",11,"#987240"));}list.Children.Add(Card(body));
        }
        if(list.Children.Count==0)list.Children.Add(Card(Stack(Text("暂无匹配的软件",18,"#263C59",true),Gap(8),Text("可以刷新设备商店、导入目录，或使用作者提供的安装清单地址。",12,"#687E98"))));
      };
      load=async delegate{
        if(!online)throw new Exception("请先连接摆件，再读取应用商店。");string source=server.Text.Trim().TrimEnd('/');if(!DependencyCatalog.WebUrl(source))throw new Exception("商店地址需要是 HTTP/HTTPS 网页地址。");summary.Text="正在通过摆件读取应用目录…";
        try{
          using(var client=new DeviceClient(device.Address,35)){
            var catalog=await client.Request("/api/system/apps/catalog?server="+Uri.EscapeDataString(source)+"&channel=stable");var items=J.List(catalog,"items");AppIcons.AdoptCatalog(catalog,source);
            var index=DependencyIndex();foreach(var item in items){try{index.IngestStoreItem(item);}catch(Exception ex){Store.Log("商店依赖信息未采用："+ex.Message);}}
            storeItems=items;storeLoaded=true;storeSource="设备商店 · "+items.Count+" 个条目 · "+DateTime.Now.ToString("HH:mm");Store.Config["storeServer"]=source;Store.Save();Store.Atomic(Path.Combine(Store.Data,"store-catalog.json"),J.Stringify(J.D("items",items,"source",source,"fetched_at",DateTime.Now.ToString("s"))));summary.Text=storeSource;render();Notice("应用目录已读取；安装时会提示查看电脑端依赖。");
          }
        }catch(Exception ex){
          if(storeItems.Count==0)UseInstalledStoreFallback();summary.Text="设备商店读取失败："+ex.Message+"。当前显示"+storeSource+"。可重试或导入作者目录。";render();Notice("商店上游暂时无法读取；本地依赖清单仍可使用。",true);
        }
      };
      var top=Stack(Text("安装前，先了解电脑端依赖",20,"#263C59",true),Gap(8),Text("这里安装的是摆件应用。所需的电脑运行环境或第三方桥接，会提示你到“依赖安装”查看说明并自行下载。",12,"#687E98"));Field(top,"应用商店服务地址",server);
      top.Children.Add(Row(Button("刷新设备商店",async delegate{await load();},true),Button("导入应用目录",delegate{
        var file=new OpenFileDialog {Filter="应用目录 (*.json)|*.json"};if(file.ShowDialog(this)==true){if(new FileInfo(file.FileName).Length>4194304)throw new Exception("应用目录请小于 4 MB。");var data=J.Parse(File.ReadAllText(file.FileName,Encoding.UTF8));if(J.Get(data,"items")==null)throw new Exception("应用目录需要 items 数组。");var items=J.List(data,"items");foreach(var item in items){DependencyCatalog.ValidateId(J.Str(item,"app_id",J.Str(item,"id")));DependencyIndex().IngestStoreItem(item);}storeItems=items;storeLoaded=true;storeSource="本地导入目录 · "+items.Count+" 个条目";Store.Atomic(Path.Combine(Store.Data,"store-catalog.json"),J.Stringify(J.D("items",items,"source","用户导入","fetched_at",DateTime.Now.ToString("s"))));summary.Text=storeSource;render();Notice("目录已导入；人工安装确认状态没有改变。");}return Task.FromResult(0);
      }),Button("使用安装清单地址",async delegate{
        var dialog=new StoreManifestDialog {Owner=this};if(dialog.ShowDialog()==true)await BeginStoreInstall(dialog.Entry);
      }),Button("维护依赖清单",delegate{dependencyAppFilter="";ShowPage("依赖安装");return Task.FromResult(0);})));page.Children.Add(Card(top));
      var kindBox=new StackPanel();Field(kindBox,"按安装与依赖信息筛选",kind);var searchBox=new StackPanel();Field(searchBox,"搜索应用名称或说明",query);page.Children.Add(Columns(kindBox,searchBox,1));page.Children.Add(Gap(10));page.Children.Add(summary);page.Children.Add(Gap(16));page.Children.Add(list);query.TextChanged+=delegate{render();};kind.SelectionChanged+=delegate{render();};
      if(!storeLoaded&&storeItems.Count==0){string cache=Path.Combine(Store.Data,"store-catalog.json");if(File.Exists(cache)){try{var data=J.Parse(File.ReadAllText(cache,Encoding.UTF8));storeItems=J.List(data,"items");storeSource="缓存目录 · "+J.Str(data,"fetched_at","时间未记录");summary.Text=storeSource;}catch{}}if(storeItems.Count==0){UseInstalledStoreFallback();summary.Text=storeSource+"；点击刷新可加载远程目录。";}}
      render();
    }
    void UseInstalledStoreFallback(){storeItems=J.List(state,"installed_apps").Select(a=>J.D("app_id",J.Str(a,"id"),"name",Name(a),"version",J.Str(a,"version"),"kind",J.Str(a,"kind"),"description","当前摆件已安装的软件；远程应用目录尚未加载。","catalog_scope","已安装清单")).ToList();storeSource="当前摆件已安装清单";}
    async Task BeginStoreInstall(Dictionary<string,object> item){
      if(!online)throw new Exception("请先连接摆件。");string id=J.Str(item,"app_id",J.Str(item,"id"));DependencyCatalog.ValidateId(id);string manifest=J.Str(item,"manifest_url");if(!DependencyCatalog.WebUrl(manifest))throw new Exception("安装清单必须是有效的 HTTP/HTTPS 地址。");if(id.Equals("launcher",StringComparison.OrdinalIgnoreCase)||J.Str(item,"kind")=="launcher")throw new Exception("启动器更新请使用设备自己的维护入口。");
      await Refresh();if(J.List(state,"installed_apps").Any(a=>J.Str(a,"id").Equals(id,StringComparison.OrdinalIgnoreCase)))throw new Exception("此应用已经安装。此入口只安装新应用，避免覆盖已有配置。");
      var index=DependencyIndex();index.IngestStoreItem(item);string name=J.Str(item,"name_zh_cn",J.Str(item,"name_zh",J.Str(item,"name",id)));
      var prompt=new DependencyInstallPrompt(index,id,name,manifest){Owner=this};prompt.ShowDialog();if(prompt.Result==DependencyInstallPrompt.Choice.Dependencies){ShowAppDependencies(id);Notice("查看依赖下载和配置说明后，可以返回商店继续安装。");return;}if(prompt.Result!=DependencyInstallPrompt.Choice.Install)return;
      long baseline=(long)J.Num(J.Map(state,"app_install"),"seq");await device.Request("/api/system/apps","POST",J.D("manifest_url",manifest,"app_id",id,"name",name));Notice("已提交安装："+name+"；等待摆件返回结果…");
      bool completed=false;for(int attempt=0;attempt<90;attempt++){
        await Task.Delay(1000);var next=await device.Request("/api/system/state");state=next;var install=J.Map(next,"app_install");var jobs=J.List(install,"jobs");if(jobs.Count==0&&J.Str(install,"app_id")!="")jobs.Add(install);
        var job=jobs.Where(j=>J.Str(j,"app_id").Equals(id,StringComparison.OrdinalIgnoreCase)&&J.Num(j,"seq")>baseline).OrderByDescending(j=>J.Num(j,"seq")).FirstOrDefault();
        if(job==null)continue;string phase=J.Str(job,"phase");if(new[]{"error","failed","cancelled"}.Contains(phase))throw new Exception("摆件安装未完成："+J.Str(job,"error",phase));if(phase=="done"){completed=J.List(next,"installed_apps").Any(a=>J.Str(a,"id").Equals(id,StringComparison.OrdinalIgnoreCase));if(!completed)throw new Exception("任务报告完成，但应用列表尚未确认安装。");break;}Notice("正在安装 "+name+" · "+phase+" · "+J.Num(job,"percent").ToString("0")+"%");
      }
      if(!completed)throw new Exception("安装命令已提交，但尚未确认完成。请刷新应用列表检查，不要重复提交。");
      if(prompt.OpenDependenciesAfterInstall){ShowAppDependencies(id);Notice(name+" 已安装到摆件；请在这里查看并准备电脑端依赖。");}else{ShowPage("应用商店");Notice(name+" 已安装到摆件。");}
    }
  }
  public sealed class DependencyInstallPrompt : Window {
    public enum Choice {Cancel,Install,Dependencies}
    public Choice Result=Choice.Cancel;
    readonly CheckBox after=new CheckBox {Content="设备应用安装完成后，打开依赖页面",IsChecked=true};
    public bool OpenDependenciesAfterInstall {get{return after.IsChecked==true;}}
    public DependencyInstallPrompt(DependencyCatalog index,string id,string name,string manifest){
      Style=(Style)FindResource(typeof(Window));Title="安装前查看应用依赖";Width=750;Height=570;MinWidth=600;MinHeight=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;
      var root=new DockPanel {Margin=new Thickness(28,12,28,24)};var bottom=new StackPanel();bottom.Children.Add(after);var actions=new StackPanel {Orientation=Orientation.Horizontal};var view=new Button {Content="先查看依赖",Style=(Style)FindResource("Primary"),Margin=new Thickness(0,0,9,0)};view.Click+=delegate{Result=Choice.Dependencies;DialogResult=true;};var install=new Button {Content="继续安装到摆件",Margin=new Thickness(0,0,9,0)};install.Click+=delegate{Result=Choice.Install;DialogResult=true;};var cancel=new Button {Content="取消"};cancel.Click+=delegate{Close();};actions.Children.Add(view);actions.Children.Add(install);actions.Children.Add(cancel);bottom.Children.Add(actions);DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
      var body=new StackPanel();body.Children.Add(new TextBlock {Text=name,FontSize=23,FontWeight=FontWeights.SemiBold});body.Children.Add(new TextBlock {Text="应用清单："+manifest,FontSize=11,Margin=new Thickness(0,10,0,14)});
      var requirements=index.Requirements(id);if(!index.Reviewed(id))body.Children.Add(new TextBlock {Text="该应用尚未登记电脑端依赖信息。可以先查看作者说明或补充依赖；未登记不表示不需要依赖。",Margin=new Thickness(0,0,0,16)});else if(requirements.Count==0)body.Children.Add(new TextBlock {Text="当前登记为无需外部电脑依赖，请以作者说明为准。",Margin=new Thickness(0,0,0,16)});
      foreach(var r in requirements){var d=index.Dependency(J.Str(r,"dependency_id"));body.Children.Add(new TextBlock {Text="• "+J.Str(d,"name",J.Str(r,"dependency_id"))+"  ·  "+(J.Bool(r,"optional")?"可选":"需要"),FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,4)});body.Children.Add(new TextBlock {Text=J.Str(r,"version","版本见作者说明")+" · "+(index.Status(J.Str(r,"dependency_id"))=="ready"?"人工确认已准备":"尚未确认准备"),FontSize=12});}
      body.Children.Add(new TextBlock {Text="电脑端依赖由你根据作者说明自行下载安装；Cubic Desk 提供清单、说明和跳转入口。可以先安装设备应用，再准备这些依赖。",Margin=new Thickness(0,20,0,10)});root.Children.Add(new ScrollViewer {Content=body});GlassWindow.Attach(this,root);
    }
  }
  public sealed class StoreManifestDialog : Window {
    public Dictionary<string,object> Entry;
    public StoreManifestDialog(){Style=(Style)FindResource(typeof(Window));Title="使用作者提供的安装清单";Width=700;Height=460;WindowStartupLocation=WindowStartupLocation.CenterOwner;var root=new StackPanel {Margin=new Thickness(28,10,28,28)};TextBox id=new TextBox(),name=new TextBox(),url=new TextBox();var error=new TextBlock {Foreground=System.Windows.Media.Brushes.IndianRed};foreach(var row in new[]{new KeyValuePair<string,TextBox>("应用标识（与作者清单一致）",id),new KeyValuePair<string,TextBox>("应用名称",name),new KeyValuePair<string,TextBox>("作者安装清单 URL（manifest JSON）",url)}){root.Children.Add(new TextBlock {Text=row.Key,Margin=new Thickness(0,12,0,6)});root.Children.Add(row.Value);}root.Children.Add(error);var next=new Button {Content="查看依赖与安装提示",Style=(Style)FindResource("Primary"),Margin=new Thickness(0,16,0,0)};next.Click+=delegate{try{DependencyCatalog.ValidateId(id.Text.Trim());if(!DependencyCatalog.WebUrl(url.Text.Trim()))throw new Exception("请输入有效的 HTTP/HTTPS 安装清单地址。");Entry=J.D("app_id",id.Text.Trim(),"name",string.IsNullOrWhiteSpace(name.Text)?id.Text.Trim():name.Text.Trim(),"manifest_url",url.Text.Trim(),"kind","app");DialogResult=true;}catch(Exception ex){error.Text=ex.Message;}};root.Children.Add(next);GlassWindow.Attach(this,root);}
  }
}
