using System.Diagnostics;
using System.Runtime.InteropServices;
// Synthetic runtime regression only: keep one object alive across balanced COM calls.
// Full collections below measure retained growth; never copy them into production sampling.
var wrappers = new Wrappers();
var value = new object();
void Round(int count) { for(int i=0;i<count;i++) Marshal.Release(wrappers.GetOrCreateComInterfaceForObject(value, CreateComInterfaceFlags.None)); }
Round(1000);
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long before=GC.GetTotalMemory(true);
var watch=Stopwatch.StartNew(); Round(1_000_000); watch.Stop();
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
Console.WriteLine($"runtime={Environment.Version} retainedGrowthBytes={GC.GetTotalMemory(true)-before} elapsedMs={watch.ElapsedMilliseconds}");
GC.KeepAlive(value); GC.KeepAlive(wrappers);
unsafe class Wrappers : ComWrappers {
 protected override ComInterfaceEntry* ComputeVtables(object obj, CreateComInterfaceFlags flags, out int count) { count=0; return null; }
 protected override object CreateObject(IntPtr ptr, CreateObjectFlags flags) => throw new NotSupportedException();
 protected override void ReleaseObjects(System.Collections.IEnumerable objects) => throw new NotSupportedException();
}
