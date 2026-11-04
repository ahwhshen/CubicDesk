using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using CubicDesk;
class FakeHandler : HttpMessageHandler {
 public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Reply=delegate{throw new NotSupportedException("No mock response configured");};
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){return Reply(request,token);}
}
class Regression {
 static int failures,checks;
 static void A(bool ok,string reason="assertion failed"){if(!ok)throw new Exception(reason);}
 static void Check(string name,Action test){checks++;try{test();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Reject(Action action,Type type=null,string text=""){try{action();}catch(Exception e){A(type==null||type.IsInstanceOfType(e),"unexpected exception "+e.GetType().Name);A(text==""||e.Message.Contains(text),e.Message);return;}throw new Exception("unexpectedly accepted");}
 static void Set(object o,string name,object value){(o is MainWindow?typeof(MainWindow):o.GetType()).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
 static object Get(object o,string name){return (o is MainWindow?typeof(MainWindow):o.GetType()).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static object Call(object o,string name,params object[] args){try{return (o is MainWindow?typeof(MainWindow):o.GetType()).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
 static void Wait(Task task){var limit=DateTime.UtcNow.AddSeconds(5);while(!task.IsCompleted){Pump();Thread.Sleep(5);if(DateTime.UtcNow>limit)throw new Exception("test timeout");}task.GetAwaiter().GetResult();}
 static void Until(Func<bool> done){var limit=DateTime.UtcNow.AddSeconds(15);while(!done()){Pump();if(done())return;if(DateTime.UtcNow>limit)throw new Exception("test timeout");Thread.Sleep(5);}}
 static IEnumerable<T> Controls<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;foreach(object child in LogicalTreeHelper.GetChildren(root)){var d=child as DependencyObject;if(d!=null)foreach(var x in Controls<T>(d))yield return x;}}
 static DeviceClient Client(FakeHandler handler,int timeout=1){var d=new DeviceClient("127.0.0.1",timeout);var old=(HttpClient)Get(d,"client");old.Dispose();Set(d,"client",new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1"),Timeout=TimeSpan.FromMilliseconds(60)});return d;}
 static HttpResponseMessage Response(string body){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};}
 static Application App(){var a=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var f=File.OpenRead(Path.Combine(Store.Root,"Theme.xaml")))a.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(f));SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());return a;}
 [STAThread]static int Main(){Console.OutputEncoding=Encoding.UTF8;Store.Load();Store.Config=new Dictionary<string,object>();Run();Console.WriteLine("CHECKS="+checks+" FAILURES="+failures);return failures==0?0:1;}
 static void Run(){
Check("brightness edit preserves existing unknown values",delegate{var initial=J.D("brightness",55,"language","fr","autostart_enabled",true,"autostart_app_id","hidden");var edited=new Dictionary<string,object>(initial);edited["brightness"]=60;var latest=J.Parse("{\"brightness\":55,\"language\":\"fr\",\"autostart_enabled\":true,\"autostart_app_id\":\"hidden\",\"future\":{\"keep\":true}}");DeviceSettingsMerge.Apply(latest,initial,edited);A(J.Num(latest,"brightness")==60&&J.Str(latest,"language")=="fr"&&J.Bool(latest,"autostart_enabled")&&J.Bool(J.Map(latest,"future"),"keep"));});
Check("untouched form preserves concurrent device changes",delegate{var initial=J.D("timezone","CST-8","language","fr");var latest=J.D("timezone","UTC0","language","de");DeviceSettingsMerge.Apply(latest,initial,new Dictionary<string,object>(initial));A(J.Str(latest,"timezone")=="UTC0"&&J.Str(latest,"language")=="de");});
Check("explicit language and startup changes applied",delegate{var latest=J.D("language","fr","autostart_enabled",true,"autostart_app_id","hidden");DeviceSettingsMerge.Apply(latest,new Dictionary<string,object>(latest),J.D("language","en","autostart_enabled",false,"autostart_app_id",""));A(J.Str(latest,"language")=="en"&&!J.Bool(latest,"autostart_enabled")&&J.Str(latest,"autostart_app_id")=="");});
 Check("startup selection overrides concurrent disabled state as a pair",delegate{var initial=J.D("autostart_enabled",true,"autostart_app_id","app-a");var latest=J.D("autostart_enabled",false,"autostart_app_id","app-a");DeviceSettingsMerge.Apply(latest,initial,J.D("autostart_enabled",true,"autostart_app_id","app-b"));A(J.Bool(latest,"autostart_enabled")&&J.Str(latest,"autostart_app_id")=="app-b");});
 Check("untouched startup selection preserves both concurrent fields",delegate{var initial=J.D("autostart_enabled",true,"autostart_app_id","app-a");var latest=J.D("autostart_enabled",false,"autostart_app_id","app-b");DeviceSettingsMerge.Apply(latest,initial,new Dictionary<string,object>(initial));A(!J.Bool(latest,"autostart_enabled")&&J.Str(latest,"autostart_app_id")=="app-b");});
 Check("partial startup changes rejected before mutating settings",delegate{var initial=J.D("autostart_enabled",true,"autostart_app_id","app-a","brightness",55);var latest=new Dictionary<string,object>(initial);Reject(delegate{DeviceSettingsMerge.Apply(latest,initial,J.D("brightness",60,"autostart_app_id","app-b"));});A(J.Num(latest,"brightness")==55&&J.Str(latest,"autostart_app_id")=="app-a");});
 var app=App();var handler=new FakeHandler{Reply=delegate(HttpRequestMessage req,CancellationToken token){return Task.FromResult(Response(req.RequestUri.AbsolutePath.Contains("/fs/file")?"{\"brightness\":55,\"language\":\"fr\",\"autostart_enabled\":true,\"autostart_app_id\":\"hidden\"}":"{\"ok\":true}"));}};
using(var d=Client(handler)){var w=new MainWindow();Set(w,"runtimeStarted",true);Set(w,"device",d);Set(w,"online",true);Set(w,"state",J.D("installed_apps",new[]{J.D("id","hidden","kind","app")},"apps",new object[0]));
Check("WPF preserves unknown language and hidden startup",delegate{Call(w,"ShowPage","设备设置");Until(delegate{return Controls<Button>(w).Any(x=>Convert.ToString(x.Content)=="保存到摆件");});var boxes=Controls<ComboBox>(w).ToList();A(Convert.ToString(boxes[0].SelectedItem)=="fr");A(boxes[1].SelectedIndex>0);});
Check("WPF preserves startup missing from all lists",delegate{Set(w,"state",J.D("installed_apps",new object[0],"apps",new object[0]));Call(w,"ShowPage","设备设置");Until(delegate{return Controls<Button>(w).Any(x=>Convert.ToString(x.Content)=="保存到摆件");});A(Controls<ComboBox>(w).Skip(1).First().SelectedIndex>0);});w.StopRuntime();w.Close();}app.Shutdown();

 }
}
