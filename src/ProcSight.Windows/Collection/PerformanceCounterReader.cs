using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ProcSight.Windows.Collection;
public sealed class PerformanceCounterReader : IDisposable
{
    private sealed class QueryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public QueryHandle():base(true) { }
        protected override bool ReleaseHandle()=>PdhCloseQuery(handle)==0;
    }
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] private static extern uint PdhOpenQueryW(string? source,UIntPtr data,out QueryHandle query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] private static extern uint PdhAddEnglishCounterW(QueryHandle query,string path,UIntPtr data,out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(QueryHandle query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter,uint format,ref uint bytes,out uint count,IntPtr values);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);
    [StructLayout(LayoutKind.Sequential)] private struct Item { public IntPtr Name; public uint Status; public double Value; }
    private QueryHandle? query; private IntPtr counter; private bool sampled;
    public PerformanceCounterReader(string path)
    {
        if(PdhOpenQueryW(null,UIntPtr.Zero,out var opened)==0) {
            query=opened;
            if(PdhAddEnglishCounterW(query,path,UIntPtr.Zero,out counter)!=0) Dispose();
        } else opened?.Dispose();
    }
    public (double? Value,bool Warming) Read(bool idle=false,bool percentage=true)
    {
        if(query is null||query.IsInvalid||PdhCollectQueryData(query)!=0) return(null,false);
        if(!sampled) { sampled=true; return(null,true); }
        uint bytes=0; var status=PdhGetFormattedCounterArrayW(counter,0x200,ref bytes,out var count,IntPtr.Zero);
        if(status!=0x800007D2||bytes==0||bytes>1048576||count>4096) return(null,false);
        var buffer=Marshal.AllocHGlobal((int)bytes);
        try {
            if(PdhGetFormattedCounterArrayW(counter,0x200,ref bytes,out count,buffer)!=0||count>4096||count*(long)Marshal.SizeOf<Item>()>bytes) return(null,false);
            double? max=null;
            for(var i=0;i<count;i++) {
                var item=Marshal.PtrToStructure<Item>(buffer+i*Marshal.SizeOf<Item>());
                if(item.Status>1||!double.IsFinite(item.Value)||item.Value<0) continue;
                var value=idle?100-item.Value:item.Value; max=Math.Max(max??0,percentage?Math.Clamp(value,0,100):Math.Max(0,value));
            }
            return(max,false);
        } finally { Marshal.FreeHGlobal(buffer); }
    }
    public void Dispose() { query?.Dispose(); query=null; }
}
