using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.App.ViewModels;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private void ShowDetail(ProcessRow? row)
    {
        if(!detailsOpen||detailPanel is null) return;
        var entity=row?.Entity;
        var sections=entity is null?[]:ProcessDetails.Sections(entity);
        var update=entity is null?null:analysis.GetValueOrDefault(entity.Identity);
        var selection=row is null?"none":row.IsGroup?"group|"+row.Key:entity!.Process is { } lifetimeProcess?"process|"+lifetimeProcess.Key:"service|"+entity.Service?.Name;
        var localRole=entity is null?null:ProcessRoleLabels.Describe(entity);
        // Transient local UI comparison only. Never persist or log this value.
        var fingerprint=selection+"|"+entity?.Identity.Value+"|"+row?.Name+"|"+entity?.Owner+"|"+localRole?.Explanation+"|"+
            string.Join("|",sections.SelectMany(s=>s.Fields.Select(f=>f.Label+":"+f.Value)))+"|"+update?.State+"|"+update?.Error+"|"+
            update?.Result.IdentityLabel+"|"+update?.Result.Role+"|"+string.Join("|",update?.Result.Assessments.Select(a=>$"{a.Axis}:{a.Value}:{a.Reason}:{a.Confidence}:{a.ExpiresAt}:{string.Join(',',a.Origins)}")??[])+"|"+
            string.Join(',',update?.Result.MatchingRuleIds??[])+"|"+string.Join(',',update?.Result.AppliedRuleIds??[])+"|"+string.Join('|',update?.Result.Issues??[]);
        if(detailFingerprint==fingerprint) return;
        var sameSelection=selection==detailSelectionKey;
        var expanded=sameSelection?detailPanel.Children.OfType<Expander>().Where(e=>e.Tag is string).ToDictionary(e=>(string)e.Tag,e=>e.IsExpanded):[];
        var offset=sameSelection?detailScroll?.VerticalOffset??0:0;
        var previousEvidence=sameSelection&&detailEvidenceScope==entity?.Identity.Value?evidencePanel:null;
        var evidenceExpanded=previousEvidence?.Children.OfType<Expander>().Where(e=>e.Tag is string).ToDictionary(e=>(string)e.Tag,e=>e.IsExpanded)??[];
        if(previousEvidence is not null&&detailPanel.Children.OfType<Expander>().FirstOrDefault(e=>e.Tag as string=="evidence")?.Content is Border oldEvidenceWrapper)
            oldEvidenceWrapper.Child=null;
        detailSelectionKey=selection; detailFingerprint=fingerprint; var version=++detailVersion;
        detailEvidenceScope=entity?.Identity.Value;
        detailPanel.Children.Clear(); evidencePanel=null;
        var toolbar=new CommandBar { Background=null,DefaultLabelPosition=CommandBarDefaultLabelPosition.Right };
        var back=new AppBarButton { Label="Back to list",Icon=new SymbolIcon(Symbol.Back) };
        back.Click+=(_,_)=> { detailsOpen=false; UpdateDetailsLayout(); table?.Focus(FocusState.Programmatic); };
        toolbar.PrimaryCommands.Add(back);
        var inspect=new AppBarButton { Label="Rules",Icon=new SymbolIcon(Symbol.Library),IsEnabled=row is { IsGroup:false } };
        inspect.Click+=async(_,_)=> { if(entity is not null) await InspectProcessRulesAsync(entity); }; toolbar.PrimaryCommands.Add(inspect);
        var correct=new AppBarButton { Label="Correct classification",Icon=new SymbolIcon(Symbol.Edit),IsEnabled=row is { IsGroup:false } };
        correct.Click+=async(_,_)=>await CorrectSelectedAsync(); toolbar.SecondaryCommands.Add(correct);
        var preview=new AppBarButton { Label="Preview analysis payload",Icon=new SymbolIcon(Symbol.Document),IsEnabled=row is { IsGroup:false } };
        preview.Click+=async(_,_)=>await PreviewAsync(); toolbar.SecondaryCommands.Add(preview);
        detailPanel.Children.Add(toolbar);
        if(row is null) { detailPanel.Children.Add(RuleText("Select a process or service","SubtitleTextBlockStyle")); return; }
        var heading=new Grid { ColumnSpacing=12 };
        heading.ColumnDefinitions.Add(new() { Width=GridLength.Auto }); heading.ColumnDefinitions.Add(new());
        if(row.Icon is not null) heading.Children.Add(new Image { Source=row.Icon,Width=32,Height=32,VerticalAlignment=VerticalAlignment.Top });
        else heading.Children.Add(new FontIcon { FontFamily=new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),Glyph="\uE950",FontSize=24,Width=32,VerticalAlignment=VerticalAlignment.Top });
        var title=RuleText(row.Name,"SubtitleTextBlockStyle"); Grid.SetColumn(title,1); heading.Children.Add(title); detailPanel.Children.Add(heading);
        if(row.IsGroup) {
            detailPanel.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Informational,Title="Application group",Message="Select an individual process to inspect its metadata, assessments and applied rules." });
            return;
        }
        detailPanel.Children.Add(RuleText("Ownership: "+(entity!.Owner??"Unknown"),"CaptionTextBlockStyle",true));
        detailPanel.Children.Add(RuleText(entity.Process is { } process?$"PID {process.Key.Pid} · {process.Executable.Name}":$"Service {entity.Service!.Name} · {entity.Service.State}","CaptionTextBlockStyle",true));
        if(localRole?.UsesCommandContext==true) {
            var role=RuleSection("Local invocation role",RuleFields(("Role",localRole.Label),("Explanation",localRole.Explanation))); AddSection(role,"local-role");
        }
        detailPanel.Children.Add(RuleText("Assessments","BodyStrongTextBlockStyle"));
        detailPanel.Children.Add(RuleText("Analysis: "+(update?.State.ToString()??"Unknown"),"CaptionTextBlockStyle",true));
        foreach(var axis in Enum.GetValues<AssessmentAxis>()) {
            var assessment=update?.Result.Assessments.FirstOrDefault(a=>a.Axis==axis);
            var content=assessment is null?RuleFields(("Status","Unknown — insufficient current evidence.")):
                RuleFields(("Classification",RuleInspection.Field(assessment.Value)),("Confidence",assessment.Confidence.ToString()),("Explanation",assessment.Reason),
                    ("Review by",assessment.ExpiresAt.LocalDateTime.ToString("g")),("Privacy origins",string.Join(", ",assessment.Origins)));
            AddSection(RuleSection(axis+" · "+(assessment is null?"Unknown":RuleInspection.Field(assessment.Value)),content),"assessment-"+axis);
        }
        if(update is not null&&(update.Error is not null||update.Result.Issues.Count>0))
            detailPanel.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Warning,Title="Analysis needs attention",Message=string.Join("\n",update.Result.Issues.Concat(update.Error is null?[]:new[]{update.Error})) });
        if(update?.Result.IdentityLabel is not null||update?.Result.Role is not null) {
            AddSection(RuleSection("Analysis identity context",RuleFields(("Suggested identity",update!.Result.IdentityLabel??"Unknown"),("Suggested role",update.Result.Role??"Unknown"))),"analysis-identity");
        }
        detailPanel.Children.Add(RuleText("Local metadata","BodyStrongTextBlockStyle"));
        foreach(var section in sections) {
            var content=RuleFields(section.Fields.Select(f=>(f.Label,f.Value)).ToArray());
            if(section.Key=="command") content.Children.Add(RuleText("Available locally when Windows permits collection. Online sharing follows your separate privacy settings.","CaptionTextBlockStyle",true));
            AddSection(RuleSection(section.Title,content),section.Key);
        }
        evidencePanel=previousEvidence??new StackPanel { Spacing=12 };
        if(previousEvidence is null) evidencePanel.Children.Add(RuleText("Loading supporting evidence…","CaptionTextBlockStyle",true));
        AddSection(RuleSection("Rules & supporting evidence",evidencePanel),"evidence");
        _=ShowEvidenceAsync(entity,evidencePanel,version,evidenceExpanded,offset);
        DispatcherQueue.TryEnqueue(()=> { if(detailVersion==version) detailScroll?.ChangeView(null,offset,null,true); });
        void AddSection(Expander expander,string key) {
            expander.Tag=key;
            if(expanded.TryGetValue(key,out var open)) expander.IsExpanded=open;
            detailPanel.Children.Add(expander);
        }
    }
    private async Task ShowEvidenceAsync(ResolvedEntity entity,StackPanel target,int version,IReadOnlyDictionary<string,bool> expanded,double offset)
    {
        if(knowledge is null) { target.Children.Clear(); target.Children.Add(RuleText("No stored knowledge is available.")); return; }
        bool Current()=>version==detailVersion&&ReferenceEquals(target,evidencePanel)&&
            (table?.SelectedItem as ProcessRow)?.Entity is { } selected&&selected.Identity==entity.Identity&&selected.Process?.Key==entity.Process?.Key&&selected.Service?.Name==entity.Service?.Name;
        try {
            var cached=await Task.Run(()=>knowledge.ReadCandidatesAsync(entity,lifetime.Token),lifetime.Token);
            if(!Current()) return;
            var currentExpanded=expanded.ToDictionary(p=>p.Key,p=>p.Value);
            var currentOffset=detailScroll?.VerticalOffset??offset;
            foreach(var section in target.Children.OfType<Expander>().Where(e=>e.Tag is string)) currentExpanded[(string)section.Tag]=section.IsExpanded;
            target.Children.Clear();
            var current=analysis.GetValueOrDefault(entity.Identity)?.Result;
            target.Children.Add(RuleText(current is null?"No analysis recorded yet.":$"Applied rules: {(current.AppliedRuleIds is { } applied?applied.Count.ToString():"Not recorded")} · Matches: {current.MatchingRuleIds.Count}","CaptionTextBlockStyle",true));
            foreach(var id in current?.MatchingRuleIds??[]) {
                if(cached.Rules.FirstOrDefault(r=>r.Id==id) is not { } rule) continue;
                var button=new Button { Content=new TextBlock { Text=RuleInspection.ApplicationStatus(id,current)+" · "+RuleInspection.Title(rule,cached.Evidence),TextWrapping=TextWrapping.Wrap },HorizontalAlignment=HorizontalAlignment.Stretch };
                button.Click+=async(_,_)=>await InspectRuleAsync(rule,entity,current); target.Children.Add(button);
                target.Children.Add(RuleText(RuleInspection.Source(rule),"CaptionTextBlockStyle",true));
            }
            if(KnownTaskCatalog.Match(entity) is { } common) {
                var button=new Button { Content=RuleInspection.ApplicationStatus(common.Id,current)+" · "+common.Label };
                button.Click+=async(_,_)=>await InspectBundledRuleAsync(common,entity,current); target.Children.Add(button);
            }
            if(analysis.GetValueOrDefault(entity.Identity)?.State==AnalysisState.Stale) {
                var historical=cached.Assessments.Where(a=>a.ExpiresAt<=DateTimeOffset.UtcNow).Select(a=>$"{a.Axis}: {a.Value} · expired {a.ExpiresAt.LocalDateTime:g}").ToArray();
                if(historical.Length>0) AddEvidenceSection("history",RuleSection("Historical findings — expired",RuleText(string.Join("\n",historical))));
            }
            foreach(var correction in cached.Corrections.Where(c=>c.Assessment.ExpiresAt>DateTimeOffset.UtcNow))
                AddEvidenceSection("correction-"+correction.Id,RuleSection("User correction · "+correction.Assessment.Axis,RuleFields(("Scope",correction.Scope.ToString()),("Classification",RuleInspection.Field(correction.Assessment.Value)),("Explanation",correction.Assessment.Reason))));
            var evidence=entity.Evidence.Concat(cached.Evidence).DistinctBy(e=>e.Id).ToArray();
            foreach(var record in evidence.Take(20)) {
                var content=new StackPanel { Spacing=12 }; content.Children.Add(RuleText(RuleInspection.Describe(record)));
                if(Uri.TryCreate(record.SourceUrl,UriKind.Absolute,out var uri)&&uri.Scheme=="https") content.Children.Add(new HyperlinkButton { Content="Open evidence source",NavigateUri=uri });
                AddEvidenceSection("record-"+record.Id,RuleSection(record.Kind+" · "+record.Origin,content));
            }
            if(evidence.Length>20) target.Children.Add(RuleText($"Showing 20 of {evidence.Length} evidence records. Inspect a rule for its full referenced evidence.","CaptionTextBlockStyle",true));
            DispatcherQueue.TryEnqueue(()=> { if(Current()) detailScroll?.ChangeView(null,currentOffset,null,true); });
            void AddEvidenceSection(string key,Expander section) {
                section.Tag=key; if(currentExpanded.TryGetValue(key,out var open)) section.IsExpanded=open; target.Children.Add(section);
            }
        } catch(OperationCanceledException) { }
        catch {
            if(!Current()) return; target.Children.Clear();
            target.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Warning,Title="Evidence unavailable",Message="Stored knowledge could not be read. Local metadata remains available." });
        }
    }
}
