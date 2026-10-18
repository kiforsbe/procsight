using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public static class SignatureVerifier
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct FileInfoData
    { public uint Size; public IntPtr Path, Handle, Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData
    {
        public uint Size; public IntPtr Policy, Sip;
        public uint UiChoice, RevocationChecks, UnionChoice; public IntPtr File;
        public uint StateAction; public IntPtr State, Url; public uint ProviderFlags, UiContext; public IntPtr Signature;
    }
    [DllImport("wintrust.dll",ExactSpelling=true)] private static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
    public static (SignatureStatus Status,string? Signer) Verify(string path)
    {
        var result=VerifyWithReason(path);
        return(result.Status,result.Signer);
    }
    public static (SignatureStatus Status,string? Signer,MetadataUnavailableReason? Reason) VerifyWithReason(string path)
    {
        var pathPointer=Marshal.StringToCoTaskMemUni(path);
        var filePointer=Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoData>());
        var action=new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        var data=new TrustData { Size=(uint)Marshal.SizeOf<TrustData>(), UiChoice=2, UnionChoice=1,
            File=filePointer, StateAction=1, ProviderFlags=0x1000 };
        try
        {
            Marshal.StructureToPtr(new FileInfoData { Size=(uint)Marshal.SizeOf<FileInfoData>(), Path=pathPointer },filePointer,false);
            var status=WinVerifyTrust(new IntPtr(-1),ref action,ref data);
            if(status is 5 or unchecked((int)0x80070005))
                return(SignatureStatus.Unavailable,null,MetadataUnavailableReason.AccessDenied);
            if(status is 2 or 3 or unchecked((int)0x80070002) or unchecked((int)0x80070003))
                return(SignatureStatus.Unavailable,null,MetadataUnavailableReason.NotFound);
            if (status!=0) return (status==unchecked((int)0x800B0100) ? SignatureStatus.Unsigned : SignatureStatus.Invalid,null,null);
            // X509CertificateLoader reads certificate files, not Authenticode PE containers.
            // Trust is established by WinVerifyTrust above; this extracts only the signer label.
#pragma warning disable SYSLIB0057
            using var cert=X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return (SignatureStatus.Verified,cert.Subject,null);
        }
        catch (Exception e) when(MetadataAccess.IsUnavailable(e) || e is System.Security.Cryptography.CryptographicException)
        { return (SignatureStatus.Unavailable,null,MetadataAccess.Reason(e)); }
        finally
        {
            data.StateAction=2; WinVerifyTrust(new IntPtr(-1),ref action,ref data);
            Marshal.FreeHGlobal(filePointer); Marshal.FreeCoTaskMem(pathPointer);
        }
    }
}
