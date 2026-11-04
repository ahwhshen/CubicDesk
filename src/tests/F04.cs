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
 static void Until(Func<bool> done){var limit=DateTime.UtcNow.AddSeconds(5);while(!done()){Pump();Thread.Sleep(5);if(DateTime.UtcNow>limit)throw new Exception("test timeout");}}
 static IEnumerable<T> Controls<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;foreach(object child in LogicalTreeHelper.GetChildren(root)){var d=child as DependencyObject;if(d!=null)foreach(var x in Controls<T>(d))yield return x;}}
 static DeviceClient Client(FakeHandler handler,int timeout=1){var d=new DeviceClient("127.0.0.1",timeout);var old=(HttpClient)Get(d,"client");old.Dispose();Set(d,"client",new HttpClient(handler){BaseAddress=new Uri("http://127.0.0.1"),Timeout=TimeSpan.FromMilliseconds(60)});return d;}
 static HttpResponseMessage Response(string body){return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};}
 static Application App(){var a=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var f=File.OpenRead(Path.Combine(Store.Root,"Theme.xaml")))a.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(f));SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());return a;}
 [STAThread]static int Main(){Console.OutputEncoding=Encoding.UTF8;Store.Load();Store.Config=new Dictionary<string,object>();Run();Console.WriteLine("CHECKS="+checks+" FAILURES="+failures);return failures==0?0:1;}
 static void Run(){
foreach(string body in new[]{"null","[]","1","\"text\"","<html>","{\"ok\":\"false\"}","{\"ok\":null}"}){string text=body;Check("reject invalid response "+body,delegate{using(var d=Client(new FakeHandler{Reply=(r,t)=>Task.FromResult(Response(text))}))Reject(delegate{d.Request("/action","POST").GetAwaiter().GetResult();},null,"无法识别");});}
Check("valid flagless object remains supported",delegate{using(var d=Client(new FakeHandler{Reply=(r,t)=>Task.FromResult(Response("{\"brightness\":55}"))}))A(J.Num(d.Request("/settings").GetAwaiter().GetResult(),"brightness")==55);});
Check("actual device error retained",delegate{using(var d=Client(new FakeHandler{Reply=(r,t)=>Task.FromResult(Response("{\"ok\":false,\"error\":\"device busy\"}"))}))Reject(delegate{d.Request("/action","POST").GetAwaiter().GetResult();},null,"device busy");});
var app=App();using(var d=Client(new FakeHandler{Reply=(r,t)=>Task.FromResult(Response("null"))})){var w=new MainWindow();Set(w,"runtimeStarted",true);Set(w,"device",d);Set(w,"online",true);Set(w,"state",J.D("installed_apps",new[]{J.D("id","svc","kind","service")},"firmware_update",J.D("current_version","1.204")));Check("invalid service response shown as failure",delegate{Call(w,"ShowPage","应用管理");Controls<Button>(w).First(x=>Convert.ToString(x.Content)=="启动").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(delegate{return !(bool)Get(w,"busy");});string message=((TextBlock)Get(w,"banner")).Text;A(message.Contains("无法识别")&&!message.Contains("已提交"));});w.StopRuntime();w.Close();}app.Shutdown();

 }
}
