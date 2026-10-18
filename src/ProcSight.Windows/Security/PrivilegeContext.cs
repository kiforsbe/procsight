using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ProcSight.Windows.Security;
public static class PrivilegeContext
{
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll",SetLastError=true)] private static extern bool OpenProcessToken(IntPtr process,uint access,out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",SetLastError=true)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token,int info,out int value,int length,out int returned);
    public static bool IsElevated {
        get {
            // Fail closed for privacy if token inspection is unavailable.
            if(!OpenProcessToken(GetCurrentProcess(),8,out var token)) return true;
            using(token) return !GetTokenInformation(token,20,out var elevated,4,out _)||elevated!=0;
        }
    }
}
