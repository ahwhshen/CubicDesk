using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace CubicDesk {
  public static class DeviceRequestTimeout {
    public static Task<HttpResponseMessage> Get(HttpClient client,string route,CancellationToken token){return Await(delegate{return client.GetAsync(route,token);},token);}
    public static Task<HttpResponseMessage> Send(HttpClient client,HttpRequestMessage request,CancellationToken token){return Await(delegate{return client.SendAsync(request,token);},token);}
    public static Task<HttpResponseMessage> Put(HttpClient client,Uri address,HttpContent content){return Await(delegate{return client.PutAsync(address,content);},CancellationToken.None);}
    static async Task<HttpResponseMessage> Await(Func<Task<HttpResponseMessage>> send,CancellationToken token){
      try{return await send();}
      catch(OperationCanceledException ex){if(token.IsCancellationRequested)throw;throw new TimeoutException("设备通信超时，请检查连接后重试。",ex);}
    }
  }
}
