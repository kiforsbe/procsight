using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Tray;
public sealed class IconHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public IconHandle(IntPtr value):base(true)=>SetHandle(value);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    protected override bool ReleaseHandle()=>DestroyIcon(handle);
}
public sealed class TrayIconRenderer
{
    private readonly Queue<double?> history=[]; private TrayMetric? metric;
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo {
        public uint Size; public int Width,Height; public ushort Planes,Bits;
        public uint Compression,ImageSize; public int XPels,YPels; public uint Used,Important;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo { [MarshalAs(UnmanagedType.Bool)] public bool Icon; public uint X,Y; public IntPtr Mask,Color; }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateBitmap(int width,int height,uint planes,uint bits,byte[] pixels);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] private static extern IntPtr CreateIconIndirect(ref IconInfo info);
    public byte[] Pixels(TraySample sample,int size)
    {
        if(size is <16 or >64) throw new ArgumentOutOfRangeException(nameof(size));
        if(metric!=sample.Metric) { history.Clear(); metric=sample.Metric; }
        history.Enqueue(sample.Value); while(history.Count>30) history.Dequeue();
        var pixels=new byte[size*size*4];
        void Dot(int x,int y,byte r,byte g,byte b) { var i=(y*size+x)*4; pixels[i]=b; pixels[i+1]=g; pixels[i+2]=r; pixels[i+3]=255; }
        // High-contrast outline and a recognizable live graph without dependence on text rendering.
        for(var x=0;x<size;x++) for(var y=0;y<size;y++) if(x==0||y==size-1) Dot(x,y,230,230,230);
        if(sample.State!=TraySampleState.Available||sample.Value is null) {
            for(var x=3;x<size-3;x++) { Dot(x,x,240,180,60); if(x+1<size) Dot(x+1,x,240,180,60); }
            return pixels;
        }
        var values=history.ToArray(); var scale=sample.Unit=="B/s"?Math.Max(1024,values.Max()??1024):100;
        for(var x=2;x<size-1;x++) {
            var index=Math.Min(values.Length-1,(x-2)*values.Length/(size-3));
            var height=(int)Math.Round(Math.Clamp((values[index]??0)/scale,0,1)*(size-4));
            for(var y=size-2;y>=size-2-height;y--) Dot(x,y,30,190,245);
        }
        return pixels;
    }
    public IconHandle Render(TraySample sample,int size)
    {
        var pixels=Pixels(sample,size); var info=new BitmapInfo { Size=(uint)Marshal.SizeOf<BitmapInfo>(),Width=size,Height=-size,Planes=1,Bits=32 };
        var color=CreateDIBSection(IntPtr.Zero,ref info,0,out var bits,IntPtr.Zero,0);
        if(color==IntPtr.Zero) return new(IntPtr.Zero);
        var mask=CreateBitmap(size,size,1,1,new byte[((size+15)/16)*2*size]);
        try { if(mask==IntPtr.Zero) return new(IntPtr.Zero); Marshal.Copy(pixels,0,bits,pixels.Length);
            var icon=new IconInfo { Icon=true,Color=color,Mask=mask }; return new(CreateIconIndirect(ref icon));
        } finally { DeleteObject(color); if(mask!=IntPtr.Zero) DeleteObject(mask); }
    }
}
