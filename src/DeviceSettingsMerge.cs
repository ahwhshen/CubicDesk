using System;
using System.Collections.Generic;
namespace CubicDesk {
  public static class DeviceSettingsMerge {
    public static void Apply(Dictionary<string,object> latest,Dictionary<string,object> initial,Dictionary<string,object> edited){
      if(latest==null)throw new Exception("设备设置不是有效的 JSON 对象。");
      foreach(var pair in edited){object original;if(!initial.TryGetValue(pair.Key,out original)||!object.Equals(original,pair.Value))latest[pair.Key]=pair.Value;}
    }
  }
}
