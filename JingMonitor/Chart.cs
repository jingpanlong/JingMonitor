using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace JingMonitor;
public sealed class TrafficChart : FrameworkElement
{
    public List<(double down,double up)> Points=new();
    protected override void OnRender(DrawingContext dc)
    {
        double w=ActualWidth,h=ActualHeight-28;if(w<1||h<1)return;
        var muted=Theme.Brush("#8193AC");
        double scale=Math.Max(1024,Points.Count==0?1024:Points.Max(p=>Math.Max(p.down,p.up))*1.2);
        for(int i=0;i<5;i++){double y=i*h/4;dc.DrawLine(new Pen(Theme.Brush("#253249"),1),new Point(62,y+4),new Point(w,y+4));Text(dc,Store.Bytes(scale*(1-i/4.0))+"/s",new Point(0,y),muted,9);}
        Text(dc,"60 秒前",new Point(62,h+14),muted,10);Text(dc,"现在",new Point(w-26,h+14),muted,10);
        if(Points.Count<2)return;
        void Line(bool upload,Color c) {
            var geo=new StreamGeometry(); var coords=Points.Select((p,i)=>new Point(62+(w-62)*(60-Points.Count+i)/59.0,4+h-h*(upload?p.up:p.down)/scale)).ToArray();
            using(var ctx=geo.Open()){ctx.BeginFigure(coords[0],false,false);ctx.PolyLineTo(coords.Skip(1).ToArray(),true,false);}
            var fill=new StreamGeometry(); using(var ctx=fill.Open()){ctx.BeginFigure(new Point(coords[0].X,h+4),true,true);ctx.PolyLineTo(coords,true,false);ctx.LineTo(new Point(coords[^1].X,h+4),true,false);}
            dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(45,c.R,c.G,c.B),Color.FromArgb(0,c.R,c.G,c.B),90),null,fill);
            dc.DrawGeometry(null,new Pen(new SolidColorBrush(c),2),geo);
            dc.DrawEllipse(new SolidColorBrush(c),null,coords[^1],3,3);
        }
        Line(true,Theme.IsLight?Color.FromRgb(106,84,198):Color.FromRgb(169,151,255));Line(false,Theme.IsLight?Color.FromRgb(13,129,107):Color.FromRgb(92,228,192));
    }
    void Text(DrawingContext dc,string text,Point point,Brush brush,int size) { dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),point); }
}
