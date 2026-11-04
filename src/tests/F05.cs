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
Func<FakeHandler> slow=delegate{return new FakeHandler{Reply=async delegate(HttpRequestMessage req,CancellationToken token){await Task.Delay(1000,token).ConfigureAwait(false);return Response("{\"ok\":true}");}};};
Check("JSON request timeout has accurate error",delegate{using(var d=Client(slow()))Reject(delegate{d.Request("/state").GetAwaiter().GetResult();},typeof(TimeoutException),"设备通信超时");});
Check("file read timeout has accurate error",delegate{using(var d=Client(slow()))Reject(delegate{d.Bytes("/file",CancellationToken.None).GetAwaiter().GetResult();},typeof(TimeoutException));});
Check("caller cancellation remains cancellation",delegate{using(var d=Client(slow()))using(var c=new CancellationTokenSource(10))Reject(delegate{d.Request("/state","GET",null,c.Token).GetAwaiter().GetResult();},typeof(OperationCanceledException));});
Check("bulk PUT timeout is normalized",delegate{using(var client=new HttpClient(slow()){Timeout=TimeSpan.FromMilliseconds(30)})using(var body=new ByteArrayContent(new byte[0]))Reject(delegate{DeviceRequestTimeout.Put(client,new Uri("http://127.0.0.1/upload"),body).GetAwaiter().GetResult();},typeof(TimeoutException));});
Check("successful response remains successful",delegate{using(var d=Client(new FakeHandler{Reply=(r,t)=>Task.FromResult(Response("{\"ok\":true}"))}))A(J.Bool(d.Request("/state").GetAwaiter().GetResult(),"ok"));});
var app=App();using(var d=Client(slow())){var w=new MainWindow();Set(w,"runtimeStarted",true);Check("WPF timeout is not described as cancelled search",delegate{Wait((Task)Call(w,"Run",new Func<Task>(async delegate{await d.Request("/state");}),"正在刷新",null));string message=((TextBlock)Get(w,"banner")).Text;A(message.Contains("设备通信超时")&&!message.Contains("已取消"));});Check("explicit cancellation still shows cancelled search",delegate{Wait((Task)Call(w,"Run",new Func<Task>(delegate{throw new OperationCanceledException();}),"正在搜索",null));A(((TextBlock)Get(w,"banner")).Text=="已取消搜索。");});w.StopRuntime();w.Close();}app.Shutdown();

 }
}
