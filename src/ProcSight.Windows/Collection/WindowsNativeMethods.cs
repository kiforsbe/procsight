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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size, Usage, Pid; public UIntPtr Heap; public uint Module, Threads, Parent;
        public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    public static string? ReadImagePath(int pid)
    {
        using var handle = OpenProcess(0x1000, false, pid);
        if (handle.IsInvalid) return null;
        var buffer = new StringBuilder(32768); var size = buffer.Capacity;
        return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
    }
    public static Dictionary<int, int?> ReadParents()
    {
        var result = new Dictionary<int, int?>();
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) return result;
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" };
        if (!Process32First(snapshot, ref entry)) return result;
        do { result[(int)entry.Pid] = (int)entry.Parent; } while (Process32Next(snapshot, ref entry));
        return result;
    }
}
