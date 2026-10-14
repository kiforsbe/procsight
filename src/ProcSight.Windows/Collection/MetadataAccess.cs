using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
/// <summary>Local, app-owned reason codes; never retains exception messages or paths.</summary>
public static class MetadataAccess
{
    public static bool IsUnavailable(Exception error) => error is IOException or UnauthorizedAccessException
        or SecurityException or Win32Exception or ManagementException or COMException or ArgumentException;
    public static MetadataUnavailableReason Reason(Exception error) => error switch
    {
        UnauthorizedAccessException or SecurityException => MetadataUnavailableReason.AccessDenied,
        Win32Exception native => NativeReason(native.NativeErrorCode),
        ManagementException { ErrorCode: ManagementStatus.AccessDenied } => MetadataUnavailableReason.AccessDenied,
        COMException { HResult: unchecked((int)0x80070005) } => MetadataUnavailableReason.AccessDenied,
        COMException { HResult: unchecked((int)0x80041003) } => MetadataUnavailableReason.AccessDenied,
        FileNotFoundException or DirectoryNotFoundException => MetadataUnavailableReason.NotFound,
        _ => MetadataUnavailableReason.ReadFailed
    };
    public static MetadataUnavailableReason NativeReason(int error) => error switch
    {
        5 => MetadataUnavailableReason.AccessDenied,
        2 or 3 => MetadataUnavailableReason.NotFound,
        _ => MetadataUnavailableReason.ReadFailed
    };
}
