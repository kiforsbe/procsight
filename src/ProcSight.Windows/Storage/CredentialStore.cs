using ProcSight.Application.Settings;
using System.Runtime.InteropServices;
using System.Text;
using ProcSight.Windows.Security;
namespace ProcSight.Windows.Storage;
public sealed class CredentialStore : ICredentialStore
{
    private static string Target(string providerId)=>"ProcSight:"+(PackageContext.Inspect().CredentialScope??throw new InvalidOperationException("Package credential scope unavailable."))+providerId;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Credential
    {
        public uint Flags,Type; public string Target; public string? Comment; public long LastWritten;
        public uint BlobSize; public IntPtr Blob; public uint Persist,AttributeCount; public IntPtr Attributes;
        public string? Alias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool Read(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool Write(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool Delete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr value);
    public string? Get(string providerId)
    {
        if(!Read(Target(providerId),1,0,out var pointer))
        { if(Marshal.GetLastWin32Error()==1168) return null; throw new System.ComponentModel.Win32Exception(); }
        try
        {
            var credential=Marshal.PtrToStructure<Credential>(pointer);
            var bytes=new byte[credential.BlobSize]; Marshal.Copy(credential.Blob,bytes,0,bytes.Length);
            try { return Encoding.UTF8.GetString(bytes); } finally { Array.Clear(bytes); }
        }
        finally { CredFree(pointer); }
    }
    public void Set(string providerId,string value)
    {
        var bytes=Encoding.UTF8.GetBytes(value);
        if(bytes.Length>2560) throw new ArgumentException("Credential exceeds Windows storage limit.");
        var pointer=Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes,0,pointer,bytes.Length);
            var credential=new Credential { Type=1,Target=Target(providerId),BlobSize=(uint)bytes.Length,
                Blob=pointer,Persist=2,UserName="ProcSight" };
            if(!Write(ref credential,0)) throw new System.ComponentModel.Win32Exception();
        }
        finally { Marshal.Copy(new byte[bytes.Length],0,pointer,bytes.Length); Marshal.FreeHGlobal(pointer); Array.Clear(bytes); }
    }
    public void Remove(string providerId)
    { if(!Delete(Target(providerId),1,0) && Marshal.GetLastWin32Error()!=1168) throw new System.ComponentModel.Win32Exception(); }
}
