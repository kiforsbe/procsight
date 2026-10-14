using ProcSight.Application.Knowledge;
using ProcSight.Application.Budget;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace ProcSight.Infrastructure.Knowledge;
public sealed class SqliteKnowledgeStore(string path) : IKnowledgeStore,IUsageLedger
{
    private static readonly HashSet<string> EvidenceFields=["name","path","hash","product","version","package","signer","owner","ownerIdentity","service","commandRole","commandContext","claim","threatHash"];
    private SqliteConnection Open(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,DefaultTimeout=10 }.ToString());
        try
        {
            connection.Open();
            using var versionCommand=connection.CreateCommand(); versionCommand.CommandText="PRAGMA user_version";
            var version=Convert.ToInt32(versionCommand.ExecuteScalar());
            if(version>1) throw new InvalidDataException("Knowledge schema is newer than this application; original preserved.");
            if(version==0)
            {
            using var migration=connection.BeginTransaction();
            Execute(connection,migration,"""
            CREATE TABLE IF NOT EXISTS entries(identity TEXT PRIMARY KEY,payload TEXT NOT NULL,updated TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS rules(id TEXT PRIMARY KEY,state INTEGER NOT NULL,payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS evidence(id TEXT PRIMARY KEY,payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS corrections(id TEXT PRIMARY KEY,payload TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS history(id INTEGER PRIMARY KEY,identity TEXT NOT NULL,payload TEXT NOT NULL,updated TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS usage(day TEXT NOT NULL,kind INTEGER NOT NULL,count INTEGER NOT NULL,PRIMARY KEY(day,kind));
            CREATE INDEX IF NOT EXISTS rules_state ON rules(state);
            CREATE INDEX IF NOT EXISTS history_updated ON history(updated);
            PRAGMA user_version=1;
            """);
            ct.ThrowIfCancellationRequested(); migration.Commit();
            }
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
    private static void Execute(SqliteConnection connection,SqliteTransaction? transaction,string sql,params (string Name,object? Value)[] args)
    {
        using var command=connection.CreateCommand(); command.Transaction=transaction; command.CommandText=sql;
        foreach (var arg in args) command.Parameters.AddWithValue(arg.Name,arg.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    private static List<T> Read<T>(SqliteConnection connection,string sql,params (string Name,object? Value)[] args)
    {
        using var command=connection.CreateCommand(); command.CommandText=sql;
        foreach (var arg in args) command.Parameters.AddWithValue(arg.Name,arg.Value ?? DBNull.Value);
        using var reader=command.ExecuteReader(); var values=new List<T>();
        while (reader.Read()) values.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new InvalidDataException("Invalid knowledge record"));
        return values;
    }
    public Task<KnowledgeEntry> LoadAsync(AnalysisIdentity id,CancellationToken ct)
    {
        using var db=Open(ct);
        var entry=Read<KnowledgeEntry>(db,"SELECT payload FROM entries WHERE identity=$id",("$id",id.Value)).FirstOrDefault() ?? KnowledgeEntry.Empty(id);
        return Task.FromResult(entry with { Corrections=Read<UserCorrection>(db,"SELECT payload FROM corrections")
            .Where(c=>c.Scope==CorrectionScope.InvocationContext && c.ScopeValue==id.Value).ToList() });
    }
    public async Task<KnowledgeEntry> ReadCandidatesAsync(ResolvedEntity entity,CancellationToken ct)
    {
        var entry=await LoadAsync(entity.Identity,ct); using var db=Open(ct);
        return entry with { Rules=Read<RuleDefinition>(db,"SELECT payload FROM rules WHERE state=$state",("$state",(int)RuleState.Active)),
            Evidence=Read<EvidenceRecord>(db,"SELECT payload FROM evidence"),
            Corrections=Read<UserCorrection>(db,"SELECT payload FROM corrections").Where(c=>c.Scope switch {
                CorrectionScope.InvocationContext=>c.ScopeValue==entity.Identity.Value,
                CorrectionScope.Executable=>c.ScopeValue==entity.Facts.GetValueOrDefault("hash"),
                _=>c.ScopeValue==entity.Owner }).ToList() };
    }
    public Task SaveAsync(KnowledgeEntry entry,CancellationToken ct)
    {
        if (entry.Evidence.Any(e=>e.Facts.Keys.Any(k=>!EvidenceFields.Contains(k))))
            throw new InvalidDataException("Only normalized evidence fields may be stored.");
        if(entry.Evidence.Any(e=>e.Origin is DataOrigin.LocalCommandLine or DataOrigin.OnlineCommandLine &&
            e.Facts.Any(f=>!(f.Key=="commandRole" && f.Value is "Renderer" or "Utility" or "Worker" or "CLI") &&
                !(f.Key=="commandContext" && f.Value.Length==64 && f.Value.All(Uri.IsHexDigit)))))
            throw new InvalidDataException("Private command evidence must use controlled role values.");
        var safe=entry with { Evidence=entry.Evidence.Select(e=>e with { Facts=e.Facts.ToDictionary(f=>f.Key,f=>MetadataSanitizer.Normalize(f.Value)) }).ToList(), Corrections=[] };
        using var db=Open(ct); using var transaction=db.BeginTransaction();
        Execute(db,transaction,"INSERT INTO history(identity,payload,updated) SELECT identity,payload,updated FROM entries WHERE identity=$id",("$id",entry.Identity.Value));
        Execute(db,transaction,"INSERT OR REPLACE INTO entries VALUES($id,$payload,$updated)",("$id",entry.Identity.Value),
            ("$payload",JsonSerializer.Serialize(safe)),("$updated",entry.UpdatedAt.ToString("O")));
        foreach (var rule in safe.Rules) Execute(db,transaction,"INSERT INTO rules VALUES($id,$state,$payload) ON CONFLICT(id) DO UPDATE SET state=$state,payload=$payload WHERE json_extract(rules.payload,'$.Version') < $version",
            ("$id",rule.Id.ToString()),("$state",(int)rule.State),("$payload",JsonSerializer.Serialize(rule)),("$version",rule.Version));
        foreach (var evidence in safe.Evidence) Execute(db,transaction,"INSERT OR REPLACE INTO evidence VALUES($id,$payload)",
            ("$id",evidence.Id),("$payload",JsonSerializer.Serialize(evidence)));
        foreach (var correction in entry.Corrections) Execute(db,transaction,"INSERT OR REPLACE INTO corrections VALUES($id,$payload)",
            ("$id",correction.Id.ToString()),("$payload",JsonSerializer.Serialize(correction)));
        transaction.Commit(); return Task.CompletedTask;
    }
    public Task SetRuleStateAsync(Guid id,RuleState state,CancellationToken ct)
    {
        using var db=Open(ct); using var transaction=db.BeginTransaction();
        var rule=Read<RuleDefinition>(db,"SELECT payload FROM rules WHERE id=$id",("$id",id.ToString())).FirstOrDefault();
        if(rule is not null && state==RuleState.Active && (rule.ReviewAt<=DateTimeOffset.UtcNow || rule.ExpiresAt<=DateTimeOffset.UtcNow))
            throw new InvalidDataException("Expired rules require fresh analysis before activation.");
        if (rule is not null) Execute(db,transaction,"UPDATE rules SET state=$state,payload=$payload WHERE id=$id",
            ("$state",(int)state),("$payload",JsonSerializer.Serialize(rule with { State=state })),("$id",id.ToString()));
        transaction.Commit(); return Task.CompletedTask;
    }
    public Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct)
    { using var db=Open(ct); return Task.FromResult<IReadOnlyList<RuleDefinition>>(Read<RuleDefinition>(db,"SELECT payload FROM rules")); }
    public Task<IReadOnlyList<EvidenceRecord>> GetEvidenceAsync(IEnumerable<string> ids,CancellationToken ct)
    {
        var wanted=ids.ToHashSet(); using var db=Open(ct);
        return Task.FromResult<IReadOnlyList<EvidenceRecord>>(Read<EvidenceRecord>(db,"SELECT payload FROM evidence").Where(e=>wanted.Contains(e.Id)).ToList());
    }
    public Task DeleteRuleAsync(Guid id,CancellationToken ct)
    { using var db=Open(ct); Execute(db,null,"DELETE FROM rules WHERE id=$id",("$id",id.ToString())); return Task.CompletedTask; }
    public Task ClearAsync(ClearScope scope,CancellationToken ct)
    {
        using var db=Open(ct); using var transaction=db.BeginTransaction();
        Execute(db,transaction,scope==ClearScope.UserCorrections ? "DELETE FROM corrections" :
            "DELETE FROM entries; DELETE FROM history; DELETE FROM rules; DELETE FROM evidence;");
        transaction.Commit(); return Task.CompletedTask;
    }
    public Task PruneAsync(DateTimeOffset now,CancellationToken ct)
    {
        using var db=Open(ct); using var transaction=db.BeginTransaction();
        var before=now.AddDays(-90);
        Execute(db,transaction,"DELETE FROM history WHERE updated < $before",("$before",before.ToString("O")));
        var rules=Read<RuleDefinition>(db,"SELECT payload FROM rules");
        var corrections=Read<UserCorrection>(db,"SELECT payload FROM corrections");
        var entries=Read<KnowledgeEntry>(db,"SELECT payload FROM entries");
        foreach(var entry in entries.Where(e=>e.UpdatedAt<before && !corrections.Any(c=>c.ScopeValue==e.Identity.Value ||
            e.Evidence.Any(record=>record.Facts.Values.Contains(c.ScopeValue)))))
            Execute(db,transaction,"DELETE FROM entries WHERE identity=$id",("$id",entry.Identity.Value));
        var activeRules=rules.Where(r=>r.State==RuleState.Active&&r.ReviewAt>now&&r.ExpiresAt>now).ToList();
        foreach(var rule in rules.Where(r=>r.CreatedAt<before&&!activeRules.Contains(r)))
            Execute(db,transaction,"DELETE FROM rules WHERE id=$id",("$id",rule.Id.ToString()));
        var retainedEntries=Read<KnowledgeEntry>(db,"SELECT payload FROM entries");
        var referenced=activeRules.SelectMany(r=>r.EvidenceIds.Concat(r.Predicates.SelectMany(p=>p.EvidenceIds)))
            .Concat(retainedEntries.SelectMany(e=>e.Evidence.Select(record=>record.Id)))
            .Concat(corrections.SelectMany(c=>c.Assessment.EvidenceIds)).ToHashSet();
        foreach(var record in Read<EvidenceRecord>(db,"SELECT payload FROM evidence").Where(e=>e.ObservedAt<before&&!referenced.Contains(e.Id)))
            Execute(db,transaction,"DELETE FROM evidence WHERE id=$id",("$id",record.Id));
        transaction.Commit(); return Task.CompletedTask;
    }
    public Task<bool> TryReserveAsync(UsageKind kind,DateOnly localDate,UsageLimits limits,CancellationToken ct)
    {
        using var db=Open(ct); using var transaction=db.BeginTransaction();
        using var command=db.CreateCommand(); command.Transaction=transaction;
        command.CommandText="SELECT count FROM usage WHERE day=$day AND kind=$kind";
        command.Parameters.AddWithValue("$day",localDate.ToString("O")); command.Parameters.AddWithValue("$kind",(int)kind);
        var count=Convert.ToInt32(command.ExecuteScalar() ?? 0);
        var cap=kind==UsageKind.AutomaticJob ? limits.AutomaticJobs : limits.BillableCalls;
        if (count>=Math.Max(0,cap)) return Task.FromResult(false);
        Execute(db,transaction,"INSERT OR REPLACE INTO usage VALUES($day,$kind,$count)",("$day",localDate.ToString("O")),("$kind",(int)kind),("$count",count+1));
        transaction.Commit(); return Task.FromResult(true);
    }
    public Task<(int Jobs,int Calls)> GetUsageAsync(DateOnly localDate,CancellationToken ct)
    {
        using var db=Open(ct); using var command=db.CreateCommand();
        command.CommandText="SELECT kind,count FROM usage WHERE day=$day"; command.Parameters.AddWithValue("$day",localDate.ToString("O"));
        using var reader=command.ExecuteReader(); var jobs=0; var calls=0;
        while(reader.Read()) { if (reader.GetInt32(0)==0) jobs=reader.GetInt32(1); else calls=reader.GetInt32(1); }
        return Task.FromResult((jobs,calls));
    }
}
