using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace CubicDesk {
  public static class ProvisioningTransport {
    public static async Task<Dictionary<string,object>> Connect(DeviceClient client,string target,string password,CancellationToken token){
      try{return await client.Request("/api/system/wifi/connect","POST",J.D("ssid",target,"pwd",password),token);}
      catch(HttpRequestException){return null;}
      catch(TimeoutException){return null;}
      catch(TaskCanceledException){if(token.IsCancellationRequested)throw;return null;}
    }
  }
}
