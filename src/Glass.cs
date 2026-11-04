// 用途：绘制白色液态玻璃、柔和光晕与原生窗口交互效果。
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace CubicDesk {
  // Draw the white halo as many individually attenuated contours, without blurring text.
  public sealed class GlassSurface : Border {
    Point light=new Point(.75,.12);
    public double HaloSize=12;
    public static readonly DependencyProperty ShineProperty=DependencyProperty.Register("Shine",typeof(double),typeof(GlassSurface),new FrameworkPropertyMetadata(.22,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Shine {get{return (double)GetValue(ShineProperty);}set{SetValue(ShineProperty,value);}}
    public GlassSurface(){
      CornerRadius=new CornerRadius(20);BorderThickness=new Thickness(1);BorderBrush=new SolidColorBrush(Color.FromArgb(230,255,255,255));SnapsToDevicePixels=true;
      Background=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Color.FromArgb(242,255,255,255),0),new GradientStop(Color.FromArgb(183,255,255,255),.47),new GradientStop(Color.FromArgb(219,246,250,255),1)},new Point(0,0),new Point(1,1));
      MouseEnter+=delegate{AnimateShine(.55);};MouseLeave+=delegate{AnimateShine(.22);};
      MouseMove+=delegate(object sender,MouseEventArgs e){var p=e.GetPosition(this);light=new Point(Math.Max(0,Math.Min(1,p.X/Math.Max(1,ActualWidth))),Math.Max(0,Math.Min(1,p.Y/Math.Max(1,ActualHeight))));InvalidateVisual();};
    }
    void AnimateShine(double value){BeginAnimation(ShineProperty,new DoubleAnimation(value,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});}
    protected override void OnRender(DrawingContext dc){
      if(ActualWidth<2||ActualHeight<2)return;
      double radius=CornerRadius.TopLeft;
      for(double distance=HaloSize;distance>=.8;distance-=1){
        double attenuation=Math.Exp(-distance/(HaloSize/3.5));
        byte alpha=(byte)Math.Min(185,140*attenuation);
        var pen=new Pen(new SolidColorBrush(Color.FromArgb(alpha,255,255,255)),1.8);
        dc.DrawRoundedRectangle(null,pen,new Rect(-distance,-distance,ActualWidth+2*distance,ActualHeight+2*distance),radius+distance,radius+distance);
      }
      base.OnRender(dc);
      var inside=new Rect(1,1,Math.Max(0,ActualWidth-2),Math.Max(0,ActualHeight-2));
      dc.PushClip(new RectangleGeometry(inside,Math.Max(0,radius-1),Math.Max(0,radius-1)));
      var reflection=new RadialGradientBrush {Center=light,GradientOrigin=light,RadiusX=.88,RadiusY=1.05};
      reflection.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Shine*180),255,255,255),0));reflection.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),1));
      dc.DrawRectangle(reflection,null,inside);
      var topLight=new LinearGradientBrush(Color.FromArgb(150,255,255,255),Color.FromArgb(0,255,255,255),new Point(0,0),new Point(0,1));
      dc.DrawRectangle(topLight,null,new Rect(1,1,Math.Max(0,ActualWidth-2),Math.Min(64,ActualHeight/3)));
      dc.Pop();
      var rim=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Colors.White,0),new GradientStop(Color.FromArgb(130,255,255,255),.45),new GradientStop(Color.FromArgb(200,226,239,253),.72),new GradientStop(Colors.White,1)},new Point(0,0),new Point(1,1));
      dc.DrawRoundedRectangle(null,new Pen(rim,1.15),new Rect(.6,.6,Math.Max(0,ActualWidth-1.2),Math.Max(0,ActualHeight-1.2)),radius,radius);
    }
  }
  public sealed class GlassBackdrop : FrameworkElement {
    protected override void OnRender(DrawingContext dc){
      if(ActualWidth<=0||ActualHeight<=0)return;
      Rect bounds=new Rect(0,0,ActualWidth,ActualHeight);
      var baseFill=new LinearGradientBrush(new GradientStopCollection {new GradientStop((Color)ColorConverter.ConvertFromString("#EFF5FC"),0),new GradientStop((Color)ColorConverter.ConvertFromString("#E6EFF9"),.5),new GradientStop((Color)ColorConverter.ConvertFromString("#F8FAFF"),1)},new Point(0,0),new Point(1,1));
      dc.DrawRoundedRectangle(baseFill,null,bounds,27,27);
      dc.PushClip(new RectangleGeometry(bounds,27,27));
      Cloud(dc,bounds,new Point(.78,.15),Color.FromArgb(125,195,220,255),.65,.5);
      Cloud(dc,bounds,new Point(.18,.80),Color.FromArgb(88,198,230,240),.55,.64);
      Cloud(dc,bounds,new Point(.8,.88),Color.FromArgb(100,233,223,252),.60,.60);
      Cloud(dc,bounds,new Point(.45,.32),Color.FromArgb(230,255,255,255),.46,.55);
      dc.Pop();
    }
    static void Cloud(DrawingContext dc,Rect bounds,Point at,Color color,double x,double y){var brush=new RadialGradientBrush {Center=at,GradientOrigin=at,RadiusX=x,RadiusY=y};brush.GradientStops.Add(new GradientStop(color,0));brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,color.R,color.G,color.B),1));dc.DrawRectangle(brush,null,bounds);}
  }
  public static class GlassWindow {
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam);
    public static void Attach(Window window,UIElement content){
      window.Content=null;window.WindowStyle=WindowStyle.None;window.AllowsTransparency=true;window.Background=Brushes.Transparent;window.ResizeMode=ResizeMode.CanResize;
      var outside=new Grid {Background=Brushes.Transparent};
      var frame=new GlassSurface {Margin=new Thickness(26),CornerRadius=new CornerRadius(28),HaloSize=25,Padding=new Thickness(1),Background=Brushes.Transparent};
      var body=new Grid();body.Children.Add(new GlassBackdrop());
      var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(42)});layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
      var chrome=new Grid {Background=Brushes.Transparent,Margin=new Thickness(19,2,12,0)};
      chrome.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});chrome.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
      var title=new TextBlock {Text="◇  "+window.Title,FontSize=11,Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#667E9A")),VerticalAlignment=VerticalAlignment.Center};chrome.Children.Add(title);
      var controls=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(controls,1);chrome.Children.Add(controls);
      controls.Children.Add(ChromeButton("—","最小化",delegate{window.WindowState=WindowState.Minimized;}));
      controls.Children.Add(ChromeButton("□","最大化或还原",delegate{ToggleMax(window,frame);}));
      controls.Children.Add(ChromeButton("×","关闭",delegate{window.Close();}));
      chrome.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2)ToggleMax(window,frame);else if(e.LeftButton==MouseButtonState.Pressed){if(window.WindowState==WindowState.Maximized){window.WindowState=WindowState.Normal;frame.Margin=new Thickness(26);}window.DragMove();}};
      layout.Children.Add(chrome);Grid.SetRow(content,1);layout.Children.Add(content);body.Children.Add(layout);frame.Child=body;outside.Children.Add(frame);
      var grip=new TextBlock {Text="◢",FontSize=16,Foreground=new SolidColorBrush(Color.FromArgb(130,153,181,216)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,36,32),Cursor=Cursors.SizeNWSE,ToolTip="拖动调整窗口大小"};
      grip.MouseLeftButtonDown+=delegate{var handle=new WindowInteropHelper(window).Handle;SendMessage(handle,0x112,new IntPtr(0xF008),IntPtr.Zero);};outside.Children.Add(grip);
      window.StateChanged+=delegate{frame.Margin=window.WindowState==WindowState.Maximized?new Thickness(5):new Thickness(26);grip.Visibility=window.WindowState==WindowState.Maximized?Visibility.Collapsed:Visibility.Visible;};
      window.Content=outside;
    }
    static Button ChromeButton(string text,string label,Action action){var b=new Button {Content=text,Width=34,Height=28,MinHeight=28,Padding=new Thickness(0),Margin=new Thickness(3,0,0,0),BorderThickness=new Thickness(0),Background=new SolidColorBrush(Color.FromArgb(100,255,255,255)),FontSize=15,ToolTip=label};AutomationProperties.SetName(b,label);b.Click+=delegate{action();};return b;}
    static void ToggleMax(Window window,GlassSurface frame){if(window.WindowState==WindowState.Maximized){window.WindowState=WindowState.Normal;frame.Margin=new Thickness(26);}else{window.MaxHeight=SystemParameters.WorkArea.Height;window.WindowState=WindowState.Maximized;frame.Margin=new Thickness(5);}}
  }
}
