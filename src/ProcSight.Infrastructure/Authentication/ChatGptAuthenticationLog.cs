using System.Text;
using System.Threading.Channels;
using ProcSight.Core.Models;

namespace ProcSight.Infrastructure.Authentication;

public enum ChatGptAuthStage
{
    AppStarted, DialogOpened, PanelLoaded, PanelUnloaded, AccountPickerUpdated, SignInClicked,
    SessionLock, CatalogRead, CallbackListener, BrowserLaunch, CallbackReceived,
    CallbackValidation, TokenExchange, IdentityValidation, SessionWrite, CatalogWrite,
    SignIn, DialogAccountUpdate, Models, HttpResponse
}
public enum ChatGptAuthOutcome { Started, Succeeded, Failed, Cancelled }

// Only closed, app-owned fields: no free text, account IDs, URIs, payloads or exceptions.
public sealed record ChatGptAuthEvent(ChatGptAuthStage Stage, ChatGptAuthOutcome Outcome,
    ProviderErrorKind? ErrorKind = null, int? HttpStatus = null, int? Count = null, int? HResult = null);

/// <summary>Local, bounded operational diagnostics, separate from memory-only AI activity.</summary>
public sealed class ChatGptAuthenticationLog(string path)
{
    private const long MaximumBytes = 128 * 1024;
    private readonly Channel<(DateTimeOffset Time, ChatGptAuthEvent Entry)> pending =
        Channel.CreateBounded<(DateTimeOffset, ChatGptAuthEvent)>(new BoundedChannelOptions(256) {
            SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
    private int started;
    public void Write(ChatGptAuthEvent entry)
    {
        if (!Enum.IsDefined(entry.Stage) || !Enum.IsDefined(entry.Outcome) ||
            entry.ErrorKind is { } kind && !Enum.IsDefined(kind) ||
            entry.HttpStatus is < 100 or > 599 || entry.Count is < 0 or > 512) return;
        pending.Writer.TryWrite((DateTimeOffset.UtcNow, entry));
        if (Interlocked.Exchange(ref started, 1) == 0) _ = Task.Run(DrainAsync);
    }
    private async Task DrainAsync()
    {
        await foreach (var item in pending.Reader.ReadAllAsync()) Append(item.Time, item.Entry);
    }
    private void Append(DateTimeOffset timestamp, ChatGptAuthEvent entry)
    {
        // Diagnostics must never break authentication or propagate storage errors.
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (File.Exists(path) && new FileInfo(path).Length >= MaximumBytes)
                File.Move(path, path + ".previous", true);
            var line = $"{timestamp:O} pid={Environment.ProcessId} stage={entry.Stage} outcome={entry.Outcome}";
            if (entry.ErrorKind is { } error) line += $" error={error}";
            if (entry.HttpStatus is { } status) line += $" http={status}";
            if (entry.Count is { } count) line += $" count={count}";
            if (entry.HResult is { } result) line += $" hresult=0x{result:X8}";
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }
}
