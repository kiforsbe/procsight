using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class ProcessSampler
{
    private readonly Dictionary<ProcessKey, (TimeSpan Cpu, DateTimeOffset At)> previous = [];
    public ResourceSample Sample(ProcessKey key, TimeSpan? totalCpu, long? workingSet,
        DateTimeOffset now, int logicalProcessors)
    {
        var cpu = totalCpu is null ? Observation<double>.Missing("Access unavailable") : Observation<double>.Pending();
        TimeSpan? interval = null;
        if (totalCpu is { } current)
        {
            if (previous.TryGetValue(key, out var prior) && now > prior.At && current >= prior.Cpu)
            {
                interval = now - prior.At;
                cpu = Observation<double>.Available(Math.Clamp((current - prior.Cpu).TotalSeconds /
                    interval.Value.TotalSeconds / Math.Max(1, logicalProcessors) * 100, 0, 100));
            }
            previous[key] = (current, now);
        }
        return new(cpu, workingSet is { } memory ? Observation<long>.Available(memory) :
            Observation<long>.Missing("Access unavailable"), Observation<double>.Missing("Not collected"),
            Observation<double>.Missing("Not collected"), now, interval);
    }
    public void Prune(IReadOnlySet<ProcessKey> live)
    {
        foreach (var key in previous.Keys.Where(k => !live.Contains(k)).ToArray()) previous.Remove(key);
    }
}
