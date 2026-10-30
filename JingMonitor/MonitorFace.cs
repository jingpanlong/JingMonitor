using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace JingMonitor;
// Shared vector renderer: previews, exported gallery and live windows use exactly the same skin.
public sealed class MonitorFace : FrameworkElement
{
    public Skin Skin { get; set; }=Skin.Builtin()[0];
    public Sample? Data { get; set; }
    public bool Ball { get; set; }
    public bool Demo { get; set; }
    public bool Locked { get; set; }
    public string CollapsedEdge { get; set; } = "";
    public ImageSource? BackgroundImage { get; set; }
    public double ImageOpacity { get; set; } = 1;
    public double ImageOverlay { get; set; } = .45;
    public bool ImageFit { get; set; }
    static Color C(string c)=>(Color)ColorConverter.ConvertFromString(c);
    static SolidColorBrush B(string c,double opacity=1){var b=new SolidColorBrush(C(c)){Opacity=opacity};b.Freeze();return b;}
    protected override void OnRender(DrawingContext dc)
    {
        double width=ActualWidth,height=ActualHeight;if(width<1||height<1)return;
        if(CollapsedEdge!=""){
            dc.DrawRoundedRectangle(B(Skin.Background),new Pen(B(Skin.Accent,.7),1),new Rect(.5,.5,width-1,height-1),4,4);
            bool vertical=CollapsedEdge is "Left" or "Right";
            for(int i=-1;i<=1;i++)dc.DrawEllipse(B(Skin.Accent),null,new Point(width/2+(vertical?0:i*7),height/2+(vertical?i*7:0)),1.3,1.3);
            return;
        }
        double w=Ball?168:260,h=Ball?168:132;
        dc.PushTransform(new ScaleTransform(width/w,height/h));
        var rect=new Rect(1,1,w-2,h-2);double radius=Ball?84:Skin.Radius;
        dc.PushClip(new RectangleGeometry(rect,radius,radius));
        dc.DrawRoundedRectangle(new LinearGradientBrush(C(Skin.Background),C(Skin.Background2),35),null,rect,radius,radius);
        PaintBackground(dc,w,h);
        if(BackgroundImage!=null){
            double sx=w/Math.Max(1,BackgroundImage.Width),sy=h/Math.Max(1,BackgroundImage.Height),scale=ImageFit?Math.Min(sx,sy):Math.Max(sx,sy);
            double iw=BackgroundImage.Width*scale,ih=BackgroundImage.Height*scale;
            dc.PushOpacity(Math.Clamp(ImageOpacity,0,1));dc.DrawImage(BackgroundImage,new Rect((w-iw)/2,(h-ih)/2,iw,ih));dc.Pop();
        }
        // A quiet veil protects text contrast without obscuring the background composition.
        dc.DrawRectangle(B(BackgroundImage!=null?"#000000":Skin.Background,BackgroundImage!=null?Math.Clamp(ImageOverlay,0,.85):.28),null,new Rect(0,0,w,h));
        string down=Demo?"12.8 MB/s":Data==null?"等待采样":Store.Bytes(Data.Download)+"/s";
        string up=Demo?"1.24 MB/s":Data==null?"等待采样":Store.Bytes(Data.Upload)+"/s";
        string cpu=Demo?"24%":Data==null?"—":Data.Cpu.ToString("0")+"%";
        string mem=Demo?"46%":Data==null?"—":Data.Memory.ToString("0")+"%";
        if(Ball){
            Brand.Draw(dc,new Rect(w/2-7,8,14,14));Text(dc,"JING · LIVE",10,0,25,w,Skin.Foreground,.7);
            Text(dc,"↓ "+down,Math.Min(Skin.FontSize,17),0,52,w,Skin.Accent,1,true);
            Text(dc,"↑ "+up,12,0,80,w,Skin.Upload);
            Text(dc,(Skin.ShowCpu?"CPU "+cpu:"")+(Skin.ShowCpu&&Skin.ShowMemory?"  ·  ":"")+(Skin.ShowMemory?"MEM "+mem:""),10,0,110,w,Skin.Foreground,.85);
            dc.DrawEllipse(null,new Pen(B(Skin.Accent,.3),1),new Point(w/2,h/2),76,76);
        }else{
            Brand.Draw(dc,new Rect(17,11,15,15));Text(dc,Skin.Layout=="terminal"?"> JING / NETWORK":"JING  /  LIVE MONITOR",9,38,13,193,Skin.Foreground,.65);
            // A visible grip gives the floating card an obvious affordance for dragging.
            for(int i=0;i<3;i++)dc.DrawEllipse(B(Skin.Foreground,.45),null,new Point(234+i*4,19),.8,.8);
            if(Skin.Layout=="split"){
                Text(dc,"↓ DOWNLOAD",8,18,39,108,Skin.Accent,.85);Text(dc,down,Math.Min(Skin.FontSize+1,20),18,55,112,Skin.Accent,1,true);
                Text(dc,"↑ UPLOAD",8,142,39,100,Skin.Upload,.85);Text(dc,up,Math.Min(Skin.FontSize,18),142,55,100,Skin.Upload,1,true);
                dc.DrawLine(new Pen(B(Skin.Foreground,.13),1),new Point(130,42),new Point(130,82));
            }else{
                Text(dc,"↓ "+down,Math.Min(Skin.FontSize+3,22),18,37,222,Skin.Accent,1,true);
                Text(dc,"↑ "+up,Math.Min(Skin.FontSize,18),18,65,222,Skin.Upload);
            }
            dc.DrawLine(new Pen(B(Skin.Foreground,.13),1),new Point(18,99),new Point(242,99));
            Text(dc,(Skin.ShowCpu?"CPU  "+cpu:"")+(Skin.ShowCpu&&Skin.ShowMemory?"     ":"")+(Skin.ShowMemory?"MEM  "+mem:""),10,18,110,220,Skin.Foreground,.9);
        }
        dc.Pop();dc.DrawRoundedRectangle(null,new Pen(B(Skin.Border,.9),1),rect,radius,radius);dc.Pop();
    }
    void Text(DrawingContext dc,string text,double size,double x,double y,double width,string color,double opacity=1,bool bold=false)
    {
        if(BackgroundImage!=null)color=color==Skin.Accent?"#B8FFE6":color==Skin.Upload?"#C3DCFF":"#FFFFFF";
        var ft=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,B(color,opacity),VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(1,width),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
        if(Ball)ft.TextAlignment=TextAlignment.Center;dc.DrawText(ft,new Point(x,y));
    }
    void PaintBackground(DrawingContext dc,double w,double h)
    {
        void Ellipse(double x,double y,double rx,double ry,string c,double opacity)=>dc.DrawEllipse(B(c,opacity),null,new Point(x,y),rx,ry);
        void Line(double x,double y,double xx,double yy,string c,double opacity,double thickness=1)=>dc.DrawLine(new Pen(B(c,opacity),thickness),new Point(x,y),new Point(xx,yy));
        void Poly(string c,double opacity,params Point[] p){var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(p[0],true,true);ctx.PolyLineTo(p[1..],true,false);}dc.DrawGeometry(B(c,opacity),null,g);}
        switch(Skin.Pattern){
            case "aurora":
                for(int i=0;i<6;i++){var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(w*.35+i*14,-30),false,false);ctx.BezierTo(new Point(w*.9,25),new Point(w*.25,65),new Point(w+35,h+20),true,false);}dc.DrawGeometry(null,new Pen(B(Skin.Accent,.045+i*.008),14),g);}break;
            case "stars":
                for(int i=0;i<45;i++){double x=(i*47+33)%w,y=(i*71+15)%h;Ellipse(x,y,i%4==0?1.2:.6,i%4==0?1.2:.6,Skin.Foreground,.18+(i%3)*.08);}dc.DrawEllipse(null,new Pen(B(Skin.Accent,.22),1),new Point(w-20,28),69,69);break;
            case "glacier":
                Poly("#FFFFFF",.45,new Point(w*.5,h),new Point(w*.75,16),new Point(w+20,h));Poly("#5E9DBE",.22,new Point(w*.65,h),new Point(w*.88,48),new Point(w+30,h));Poly("#FFFFFF",.35,new Point(0,h),new Point(50,65),new Point(115,h));break;
            case "sunset":
                Ellipse(w-35,40,38,38,"#FFD99A",.6);for(int i=0;i<7;i++)Line(w-78,42+i*8,w+10,42+i*8,Skin.Background,.45,3);Poly("#512D45",.6,new Point(0,h),new Point(w*.5,100),new Point(w,80),new Point(w,h));break;
            case "petals":case "bloom":
                for(int i=0;i<9;i++){double angle=i*Math.PI*2/9;Ellipse(w-20+Math.Cos(angle)*26,38+Math.Sin(angle)*27,23,13,Skin.Pattern=="petals"?"#FFFFFF":Skin.Upload,.16);}Ellipse(w-18,38,13,13,Skin.Accent,.13);for(int i=0;i<6;i++)Ellipse((i*53)%w,(i*37)%h,5,2,Skin.Upload,.15);break;
            case "paper":
                for(int i=0;i<10;i++)Line(0,i*16,w,i*16,"#7295AC",.13);Line(w-51,0,w-51,h,"#91AABB",.18);dc.DrawRoundedRectangle(null,new Pen(B("#7C97A8",.18),1),new Rect(w-79,26,62,49),6,6);break;
            case "facets":
                Poly("#9AAEBB",.11,new Point(w*.55,0),new Point(w,0),new Point(w,h*.8));Poly("#000000",.2,new Point(w*.5,0),new Point(w*.6,h),new Point(w,h));Line(w*.55,0,w,h*.8,Skin.Foreground,.1);break;
            case "mountains":
                Poly("#8EB185",.23,new Point(70,h),new Point(w*.68,27),new Point(w+30,h));Poly("#B3D5A3",.16,new Point(w*.4,h),new Point(w*.88,50),new Point(w+35,h));for(int i=0;i<4;i++)Poly("#092E29",.5,new Point(w-62+i*19,h),new Point(w-51+i*19,70+i*6),new Point(w-40+i*19,h));break;
            case "waves":
                for(int i=0;i<7;i++){var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(-15,80+i*10),false,false);ctx.BezierTo(new Point(w*.2,-15+i*12),new Point(w*.65,h+55),new Point(w+20,15+i*14),true,false);}dc.DrawGeometry(null,new Pen(B("#FFFFFF",.16),2),g);}break;
            case "honeycomb":
                for(int row=0;row<6;row++)for(int col=0;col<9;col++){double x=col*31+(row%2)*15,y=row*27;var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(x+16,y),false,true);for(int i=1;i<6;i++)ctx.LineTo(new Point(x+16*Math.Cos(i*Math.PI/3),y+16*Math.Sin(i*Math.PI/3)),true,false);}dc.DrawGeometry(null,new Pen(B(Skin.Accent,.11),1),g);}break;
            case "grid":
                for(int i=0;i<14;i++)Line(i*24-40,h,w/2+(i-5)*9,0,Skin.Upload,.16);for(int i=0;i<8;i++)Line(0,15+i*i*2.5,w,15+i*i*2.5,Skin.Accent,.12);Ellipse(w-36,25,24,24,Skin.Upload,.12);break;
            case "clouds":
                for(int i=0;i<5;i++){Ellipse(w-45+i*15,35+(i%2)*12,43,18,"#FFFFFF",.15);Ellipse(18+i*28,h+2,45,23,"#EDF7FF",.15);}break;
            case "linen":
                for(int i=0;i<100;i++)Line(i*4,0,i*4,h,"#9C7F54",.04);for(int i=0;i<40;i++)Line(0,i*4,w,i*4,"#9C7F54",.04);dc.DrawEllipse(null,new Pen(B("#B79966",.22),2),new Point(w-15,12),54,54);break;
            case "orbits":
                for(int i=0;i<4;i++)dc.DrawEllipse(null,new Pen(B(Skin.Accent,.14),1),new Point(w-30,28),38+i*16,24+i*15);Ellipse(w-64,45,5,5,Skin.Upload,.45);Ellipse(w-9,6,10,10,Skin.Accent,.16);break;
            case "sonar":
                for(int i=0;i<6;i++)dc.DrawEllipse(null,new Pen(B(Skin.Accent,.11),1),new Point(w-9,h-6),21+i*21,21+i*21);Line(w-9,0,w-9,h,Skin.Accent,.15);Line(0,h-6,w,h-6,Skin.Accent,.15);Ellipse(w-37,60,3,3,Skin.Accent,.5);break;
        }
    }
}
