using System;
using System.Net;
using System.Collections.Generic;
namespace CubicDesk {
  public static class UploadResponse {
    public static void EnsureSuccess(HttpStatusCode status,string text){
      int code=(int)status;bool success=code>=200&&code<300;Dictionary<string,object> data=null;
      if(!string.IsNullOrWhiteSpace(text)){try{data=J.Parse(text);}catch{}}
      string fallback="写入失败：HTTP "+code;
      if(!success){string reason=J.Str(data,"error");if(string.IsNullOrWhiteSpace(reason))reason=J.Str(data,"message");throw new Exception(string.IsNullOrWhiteSpace(reason)?fallback:reason+"（HTTP "+code+"）");}
      if(string.IsNullOrWhiteSpace(text))return;
      if(data==null||(data.ContainsKey("ok")&&!(data["ok"] is bool)))throw new Exception("设备返回了无法识别的写入响应（HTTP "+code+"）。");
      if(data.ContainsKey("ok")&&!J.Bool(data,"ok")){string reason=J.Str(data,"error");if(string.IsNullOrWhiteSpace(reason))reason=J.Str(data,"message");throw new Exception(string.IsNullOrWhiteSpace(reason)?"设备拒绝写入（HTTP "+code+"）。":reason);}
    }
  }
}
