using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace CubicDesk {
  public static class Program {
    [STAThread] public static int Main(string[] args) {
      Store.Load();
      if(args.Contains("--self-test"))return Diagnostics.Run(args);
      using(var instance=new SingleInstanceGate()){
      if(!instance.Primary){if(!args.Contains("--background"))instance.Activate();return 0;}
      try {
        var application=new ResidentApplication(instance,args.Contains("--background"));
        using(var stream=File.OpenRead(Path.Combine(Store.Root,"Theme.xaml")))application.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(stream));
        application.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){Store.Log("错误："+e.Exception.Message);MessageBox.Show(e.Exception.Message,"Cubic Desk",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};
        application.Run();return 0;
      }catch(Exception ex){Store.Log(ex.ToString());MessageBox.Show(ex.Message,"Cubic Desk 无法启动");return 1;}
      }
    }
  }
}
