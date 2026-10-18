namespace ProcSight.Windows.Tray;
/// <summary>Shell registration state, independent of window callbacks and native resource ownership.</summary>
public sealed class NotificationRegistration(Func<uint,bool> shell) : IDisposable
{
    public bool Registered { get; private set; }
    public event Action? Unavailable;
    private bool visible,disposed;
    public bool Show() {
        if(disposed) return false; visible=true;
        if(!Registered) { Registered=shell(0); if(Registered) shell(4); }
        return Registered;
    }
    public void Update() { if(visible&&!disposed) { if(Registered) { if(!shell(1)) Registered=false; } else Show(); if(!Registered) Unavailable?.Invoke(); } }
    public void ExplorerRestarted() { Registered=false; if(visible&&!disposed) Show(); }
    public void Hide() { visible=false; if(Registered) shell(2); Registered=false; }
    public void Dispose() { if(disposed) return; Hide(); disposed=true; }
}
