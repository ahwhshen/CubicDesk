using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CubicDesk {
  public sealed class DeskTrayMenu : ContextMenuStrip {
    public DeskTrayMenu(){Renderer=new DeskMenuRenderer();BackColor=Color.White;ForeColor=Color.FromArgb(38,60,89);Font=new Font("Microsoft YaHei UI",9.5f);Padding=new Padding(6,7,6,7);ShowImageMargin=false;ShowCheckMargin=false;DropShadowEnabled=true;}
    protected override void OnOpened(EventArgs e){base.OnOpened(e);using(var path=DeskMenuRenderer.Round(new Rectangle(0,0,Width,Height),10)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
  }
  public sealed class DeskMenuRenderer : ToolStripProfessionalRenderer {
    public DeskMenuRenderer(){RoundedEdges=true;}
    public static GraphicsPath Round(Rectangle rectangle,int radius){var path=new GraphicsPath();int size=radius*2;path.AddArc(rectangle.X,rectangle.Y,size,size,180,90);path.AddArc(rectangle.Right-size,rectangle.Y,size,size,270,90);path.AddArc(rectangle.Right-size,rectangle.Bottom-size,size,size,0,90);path.AddArc(rectangle.X,rectangle.Bottom-size,size,size,90,90);path.CloseFigure();return path;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Color.FromArgb(250,252,255));}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(new Rectangle(0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1),9))using(var pen=new Pen(Color.FromArgb(218,229,240)))e.Graphics.DrawPath(pen,path);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(!e.Item.Selected||!e.Item.Enabled)return;var bounds=new Rectangle(2,1,e.Item.Width-4,e.Item.Height-2);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(bounds,6))using(var brush=new LinearGradientBrush(bounds,Color.White,Color.FromArgb(231,241,252),90f))e.Graphics.FillPath(brush,path);}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(Color.FromArgb(226,234,243)))e.Graphics.DrawLine(pen,10,e.Item.Height/2,e.Item.Width-10,e.Item.Height/2);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?Color.FromArgb(38,60,89):Color.FromArgb(126,143,163);base.OnRenderItemText(e);}
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var box=e.ImageRectangle;using(var pen=new Pen(Color.FromArgb(39,111,192),1.7f))e.Graphics.DrawLines(pen,new[]{new Point(box.Left+2,box.Top+box.Height/2),new Point(box.Left+6,box.Bottom-3),new Point(box.Right-2,box.Top+3)});}
  }
}
