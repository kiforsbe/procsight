using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public enum PerformanceSubsystem { Overview,Cpu,Memory,Disks,Network,Gpu }
public sealed record PerformanceSnapshot(DateTimeOffset At,Observation<double> Cpu,Observation<double> Memory,
    Observation<double> DiskBusy,Observation<double> DiskBytes,Observation<double> NetworkBytes,
    Observation<double> Gpu,long? TotalMemory,long? AvailableMemory,int LogicalProcessors,
    Observation<double>? DiskRead=null,Observation<double>? DiskWrite=null,
    Observation<double>? NetworkSent=null,Observation<double>? NetworkReceived=null,string? CpuName=null)
{
    public Observation<double> Metric(PerformanceSubsystem subsystem)=>subsystem switch {
        PerformanceSubsystem.Cpu=>Cpu,PerformanceSubsystem.Memory=>Memory,PerformanceSubsystem.Disks=>DiskBusy,
        PerformanceSubsystem.Network=>NetworkBytes,PerformanceSubsystem.Gpu=>Gpu,_=>Cpu };
}
