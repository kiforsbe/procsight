using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace ProcSight.Windows.Collection;
public sealed record KernelProcessStamp(int Parent,string Name,ulong Sequence);
public static class ProcessLifetimeScope
{
    public static Guid Create(Guid collectorSession,int pid,ulong sequence)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"{collectorSession:N}:{pid}:{sequence}"))[..16]);
}
public sealed class ProcessLifetimeTracker
{
    private readonly Guid session=Guid.NewGuid();
    private readonly Dictionary<int,(ulong Sequence,DateTimeOffset? Started)> observed=[];
    public ProcSight.Core.Models.ProcessKey Resolve(int pid,DateTimeOffset? started,ulong? sequence,Guid snapshot)
    {
        if(sequence is not { } proof) { observed.Remove(pid); return new(pid,started,started is null?snapshot:null); }
        if(observed.TryGetValue(pid,out var previous)&&previous.Sequence==proof) started??=previous.Started;
        observed[pid]=(proof,started);
        return new(pid,started,ProcessLifetimeScope.Create(session,pid,proof));
    }
    public void Prune(IReadOnlySet<int> live)
    {
        foreach(var pid in observed.Keys.Where(pid=>!live.Contains(pid)).ToArray()) observed.Remove(pid);
    }
}
/// <summary>Optional Windows 11 kernel sequence enumeration; no per-process access or elevation.</summary>
public static class KernelProcessLifetimeReader
{
    public static bool ConfirmsLifetime(KernelProcessStamp expected,KernelProcessStamp? actual)=>actual is not null&&expected.Sequence==actual.Sequence;
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length,MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicProcessInfo
    {
        public uint NextEntryOffset; public IntPtr Pid,Parent; public ulong Sequence; public UnicodeString ImageName;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Query(int kind,IntPtr buffer,int length,out int required);
    private static readonly Lazy<Query?> query=new(()=> {
        if(!NativeLibrary.TryLoad("ntdll.dll",typeof(KernelProcessLifetimeReader).Assembly,DllImportSearchPath.System32,out var module)) return null;
        if(NativeLibrary.TryGetExport(module,"NtQuerySystemInformation",out var address)) return Marshal.GetDelegateForFunctionPointer<Query>(address);
        NativeLibrary.Free(module); return null;
    });
    public static IReadOnlyDictionary<int,KernelProcessStamp>? Read()
    {
        if(query.Value is not { } read) return null;
        var size=65536;
        for(var attempt=0;attempt<8&&size<=8*1024*1024;attempt++) {
            var buffer=Marshal.AllocHGlobal(size);
            try {
                // SYSTEM_INFORMATION_CLASS value from the installed Windows SDK winternl.h.
                var status=read(252,buffer,size,out var required);
                if(status==unchecked((int)0xC0000004)||status==unchecked((int)0xC0000023)) {
                    if(required<0||required>8*1024*1024) return null;
                    size=Math.Max(size*2,required); continue;
                }
                if(status<0||required<=0||required>size) return null;
                return Parse(buffer,required);
            } finally { Marshal.FreeHGlobal(buffer); }
        }
        return null;
    }
    public static IReadOnlyDictionary<int,KernelProcessStamp>? Parse(IntPtr buffer,int length)
    {
        var result=new Dictionary<int,KernelProcessStamp>(); var offset=0; var entrySize=Marshal.SizeOf<BasicProcessInfo>();
        if(buffer==IntPtr.Zero||length<entrySize||length>8*1024*1024) return null;
        for(var count=0;count<65536;count++) {
            if(offset<0||offset>length-entrySize) return null;
            var entry=Marshal.PtrToStructure<BasicProcessInfo>(IntPtr.Add(buffer,offset));
            var pid=entry.Pid.ToInt64(); var parent=entry.Parent.ToInt64();
            if(pid<0||pid>int.MaxValue||parent<0||parent>int.MaxValue||entry.ImageName.Length%2!=0||entry.ImageName.Length>entry.ImageName.MaximumLength) return null;
            var name="";
            if(entry.ImageName.Length>0) {
                var relative=entry.ImageName.Buffer.ToInt64()-buffer.ToInt64();
                if(relative<0||relative>length-entry.ImageName.Length) return null;
                name=Marshal.PtrToStringUni(entry.ImageName.Buffer,entry.ImageName.Length/2)??"";
            }
            if(!result.TryAdd((int)pid,new((int)parent,name,entry.Sequence))) return null;
            if(entry.NextEntryOffset==0) return result;
            if(entry.NextEntryOffset<entrySize||entry.NextEntryOffset>length-offset-entrySize) return null;
            offset+=checked((int)entry.NextEntryOffset);
        }
        return null;
    }
}
