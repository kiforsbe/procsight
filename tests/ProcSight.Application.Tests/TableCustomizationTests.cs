using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using Xunit;
using ProcSight.Core.Identity;
using ProcSight.Tests;
namespace ProcSight.Application.Tests;
public sealed class TableCustomizationTests
{
    [Fact] public void LayoutAlwaysHasOneFirstNameAndRejectsUnknownDuplicateAndInvalidWidths()
    {
        var layout=TableColumns.Normalize([new("CommandLine",400),new("Name",double.NaN),new("Name",99),new("Bogus",99),new("Pid",-1)],false);
        Assert.Equal("Name",layout[0].Id); Assert.Single(layout,c=>c.Id=="Name");
        Assert.DoesNotContain(layout,c=>c.Id=="Bogus"); Assert.All(layout,c=>Assert.InRange(c.Width,48,2000));
        Assert.Equal(new[]{"Name","CommandLine","Pid"},layout.Select(c=>c.Id));
    }
    [Fact] public void DefaultsAndServiceAvailabilityAreSeparate()
    {
        Assert.Contains(TableColumns.Normalize(null,false),c=>c.Id=="Cpu");
        Assert.DoesNotContain(TableColumns.Normalize(null,true),c=>c.Id=="Cpu");
        Assert.Contains(TableColumns.Normalize(null,true),c=>c.Id=="ServiceState");
        Assert.DoesNotContain(TableColumns.Normalize([new("Cpu",100)],true),c=>c.Id=="Cpu");
    }
    [Theory] [InlineData("=HYPERLINK(\"x\")")] [InlineData(" +SUM(1)")] [InlineData("\t@bad")]
    public void DelimitedExportDoesNotExecuteSpreadsheetText(string value)
    {
        var text=InventoryExport.Delimited(["Name"],[new Dictionary<string,string?> { ["Name"]=value }],',');
        Assert.Contains("\"'",text);
    }
    [Fact] public void ExportHandlesQuotesNewlinesUnicodeAndJsonWithoutSpreadsheetMutation()
    {
        var rows=new[]{new Dictionary<string,string?> { ["Name"]="=\"å\"\nvalue",["Pid"]=null }};
        Assert.Contains("å\"\"\nvalue",InventoryExport.Delimited(["Name","Pid"],rows,','));
        var json=InventoryExport.Json(["Name","Pid"],rows);
        using var parsed=System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(rows[0]["Name"],parsed.RootElement[0].GetProperty("Name").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null,parsed.RootElement[0].GetProperty("Pid").ValueKind);
    }
    [Fact] public void ExportPrivateFieldsRequireTheirOwnExplicitChoice()
    {
        Assert.DoesNotContain(TableColumns.ExportColumns(false,false,false,false),c=>c.Id=="CommandLine"||c.Id=="Path"||c.Id=="Windows");
        Assert.Contains(TableColumns.ExportColumns(false,true,true,true),c=>c.Id=="CommandLine");
    }
    [Fact] public void ExcludedCommandsAndTitlesDoNotLeakThroughExportNames()
    {
        var process=Samples.Process(42) with { Executable=new("python.exe",Product:"Python",FullPath:@"C:\Private\python.exe"),
            CommandLine=new("python.exe private_script.py --secret PRIVATE",Samples.Now),Windows=[new("PRIVATE WINDOW","Fixture")] };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        var values=TableValues.Read(entity,commands:false,identifying:false,windows:false);
        Assert.Null(values["CommandLine"]); Assert.Null(values["Path"]); Assert.Null(values["Owner"]); Assert.Null(values["Windows"]);
        Assert.DoesNotContain("private_script",values["Name"]!,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE WINDOW",values["Name"]!);
        var included=TableValues.Read(entity);
        Assert.Equal(process.CommandLine.Raw,included["CommandLine"]); Assert.Equal("PRIVATE WINDOW",included["Windows"]);
    }
}
