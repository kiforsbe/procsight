using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class WindowsSnapshotSource : IWindowsSnapshotSource
{
    private readonly ProcessSampler sampler = new();
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
        var processTable = WindowsNativeMethods.ReadProcessTable();
        var commands = ProcessMetadataReader.ReadCommandLines();
        var rows = new List<ProcessSnapshot>();
        foreach (var process in processTable)
        {
                ct.ThrowIfCancellationRequested();
                var pid = process.Key;
                var counters=WindowsNativeMethods.ReadCounters(pid);
                var started = counters.Started;
                var key = new ProcessKey(pid, started, started is null ? id : null);
                var path = WindowsNativeMethods.ReadImagePath(pid);
                var name=path is not null?Path.GetFileName(path):process.Value.Name;
                var executable = metadata.Read(name, path) with { PackageIdentity=counters.Package };
                var sample = sampler.Sample(key,counters.Cpu,counters.Memory,now,Environment.ProcessorCount);
                rows.Add(new(key,process.Value.Parent, executable, sample,
                    services.Where(s => s.HostPid == pid && (started is null || started<=servicesAt)).Select(s => s.Name).ToArray(),
                    commands.TryGetValue(pid, out var command) ? new(command, now) : null));
        }
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
