using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JingMonitor;
public static class Brand
{
    public static void Draw(DrawingContext dc,Rect target)
    {
        dc.PushTransform(new TranslateTransform(target.X,target.Y));dc.PushTransform(new ScaleTransform(target.Width/64,target.Height/64));
        dc.PushClip(new RectangleGeometry(new Rect(0,0,64,64),16,16));
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(13,148,136),Color.FromRgb(30,64,175),45),null,new Rect(0,0,64,64),16,16);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(24,255,255,255)),null,new Point(54,7),31,31);
        var pen=new Pen(Brushes.White,5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        dc.DrawGeometry(null,pen,Geometry.Parse("M 20,18 L 20,36 C 20,48 30,48 35,38 L 45,22 M 35,24 L 45,22 L 47,32"));
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(167,243,208)),null,new Point(20,12),2.4,2.4);dc.Pop();dc.Pop();dc.Pop();
    }
    public static BitmapSource Image(int size)
    {
        var v=new DrawingVisual();using(var dc=v.RenderOpen())Draw(dc,new Rect(0,0,size,size));
        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(v);bitmap.Freeze();return bitmap;
    }
    static byte[] Png(int size){var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(Image(size)));using var s=new MemoryStream();e.Save(s);return s.ToArray();}
    static byte[] Ico()
    {
        int[] sizes={16,20,24,32,48,64,128,256};var png=sizes.Select(Png).ToArray();using var s=new MemoryStream();using var w=new BinaryWriter(s);w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
        for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(png[i].Length);w.Write(offset);offset+=png[i].Length;}foreach(var p in png)w.Write(p);return s.ToArray();
    }
    public static System.Drawing.Icon Icon(){using var stream=new MemoryStream(Ico());using var icon=new System.Drawing.Icon(stream);return (System.Drawing.Icon)icon.Clone();}
    public static void Export(string folder){Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"app.ico"),Ico());File.WriteAllBytes(Path.Combine(folder,"logo.png"),Png(256));}
}
public sealed class BrandLogo:FrameworkElement{protected override void OnRender(DrawingContext dc)=>Brand.Draw(dc,new Rect(0,0,ActualWidth,ActualHeight));}
