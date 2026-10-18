using System.Text;
using System.Text.Json;
using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
/// <summary>Explicit local export only. Never reuse inventory exports as provider payloads or diagnostics.</summary>
public static class InventoryExport
{
    public static ResolvedEntity[] ExpandSelection(IEnumerable<ProcessListRow> rows)=>rows.SelectMany(row=>row.Members)
        .DistinctBy(entity=>entity.Process is { } process?"process|"+process.Key:"service|"+(entity.Service?.Name??entity.Identity.Value)).ToArray();
    public static string Json(IReadOnlyList<string> columns,IEnumerable<IReadOnlyDictionary<string,string?>> rows)=>
        JsonSerializer.Serialize(rows.Select(row=>columns.ToDictionary(id=>id,id=>row.GetValueOrDefault(id))),new JsonSerializerOptions { WriteIndented=true });
    public static string Delimited(IReadOnlyList<string> columns,IEnumerable<IReadOnlyDictionary<string,string?>> rows,char separator)
    {
        if(separator is not (',' or '\t')) throw new ArgumentOutOfRangeException(nameof(separator));
        string Cell(string? value) {
            value??="";
            if(value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@'||value.FirstOrDefault() is '\t' or '\r' or '\n') value="'"+value;
            return "\""+value.Replace("\"","\"\"")+"\"";
        }
        var text=new StringBuilder(); text.AppendLine(string.Join(separator,columns.Select(Cell)));
        foreach(var row in rows) text.AppendLine(string.Join(separator,columns.Select(id=>Cell(row.GetValueOrDefault(id)))));
        return text.ToString();
    }
}
