using System;
using System.Collections.Generic;
namespace CubicDesk {
  public static class DeviceSettingsMerge {
    public static void Apply(Dictionary<string,object> latest,Dictionary<string,object> initial,Dictionary<string,object> edited){
      if(latest==null)throw new Exception("设备设置不是有效的 JSON 对象。");
      bool startupChanged=Changed(initial,edited,"autostart_enabled")||Changed(initial,edited,"autostart_app_id");
      if(startupChanged&&(!edited.ContainsKey("autostart_enabled")||!edited.ContainsKey("autostart_app_id")))throw new ArgumentException("开机应用和启用状态必须一起提交。");
      foreach(var pair in edited){
        bool startupField=pair.Key=="autostart_enabled"||pair.Key=="autostart_app_id";
        if(startupField?startupChanged:Changed(initial,edited,pair.Key))latest[pair.Key]=pair.Value;
      }
    }
    static bool Changed(Dictionary<string,object> initial,Dictionary<string,object> edited,string key){object value,original;return edited.TryGetValue(key,out value)&&(!initial.TryGetValue(key,out original)||!object.Equals(original,value));}
  }
}
