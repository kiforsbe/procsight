using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
/// <summary>Owns one bounded real-time kernel session, never a shared logger or ETL file.</summary>
public sealed class ProcessIoMonitor : IAsyncDisposable
{
    private readonly ProcessIoAccumulator accumulator=new(); private readonly object gate=new();
    private TraceEventSession? session; private Task? worker; private bool disposed;
    private Microsoft.Diagnostics.Tracing.ETWTraceEventSource? source;
    private volatile bool running; private volatile string reason="Starting resource monitoring"; private int losses;
    public IReadOnlyDictionary<ProcessKey,ProcessIoRates> Sample(IReadOnlyList<ProcessKey> keys,DateTimeOffset now) {
        lock(gate) {
            if(disposed) return Missing(keys,"Monitoring stopped");
            worker??=Task.Run(Run);
            if(!running) return Missing(keys,reason);
            int lost;
            try { lost=session?.EventsLost??0; }
            catch { accumulator.Reset(); return Missing(keys,"Event delivery status unavailable; sampling restarts"); }
            if(lost!=losses) { losses=lost; accumulator.Reset(); return Missing(keys,"Events lost; sampling restarts"); }
        }
        return accumulator.Sample(keys,now);
    }
    private static IReadOnlyDictionary<ProcessKey,ProcessIoRates> Missing(IReadOnlyList<ProcessKey> keys,string reason)=>
        keys.ToDictionary(k=>k,_=>new ProcessIoRates(Observation<double>.Missing(reason),Observation<double>.Missing(reason)));
    private void Run() {
        try {
            if(TraceEventSession.IsElevated()!=true) { reason="Administrator permission required for process disk/network monitoring"; return; }
            using var owned=new TraceEventSession("ProcSight.IO."+Environment.ProcessId+"."+Guid.NewGuid().ToString("N")) { StopOnDispose=true,BufferSizeMB=8 };
            KernelTraceEventParser kernel;
            Microsoft.Diagnostics.Tracing.ETWTraceEventSource stream;
            lock(gate) {
                if(disposed) return;
                session=owned;
                // Enabling must precede Source: accessing Source starts the session.
                owned.EnableKernelProvider(KernelTraceEventParser.Keywords.DiskIO|KernelTraceEventParser.Keywords.NetworkTCPIP);
                source=stream=owned.Source;
                kernel=new KernelTraceEventParser(stream,KernelTraceEventParser.ParserTrackingOptions.ThreadToProcess);
            }
            static DateTimeOffset At(DateTime timestamp)=>new DateTimeOffset(timestamp).ToUniversalTime();
            kernel.DiskIORead+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),e.TransferSize,0);
            kernel.DiskIOWrite+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),e.TransferSize,0);
            kernel.TcpIpSend+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.TcpIpRecv+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.TcpIpSendIPV6+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.TcpIpRecvIPV6+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.UdpIpSend+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.UdpIpRecv+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.UdpIpSendIPV6+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.UdpIpRecvIPV6+=e=>accumulator.Add(e.ProcessID,At(e.TimeStamp),0,e.size);
            kernel.ProcessStart+=e=>accumulator.Invalidate(e.ProcessID,At(e.TimeStamp));
            kernel.ProcessStop+=e=>accumulator.Invalidate(e.ProcessID,At(e.TimeStamp));
            lock(gate) { if(disposed) return; running=true; }
            stream.Process();
            reason="Resource event stream ended";
        } catch(UnauthorizedAccessException) { reason="Administrator permission required for process disk/network monitoring"; }
        catch { reason="Process resource event monitoring unavailable"; }
        finally { running=false; lock(gate) { session=null; source=null; } accumulator.Reset(); }
    }
    // Blocking native cleanup runs off the window thread, before final window destruction.
    public void Stop() {
        TraceEventSession? owned;
        Microsoft.Diagnostics.Tracing.ETWTraceEventSource? stream;
        lock(gate) {
            disposed=true; running=false;
            owned=session; stream=source;
        }
        // Do not hold the state lock across native ETW shutdown or callback completion.
        try { stream?.StopProcessing(); } catch { }
        try { owned?.Stop(); } catch { }
    }
    public async ValueTask DisposeAsync() {
        Stop();
        Task? completion; lock(gate) completion=worker;
        if(completion is not null) try { await completion.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
    }
}
