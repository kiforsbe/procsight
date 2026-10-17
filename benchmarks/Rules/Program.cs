using System.Diagnostics;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Infrastructure.Knowledge;
var now=DateTimeOffset.UtcNow;
var count=200;
var evidence=Enumerable.Range(0,count).Select(i=>new EvidenceRecord("ev"+i,DataOrigin.ShareableMetadata,now,now.AddDays(30),"LocalMetadata",new(){["hash"]="hash"+i})).ToList();
var rules=evidence.Select((e,i)=>new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(30),[new(PredicateKind.Exact,"hash","hash"+i,[e.Id])],[new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture",Confidence.High,[e.Id],now.AddDays(30),[DataOrigin.ShareableMetadata])],[e.Id],100)).ToList();
var resource=new ResourceSample(Observation<double>.Pending(),Observation<long>.Available(1024),Observation<double>.Pending(),Observation<double>.Pending(),now,null);
ResolvedEntity Entity(int i) {
 var d=KnownTaskCatalog.Definitions[i%15];
 var exe=new ExecutableMetadata(d.Executable,@"C:\Windows\System32\"+d.Executable,Product:d.Product,Signature:SignatureStatus.Verified,VerifiedSigner:d.Signers[0],OriginalName:d.OriginalName ?? d.Executable);
 var process=new ProcessSnapshot(new(i+1,now,null),null,exe,resource,[]);
 return new(new("identity"+i),"Synthetic",null,process,null,new(){["hash"]="hash"+(i%count)},[],[DataOrigin.ShareableMetadata]);
}
var entities=Enumerable.Range(0,1000).Select(Entity).ToArray();
var entry=KnowledgeEntry.Empty(new("seed")) with {Rules=rules,Evidence=evidence,UpdatedAt=now};
var path=Path.Combine(Path.GetTempPath(),"procsight-rules-perf-"+Guid.NewGuid()+".db");
var store=new SqliteKnowledgeStore(path); await store.SaveAsync(entry,default);
var engine=new LocalAnalysisEngine();
void Measure(string name,int n,Action<int> body){ for(var i=0;i<20;i++)body(i); GC.Collect();var alloc=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(var i=0;i<n;i++)body(i);sw.Stop();Console.WriteLine($"{name} n={n}: {sw.Elapsed.TotalMilliseconds:F1} ms, {(GC.GetAllocatedBytesForCurrentThread()-alloc)/1048576d:F2} MiB");}
Console.WriteLine($"Catalog={KnownTaskCatalog.Definitions.Count}, learned={count}");
foreach(var n in new[]{500,1000}) {Measure("engine",n,i=>KnownTaskCatalog.Supplement(entities[i],engine.Evaluate(entities[i],entry,now),entry,now));Measure("catalog",n,i=>KnownTaskCatalog.Evaluate(entities[i],now));Measure("read",n,i=>store.ReadCandidatesAsync(entities[i],default).GetAwaiter().GetResult());Measure("read+engine",n,i=>{var e=entities[i];var k=store.ReadCandidatesAsync(e,default).GetAwaiter().GetResult();KnownTaskCatalog.Supplement(e,engine.Evaluate(e,k,now),k,now);});}
Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);
