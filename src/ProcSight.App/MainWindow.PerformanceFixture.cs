#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Synthetic UI-thread timings only. Never collect a machine inventory or provider data here.
    private async Task CheckUiPerformanceAsync(string output)
    {
        var results=new List<object>();
        var seed=lastSnapshot.Processes.ToArray();
        async Task Measure(string name,Action<int> work,int count,bool layout=true)
        {
            var elapsed=new List<double>(); var allocated=new List<long>();
            for(var i=0;i<count;i++) {
                var bytes=GC.GetAllocatedBytesForCurrentThread(); var start=Stopwatch.GetTimestamp();
                work(i); if(layout) WindowRoot.UpdateLayout();
                elapsed.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                allocated.Add(GC.GetAllocatedBytesForCurrentThread()-bytes);
                await Task.Delay(16);
            }
            var sorted=elapsed.Order().ToArray();
            results.Add(new { name,samples=count,meanMs=elapsed.Average(),p50Ms=sorted[count/2],
                p95Ms=sorted[Math.Min(count-1,(int)(count*.95))],maxMs=sorted[^1],meanAllocatedBytes=allocated.Average(),
                over16ms=elapsed.Count(v=>v>16.67),realizedRows=RealizedColumnRows().Count(),columns=tableColumnLayout.Length });
            File.WriteAllText(output+".performance.json",JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }));
        }
        foreach(var count in new[]{500,1000}) {
            var processes=Enumerable.Range(0,count).Select(i=>seed[i%seed.Length] with {
                Key=new(10000+i,seed[0].Key.StartedAt,null),
                Executable=new(i%3==0?"python.exe":i%3==1?"node.exe":"example.exe",$@"C:\Synthetic\Example\worker-{i%20}.exe",Product:"Synthetic application"),
                CommandLine=new(i%3==0?$"python.exe synthetic_script_{i%10}.py --synthetic":i%3==1?"node.exe --type=renderer":"example.exe",DateTimeOffset.UtcNow)
            }).ToArray();
            lastSnapshot=lastSnapshot with { Processes=processes };
            entities=new IdentityResolver().Resolve(lastSnapshot,[]);
            RefreshRows(); await Task.Delay(400);
            for(var warm=0;warm<3;warm++) RefreshRows();
            await Measure("projection-"+count,_=>ProcessListProjection.Build(lastSnapshot,entities,analysis.Values.ToList(),
                new(PreviousOrder:rows.Select(r=>r.Key).ToArray())),12,false);
            await Measure("row-values-"+count,_=> { foreach(var row in rows) row.Update(row.Projection); },12,false);
            await Measure("refresh-"+count,_=>RefreshRows(),12);
            if(count==1000) {
                await Measure("sort-1000",i=> { sort=i%2==0?ProcessSort.Memory:ProcessSort.Name; RefreshRows(true); },8);
                sort=ProcessSort.Name;
                await Measure("search-1000",i=> { search=i%2==0?"python":""; RefreshRows(true); },8);
                search=""; RefreshRows(true);
                detailsOpen=true; UpdateDetailsLayout();
                await Measure("select-details-1000",i=>table!.SelectedItem=rows[i%rows.Count],12);
                detailsOpen=false; UpdateDetailsLayout();
                await Measure("resize-1000",i=>ResizeColumn(0,280+i%20,false),40);
                ResizeColumn(0,280,false);
                await Measure("group-toggle-1000",i=> { presentation=presentation with { Grouped=i%2==0 }; RefreshRows(true); },8);
                presentation=presentation with { Grouped=false }; RefreshRows(true);
            }
            var header=tableHeader!.Children.OfType<Border>().Single(b=>b.Tag as string=="Pid");
            draggedColumnId="Pid";
            PositionDraggedHeader(header,"Pid",450,20); WindowRoot.UpdateLayout();
            await Measure("drag-position-"+count,i=>PositionDraggedHeader(header,"Pid",450+i%40,20),100);
            var original=tableColumnLayout.ToArray();
            await Measure("drag-crossing-"+count,i=> {
                var x=i%2==0?390:490;
                PreviewDraggedColumns(original,"Pid",x);
                PositionDraggedHeader(header,"Pid",x,20);
            },40);
            RestoreDraggedColumn(); ApplyColumns(original,false);
            if(count==1000) {
                ApplyColumns(TableColumns.Available(false).Select(c=>new TableColumnPreference(c.Id,c.Width)),false);
                await Task.Delay(250);
                await Measure("refresh-all-columns-1000",_=>RefreshRows(),12);
            }
        }
        var ruleFile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"procsight-ui-perf-"+Guid.NewGuid()+".db");
        try {
            knowledge=new(ruleFile);
            page="Knowledge"; RenderPage();
            ListView? list=null;
            for(var attempt=0;attempt<50;attempt++) {
                await Task.Delay(50);
                list=FixtureControls<ListView>(PageContent).FirstOrDefault();
                if(list?.Items.Count>300) break;
            }
            if(list is null||list.Items.Count<300) throw new InvalidOperationException("Synthetic bundled rules library did not load.");
            await Measure("rules-select-bundled",i=>list.SelectedIndex=i%list.Items.Count,20);
            var box=FixtureControls<TextBox>(PageContent).First();
            await Measure("rules-search-bundled",i=>box.Text=i%2==0?"Microsoft":"",20);
        } finally {
            page="Processes"; RenderPage();
            foreach(var suffix in new[]{"","-wal","-shm"}) if(File.Exists(ruleFile+suffix)) File.Delete(ruleFile+suffix);
        }
        File.WriteAllText(output+".checks","Synthetic UI performance measurements completed; elapsed times include explicit layout where labeled; no pointer injection or real collection.");
    }
}
#endif
