namespace ProcSight.Application.Lifecycle;
/// <summary>One cleanup operation, scheduled off the window thread and shared by all close requests.</summary>
public sealed class ShutdownWork
{
    private readonly object gate=new(); private Task? completion;
    public Task RunAsync(Func<Task> cleanup) { lock(gate) return completion??=Task.Run(cleanup); }
}
