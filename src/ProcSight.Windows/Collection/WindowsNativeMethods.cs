using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
namespace ProcSight.Windows.Collection;
internal static class WindowsNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeFileHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeFileHandle process, uint flags, StringBuilder buffer, ref int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32First(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32Next(SafeFileHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool GetProcessTimes(SafeFileHandle process,out long created,out long exited,out long kernel,out long user);
    [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(SafeFileHandle process,ref MemoryCounters counters,uint size);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetPackageFullName(SafeFileHandle process,ref uint length,System.Text.StringBuilder? name);
    [StructLayout(LayoutKind.Sequential)] private struct MemoryCounters
    {
        public uint Size,PageFaults; public UIntPtr PeakWorkingSet,WorkingSet,QuotaPeakPaged,QuotaPaged,
            QuotaPeakNonPaged,QuotaNonPaged,Pagefile,PeakPagefile,PrivateUsage;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length,Load; public ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,
            TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr Heap; public uint Module, Threads, Parent;
        public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    public static string? ReadImagePath(int pid)
        => ReadImagePathWithReason(pid).Path;
    public static (string? Path, ProcSight.Core.Models.MetadataUnavailableReason? Reason) ReadImagePathWithReason(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid);
        if (handle.IsInvalid) return (null, MetadataAccess.NativeReason(Marshal.GetLastWin32Error()));
        var buffer = new StringBuilder(32768); var size = buffer.Capacity;
        return QueryFullProcessImageName(handle, 0, buffer, ref size) ? (buffer.ToString(), null)
            : (null, MetadataAccess.NativeReason(Marshal.GetLastWin32Error()));
    }
    public static Dictionary<int, int?> ReadParents()
    {
        return ReadProcessTable().ToDictionary(p=>p.Key,p=>(int?)p.Value.Parent);
    }
    public static Dictionary<int,(int Parent,string Name)> ReadProcessTable()
    {
        var result = new Dictionary<int,(int Parent,string Name)>();
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) return result;
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" };
        if (!Process32First(snapshot, ref entry)) return result;
        do { result[(int)entry.Pid] = ((int)entry.Parent,entry.Name); } while (Process32Next(snapshot, ref entry));
        return result;
    }
    public static (DateTimeOffset? Started,TimeSpan? Cpu,long? Memory,string? Package) ReadCounters(int pid)
    {
        using var process=OpenProcess(0x1000,false,pid);
        if(process.IsInvalid) return(null,null,null,null);
        DateTimeOffset? started=null; TimeSpan? cpu=null;
        if(GetProcessTimes(process,out var created,out _,out var kernel,out var user))
        { started=new DateTimeOffset(DateTime.FromFileTimeUtc(created)); cpu=TimeSpan.FromTicks(kernel+user); }
        var memory=new MemoryCounters { Size=(uint)Marshal.SizeOf<MemoryCounters>() };
        long? workingSet=GetProcessMemoryInfo(process,ref memory,memory.Size)?(long)memory.WorkingSet.ToUInt64():null;
        string? package=null; uint length=0;
        if(GetPackageFullName(process,ref length,null)==122 && length is >0 and <32768)
        { var buffer=new StringBuilder((int)length); if(GetPackageFullName(process,ref length,buffer)==0) package=buffer.ToString(); }
        return(started,cpu,workingSet,package);
    }
    public static (long Idle,long Kernel,long User)? SystemTimes()=>GetSystemTimes(out var idle,out var kernel,out var user)?(idle,kernel,user):null;
    public static (long? Total,long? Available) SystemMemory()
    {
        var status=new MemoryStatus { Length=(uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status)?((long)status.TotalPhysical,(long)status.AvailablePhysical):(null,null);
    }
}
