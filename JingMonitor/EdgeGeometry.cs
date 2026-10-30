using System;
using System.Drawing;

namespace JingMonitor;
public static class EdgeGeometry
{
    public static bool IsSide(string s)=>s is "Left" or "Right" or "Top" or "Bottom";
    public static string Nearest(Rectangle r,Rectangle a,int distance)
    {
        int[] d={Math.Abs(r.Left-a.Left),Math.Abs(a.Right-r.Right),Math.Abs(r.Top-a.Top),Math.Abs(a.Bottom-r.Bottom)};
        int min=0;for(int i=1;i<4;i++)if(d[i]<d[min])min=i;
        return d[min]<=distance?new[]{"Left","Right","Top","Bottom"}[min]:"";
    }
    public static Rectangle Anchor(Rectangle r,Rectangle a,string side)
    {
        int w=Math.Min(r.Width,a.Width),h=Math.Min(r.Height,a.Height);
        int x=Math.Clamp(r.X,a.Left,Math.Max(a.Left,a.Right-w)),y=Math.Clamp(r.Y,a.Top,Math.Max(a.Top,a.Bottom-h));
        return new Rectangle(side=="Left"?a.Left:side=="Right"?a.Right-w:x,side=="Top"?a.Top:side=="Bottom"?a.Bottom-h:y,w,h);
    }
    public static Rectangle Tab(Rectangle full,string side,int thickness,int length)
    {
        length=Math.Min(length,side is "Left" or "Right"?full.Height:full.Width);
        return side switch {"Left"=>new(full.Left,full.Top+(full.Height-length)/2,thickness,length),"Right"=>new(full.Right-thickness,full.Top+(full.Height-length)/2,thickness,length),"Top"=>new(full.Left+(full.Width-length)/2,full.Top,length,thickness),_=>new(full.Left+(full.Width-length)/2,full.Bottom-thickness,length,thickness)};
    }
}
