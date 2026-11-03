using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CubicDesk {
  public sealed class DependencyCatalog {
    readonly string directory;
    readonly Dictionary<string,Dictionary<string,object>> dependencies=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Dictionary<string,object>> apps=new Dictionary<string,Dictionary<string,object>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string,object> marks=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
    public DependencyCatalog(string dir=null,string seed=null){
      directory=dir??Store.Data;Directory.CreateDirectory(directory);
      string defaults=seed??Path.Combine(Store.Root,"dependencies.json");
      if(File.Exists(defaults))Merge(J.Parse(File.ReadAllText(defaults,Encoding.UTF8)),false);
      string local=Path.Combine(directory,"dependency-catalog.json");
      if(File.Exists(local))Merge(J.Parse(File.ReadAllText(local,Encoding.UTF8)),false);
      string status=Path.Combine(directory,"dependency-status.json");
      if(File.Exists(status))marks=new Dictionary<string,object>(J.Parse(File.ReadAllText(status,Encoding.UTF8)),StringComparer.OrdinalIgnoreCase);
    }
    public List<Dictionary<string,object>> Apps {get{return apps.Values.OrderBy(a=>J.Str(a,"name")).ToList();}}
    public List<Dictionary<string,object>> Dependencies {get{return dependencies.Values.OrderBy(a=>J.Str(a,"name")).ToList();}}
    public Dictionary<string,object> App(string id){Dictionary<string,object> value;return apps.TryGetValue(id??"",out value)?value:null;}
    public Dictionary<string,object> Dependency(string id){Dictionary<string,object> value;return dependencies.TryGetValue(id??"",out value)?value:null;}
    public List<Dictionary<string,object>> Requirements(string app){return J.List(App(app),"requirements").GroupBy(r=>J.Str(r,"dependency_id"),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();}
    public bool Reviewed(string app){return J.Bool(App(app),"reviewed");}
    public string Status(string id){id=J.Str(Dependency(id),"id",id);string value=J.Str(J.Map(marks,id),"status","unconfirmed");return new[]{"ready","unconfirmed","skipped"}.Contains(value)?value:"unconfirmed";}
    public int Pending(string app){return Requirements(app).Count(r=>!J.Bool(r,"optional")&&Status(J.Str(r,"dependency_id"))!="ready");}
    public List<string> Users(string id){return Apps.Where(a=>Requirements(J.Str(a,"app_id")).Any(r=>J.Str(r,"dependency_id").Equals(id,StringComparison.OrdinalIgnoreCase))).Select(a=>J.Str(a,"name",J.Str(a,"app_id"))).ToList();}
    public void Mark(string id,string status){if(Dependency(id)==null)throw new Exception("依赖不存在。");id=J.Str(Dependency(id),"id");if(!new[]{"ready","unconfirmed","skipped"}.Contains(status))throw new Exception("状态无效。");marks[id]=J.D("status",status,"confirmed_by","user","confirmed_at",DateTime.Now.ToString("s"));Store.Atomic(Path.Combine(directory,"dependency-status.json"),J.Stringify(marks));}
    public Dictionary<string,object> Export(){return J.D("schema_version",1,"dependencies",Dependencies,"apps",Apps);}
    public void Save(){string path=Path.Combine(directory,"dependency-catalog.json");if(File.Exists(path)){string dir=Path.Combine(directory,"dependency-backups");Directory.CreateDirectory(dir);File.Copy(path,Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".json"));}Store.Atomic(path,J.Stringify(Export()));}
    public void Merge(Dictionary<string,object> document,bool persist=true){
      if(J.Num(document,"schema_version")!=1)throw new Exception("依赖清单需要 schema_version: 1。");
      var incoming=Records(document,"dependencies");var appRecords=Records(document,"apps");if(incoming.Count>1024||appRecords.Count>2048)throw new Exception("清单条目过多。");
      foreach(var dependency in incoming)ValidateDependency(dependency);
      var ids=new HashSet<string>(dependencies.Keys,StringComparer.OrdinalIgnoreCase);foreach(var d in incoming)ids.Add(J.Str(d,"id"));
      foreach(var app in appRecords){ValidateId(J.Str(app,"app_id"));if(J.Str(app,"name").Length>200)throw new Exception("应用名称过长。");if(J.Get(app,"reviewed")!=null&&!(J.Get(app,"reviewed") is bool))throw new Exception("reviewed 必须为布尔值。");foreach(var r in Records(app,"requirements")){if(!ids.Contains(J.Str(r,"dependency_id")))throw new Exception("清单引用了未定义的依赖："+J.Str(r,"dependency_id"));if(J.Get(r,"optional")!=null&&!(J.Get(r,"optional") is bool))throw new Exception("optional 必须为布尔值。");}}
      foreach(var d in incoming){string id=J.Str(d,"id");dependencies[id]=Overlay(Dependency(id),d);}
      foreach(var a in appRecords){string id=J.Str(a,"app_id");apps[id]=Overlay(App(id),a);}
      if(persist)Save();
    }
    public void Upsert(string appId,string appName,Dictionary<string,object> dependency,string version,bool optional){
      ValidateId(appId);ValidateDependency(dependency);string id=J.Str(dependency,"id");
      var app=Overlay(App(appId),J.D("app_id",appId,"name",appName,"reviewed",true,"source","用户维护"));
      var requirements=Requirements(appId).Where(r=>!J.Str(r,"dependency_id").Equals(id,StringComparison.OrdinalIgnoreCase)).ToList();
      requirements.Add(J.D("dependency_id",id,"version",version,"optional",optional));app["requirements"]=requirements;
      Merge(J.D("schema_version",1,"dependencies",new[]{dependency},"apps",new[]{app}));
    }
    public void SetNoExternalDependencies(string appId,string appName){ValidateId(appId);apps[appId]=Overlay(App(appId),J.D("app_id",appId,"name",appName,"reviewed",true,"requirements",new object[0],"source","用户确认"));Save();}
    public void Unlink(string appId,string dependencyId){var a=App(appId);if(a==null)return;a["requirements"]=Requirements(appId).Where(r=>!J.Str(r,"dependency_id").Equals(dependencyId,StringComparison.OrdinalIgnoreCase)).ToList();a["reviewed"]=false;Save();}
    public bool IngestStoreItem(Dictionary<string,object> item){
      string appId=J.Str(item,"app_id",J.Str(item,"id"));ValidateId(appId);
      if(J.Str(App(appId),"source")=="用户维护"||J.Str(App(appId),"source")=="用户确认")return false;
      // Only explicitly declared PC dependencies are treated as computer-side requirements.
      object declared=J.Get(item,"pc_dependencies")??J.Get(item,"desktop_dependencies");if(declared==null)return false;
      var array=declared as System.Collections.IEnumerable;if(array==null||declared is string||declared is System.Collections.IDictionary)throw new Exception("电脑端依赖声明必须是数组。");
      var list=new List<Dictionary<string,object>>();var requirements=new List<Dictionary<string,object>>();int index=0;
      foreach(object entry in array){var d=J.Obj(entry);string name=entry is string?(string)entry:J.Str(d,"name");if(name=="")throw new Exception("依赖声明缺少名称。");string id=J.Str(d,"id",appId+"-pc-"+(++index));
        var dep=J.D("id",id,"name",name,"kind",J.Str(d,"kind","作者声明依赖"),"download_url",J.Str(d,"download_url"),"guide_url",J.Str(d,"guide_url"),"description",J.Str(d,"description"),"instructions",J.Str(d,"instructions"),"source","商店作者声明");
        if(J.Str(Dependency(id),"source")=="用户维护")dep=Dependency(id);list.Add(dep);requirements.Add(J.D("dependency_id",id,"version",J.Str(d,"version"),"optional",J.Bool(d,"optional")));
      }
      Merge(J.D("schema_version",1,"dependencies",list,"apps",new[]{J.D("app_id",appId,"name",J.Str(item,"name_zh_cn",J.Str(item,"name_zh",J.Str(item,"name",appId))),"reviewed",true,"requirements",requirements,"source","商店作者声明")}));return true;
    }
    public static void ValidateId(string id){if(!Regex.IsMatch(id??"",@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$"))throw new Exception("标识仅支持字母、数字、下划线、点和连字符，最长 100 个字符。");}
    static List<Dictionary<string,object>> Records(object record,string key){var value=J.Get(record,key);if(value==null)return new List<Dictionary<string,object>>();var array=value as System.Collections.IEnumerable;if(array==null||value is string||value is System.Collections.IDictionary)throw new Exception(key+" 必须是记录数组。");var result=new List<Dictionary<string,object>>();foreach(object item in array){var map=item as Dictionary<string,object>;if(map==null)throw new Exception(key+" 包含无效条目。");result.Add(map);}return result;}
    public static bool WebUrl(string value){Uri uri;return Uri.TryCreate(value,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http")&&uri.UserInfo=="";}
    static void ValidateDependency(Dictionary<string,object> d){ValidateId(J.Str(d,"id"));if(string.IsNullOrWhiteSpace(J.Str(d,"name"))||J.Str(d,"name").Length>200)throw new Exception("依赖名称为空或过长。");foreach(string key in new[]{"download_url","guide_url"}){string url=J.Str(d,key);if(url!=""&&(!WebUrl(url)||url.Length>2048))throw new Exception("下载与说明链接只支持不含账号密码的 HTTP/HTTPS 地址。");}if(J.Str(d,"instructions").Length>12000)throw new Exception("说明过长。");}
    static Dictionary<string,object> Overlay(Dictionary<string,object> previous,Dictionary<string,object> next){var result=previous==null?new Dictionary<string,object>():new Dictionary<string,object>(previous);foreach(var pair in next)result[pair.Key]=pair.Value;return result;}
  }
}
