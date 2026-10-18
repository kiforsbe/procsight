using System.Runtime.InteropServices;
namespace ProcSight.Windows.Collection;
public sealed record ExecutableIcon(byte[] Pixels,int Width=32,int Height=32);
public static class ExecutableIconReader
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ShellInfo {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Display;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string Type;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo {
        public uint Size; public int Width,Height; public ushort Planes,Bits;
        public uint Compression,ImageSize; public int X,Y; public uint Used,Important;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SHGetFileInfoW(string path,uint attrs,out ShellInfo info,uint size,uint flags);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
    public static ExecutableIcon? Read(string? path)
    {
        if(string.IsNullOrWhiteSpace(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||!File.Exists(path)) return null;
        var initialized=CoInitializeEx(IntPtr.Zero,2)>=0;
        IntPtr icon=IntPtr.Zero,dc=IntPtr.Zero,bitmap=IntPtr.Zero,old=IntPtr.Zero;
        try {
            if(SHGetFileInfoW(path,0,out var info,(uint)Marshal.SizeOf<ShellInfo>(),0x100)==IntPtr.Zero) return null;
            icon=info.Icon; if(icon==IntPtr.Zero) return null;
            dc=CreateCompatibleDC(IntPtr.Zero); if(dc==IntPtr.Zero) return null;
            var format=new BitmapInfo { Size=(uint)Marshal.SizeOf<BitmapInfo>(),Width=32,Height=-32,Planes=1,Bits=32 };
            bitmap=CreateDIBSection(dc,ref format,0,out var bits,IntPtr.Zero,0);
            if(bitmap==IntPtr.Zero) return null;
            old=SelectObject(dc,bitmap);
            var pixels=new byte[32*32*4]; Marshal.Copy(pixels,0,bits,pixels.Length);
            if(!DrawIconEx(dc,0,0,icon,32,32,0,IntPtr.Zero,3)) return null;
            Marshal.Copy(bits,pixels,0,pixels.Length);
            // Legacy non-alpha icons: opaque fallback, preserving their actual executable artwork.
            if(!Enumerable.Range(0,1024).Any(i=>pixels[i*4+3]!=0))
                for(var i=0;i<1024;i++) pixels[i*4+3]=255;
            return new(pixels);
        } finally {
            if(old!=IntPtr.Zero&&dc!=IntPtr.Zero) SelectObject(dc,old);
            if(bitmap!=IntPtr.Zero) DeleteObject(bitmap);
            if(dc!=IntPtr.Zero) DeleteDC(dc);
            if(icon!=IntPtr.Zero) DestroyIcon(icon);
            if(initialized) CoUninitialize();
        }
    }
}
