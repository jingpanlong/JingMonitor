using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace JingMonitor;
public static class BackgroundImages
{
    public static BitmapImage Load(string path)
    {
        if(new FileInfo(path).Length>20*1024*1024)throw new Exception("图片文件不能超过 20 MB");
        using var stream=File.OpenRead(path);
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None);
        int w=decoder.Frames[0].PixelWidth,h=decoder.Frames[0].PixelHeight;
        if(w<=0||h<=0||w>32768||h>32768||(long)w*h>150_000_000)throw new Exception("图片分辨率过大，请缩小后再导入");
        int decodeWidth=Math.Max(1,(int)(w*Math.Min(1,1600d/Math.Max(w,h))));stream.Position=0;
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=decodeWidth;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
    public static string Import(string path)
    {
        var image=Load(path);string folder=Path.Combine(Store.Root,"backgrounds");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(file))encoder.Save(output);return file;
    }
}
