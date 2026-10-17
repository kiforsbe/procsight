using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public sealed record TableColumn(string Id,string Label,double Width,bool Processes=true,bool Services=true,bool Private=false);
public static class TableColumns
{
    public static readonly IReadOnlyList<TableColumn> All=[
        new("Name","Name",280),new("Assessment","Assessment",140),new("Pid","PID",56),
        new("Cpu","CPU",80,Services:false),new("Memory","Memory",96,Services:false),
        new("Disk","Disk",96,Services:false),new("Network","Network",100,Services:false),
        new("ServiceState","Status",108,Processes:false),new("Startup","Startup",108,Processes:false),
        new("NativeName","Native executable name",180),new("CommandLine","Command line",440,Private:true),
        new("Path","Executable path",320,Private:true),new("ParentPid","Parent PID",100,Services:false),
        new("Publisher","Verified publisher",220),new("Signature","Signature",110),
        new("Started","Started",190,Services:false),new("Owner","Ownership",190,Private:true),
        new("Windows","Window titles",300,Services:false,Private:true),
        new("ServiceName","Service name",180,Processes:false),new("Availability","Metadata availability",240)];
    public static TableColumn[] Available(bool services)=>All.Where(c=>services?c.Services:c.Processes).ToArray();
    public static TableColumnPreference[] Normalize(IEnumerable<TableColumnPreference>? saved,bool services)
    {
        var available=Available(services).ToDictionary(c=>c.Id);
        var defaults=services?new[]{"Name","Assessment","Pid","ServiceState","Startup"}:new[]{"Name","Assessment","Pid","Cpu","Memory","Disk","Network"};
        var source=saved?.Take(64).Where(c=>c is not null&&available.ContainsKey(c.Id)).DistinctBy(c=>c.Id).ToList()
            ??defaults.Select(id=>new TableColumnPreference(id,available[id].Width)).ToList();
        var name=source.FirstOrDefault(c=>c.Id=="Name")??new("Name",280);
        return new[]{name}.Concat(source.Where(c=>c.Id!="Name")).Select(c=>c with {
            Width=double.IsFinite(c.Width)?Math.Clamp(c.Width,48,2000):available[c.Id].Width }).ToArray();
    }
    public static TableColumn[] ExportColumns(bool services,bool commands,bool identifying,bool windows)=>Available(services)
        .Where(c=>!c.Private||c.Id=="CommandLine"&&commands||c.Id=="Windows"&&windows||c.Id is "Path" or "Owner"&&identifying).ToArray();
}
