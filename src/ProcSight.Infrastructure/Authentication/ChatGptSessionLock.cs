namespace ProcSight.Infrastructure.Authentication;

/// <summary>Named mutexes recover from process exit; ownership stays on a worker thread across async work.</summary>
internal static class ChatGptSessionLock
{
    internal static T Run<T>(string name, Func<T> work, CancellationToken ct = default)
    {
        using var mutex = new Mutex(false, name);
        var held = false;
        try
        {
            try
            {
                var result = WaitHandle.WaitAny([mutex, ct.WaitHandle], TimeSpan.FromMinutes(4));
                if (result == 1) throw new OperationCanceledException(ct);
                if (result == WaitHandle.WaitTimeout) throw new ChatGptAuthenticationException("Another ChatGPT session operation is still running. Try again later.", ProcSight.Core.Models.ProviderErrorKind.Transient);
                held = true;
            }
            catch (AbandonedMutexException) { held = true; }
            ct.ThrowIfCancellationRequested();
            return work();
        }
        finally { if (held) mutex.ReleaseMutex(); }
    }
    internal static Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct) => Task.Run(() => Run("ProcSight.ChatGpt.Sessions", () => work().GetAwaiter().GetResult(), ct), ct);
}
