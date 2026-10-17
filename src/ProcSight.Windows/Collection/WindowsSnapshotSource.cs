using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class WindowsSnapshotSource : IWindowsSnapshotSource,IAsyncDisposable
{
    private readonly ProcessIoMonitor io=new();
    private readonly ProcessGpuSampler gpu=new();
    public void Stop()=>io.Stop();
    public async ValueTask DisposeAsync() {
        await io.DisposeAsync(); await captureGate.WaitAsync();
        try { gpu.Dispose(); } finally { captureGate.Release(); }
    }
    private readonly ProcessSampler sampler = new();
    private readonly ProcessLifetimeTracker lifetimes=new();
    private readonly SemaphoreSlim captureGate = new(1);
    private readonly ProcessMetadataReader metadata = new();
    private readonly ExecutableEnricher enricher = new();
    private IReadOnlyList<ServiceSnapshot> services = [];
    private DateTimeOffset servicesAt;
    private (long Idle,long Kernel,long User)? priorSystemTimes;
    private int forceServiceRefresh;
    private int serviceCursor;
    public void RequestRefresh()=>Interlocked.Exchange(ref forceServiceRefresh,1);
    public async Task<MachineSnapshot> CaptureAsync(CommandLineMode mode, CancellationToken ct)
    {
        await captureGate.WaitAsync(ct);
        try
        {
            return await Task.Run(async () =>
            {
                var snapshot=Capture(mode,ct);
                var enriched=new List<ProcessSnapshot>();
                var remaining=16;
                foreach (var process in snapshot.Processes)
                {
                    var executable=process.Executable;
                    if(enricher.TryGetCached(executable,out var cached)) executable=cached;
                    else if(executable.FullPath is not null && remaining-->0) executable=await enricher.EnrichAsync(executable,ct);
                    enriched.Add(process with { Executable=executable });
                }
                var serviceRows=snapshot.Services.ToArray(); var budget=8;
                for(var offset=0;offset<serviceRows.Length;offset++) {
                    var index=(serviceCursor+offset)%serviceRows.Length; var service=serviceRows[index];
                    async Task<ExecutableMetadata?> Enrich(ExecutableMetadata? file) {
                        if(file is null) return null;
                        if(enricher.TryGetCached(file,out var value)) return value;
                        return budget-->0?await enricher.EnrichAsync(file,ct):file;
                    }
                    serviceRows[index]=service with { Implementation=await Enrich(service.Implementation),HostExecutable=await Enrich(service.HostExecutable),
                        BinaryPath=service.BinaryPath };
                }
                serviceCursor=serviceRows.Length==0?0:(serviceCursor+4)%serviceRows.Length;
                return snapshot with { Processes=enriched,Services=serviceRows };
            },ct);
        }
        finally { captureGate.Release(); }
    }
    private MachineSnapshot Capture(CommandLineMode mode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        if (Interlocked.Exchange(ref forceServiceRefresh,0)==1||now - servicesAt > TimeSpan.FromSeconds(10))
        { services = ServiceEnumerator.Read(); servicesAt = now; }
        var kernelTable=KernelProcessLifetimeReader.Read();
        var processTable=kernelTable is null?WindowsNativeMethods.ReadProcessTable():kernelTable.ToDictionary(p=>p.Key,p=>(p.Value.Parent,Name:string.IsNullOrWhiteSpace(p.Value.Name)?"[System Process]":p.Value.Name));
        var tableObservedAt=DateTimeOffset.UtcNow;
        var commandMetadata = ProcessMetadataReader.ReadCommandLineMetadata();
        var commands = commandMetadata.Commands;
        var rows = new List<ProcessSnapshot>();
        foreach (var process in processTable)
        {
                ct.ThrowIfCancellationRequested();
                var pid = process.Key;
                var counters=WindowsNativeMethods.ReadCounters(pid);
                if(counters.Started>tableObservedAt) continue; // PID changed after enumeration.
                var key=lifetimes.Resolve(pid,counters.Started,kernelTable?.GetValueOrDefault(pid)?.Sequence,id);
                var started=key.StartedAt;
                var pathRead = WindowsNativeMethods.ReadImagePathWithReason(pid);
                var path = pathRead.Path;
                var name=path is not null?Path.GetFileName(path):process.Value.Name;
                var executable = metadata.Read(name, path) with { PackageIdentity=counters.Package };
                var sample = sampler.Sample(key,counters.Cpu,counters.Memory,now,Environment.ProcessorCount);
                rows.Add(new(key,process.Value.Parent, executable, sample,
                    services.Where(s => s.HostPid == pid && (started is null || started<=servicesAt)).Select(s => s.Name).ToArray(),
                    commands.TryGetValue(pid, out var command) ? new(command, now) : null,
                    MetadataIssues: (pathRead.Reason is { } pathReason ? new[] { new MetadataIssue("ExecutablePath",pathReason) } : [])
                        .Concat(started is null&&counters.StartReason is { } startReason?new[]{new MetadataIssue("Started",startReason)}:[])
                        .Concat(command is null ? new[] { new MetadataIssue("CommandLine",commandMetadata.Issues.GetValueOrDefault(pid,commandMetadata.MissingReason)) } : []).ToArray()));
        }
        // Capture PID-indexed GPU counters within the initial/final kernel lifetime proof.
        var gpuRates=gpu.Sample(rows.Select(r=>r.Key).ToArray());
        if(kernelTable is not null) {
            var confirmed=KernelProcessLifetimeReader.Read();
            rows=rows.Where(r=>KernelProcessLifetimeReader.ConfirmsLifetime(kernelTable[r.Key.Pid],confirmed?.GetValueOrDefault(r.Key.Pid))).ToList();
        }
        lifetimes.Prune(rows.Select(r=>r.Key.Pid).ToHashSet());
        gpu.Retain(rows.Select(r=>r.Key).ToHashSet());
        var ioRates=io.Sample(rows.Select(r=>r.Key).ToArray(),DateTimeOffset.UtcNow);
        rows=rows.Select(r=>ioRates.TryGetValue(r.Key,out var rate)?r with { Resources=r.Resources with {
            DiskBytesPerSecond=rate.Disk,NetworkBytesPerSecond=rate.Network,
            GpuPercent=gpuRates.GetValueOrDefault(r.Key,Observation<double>.Missing("GPU sample unavailable")) } }:r).ToList();
        sampler.Prune(rows.Select(r => r.Key).ToHashSet());
        var windows=WindowMetadataReader.Read(rows.Select(r=>r.Key).ToArray());
        rows=rows.Select(p=>p with { Windows=windows.GetValueOrDefault(p.Key.Pid) }).ToList();
        var systemTimes=WindowsNativeMethods.SystemTimes(); double? cpuPercent=null;
        if(systemTimes is { } current&&priorSystemTimes is { } prior)
        {
            var total=current.Kernel+current.User-prior.Kernel-prior.User;
            if(total>0) cpuPercent=Math.Clamp((1-(current.Idle-prior.Idle)/(double)total)*100,0,100);
        }
        priorSystemTimes=systemTimes;
        var memory=WindowsNativeMethods.SystemMemory();
        return new(id, now, rows, services,new(cpuPercent,memory.Total,memory.Available));
    }
}
