using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private async Task InspectProcessRulesAsync(ResolvedEntity entity)
    {
        try {
            var candidates=await Task.Run(()=>knowledge.ReadCandidatesAsync(entity,lifetime.Token),lifetime.Token);
            var current=analysis.GetValueOrDefault(entity.Identity)?.Result;
            var panel=new StackPanel { Spacing=12,MaxWidth=620 };
            panel.Children.Add(new TextBlock { Text="Matched rules may not contribute when evidence conflicts or a correction takes precedence. Inspect a rule to see its conditions, evidence and origin.",TextWrapping=TextWrapping.Wrap });
            RuleDefinition? selectedRule=null; KnownTaskDefinition? selectedBundled=null;
            var dialog=new ContentDialog { Title="Rules for selected process or service",Content=new ScrollViewer { Content=panel,MaxHeight=560 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot };
            foreach(var rule in candidates.Rules.Where(r=>current?.MatchingRuleIds.Contains(r.Id)==true)) {
                var button=new Button { Content=new TextBlock { Text=RuleInspection.ApplicationStatus(rule.Id,current)+" · "+RuleInspection.Title(rule,candidates.Evidence)+"\n"+RuleInspection.Source(rule),TextWrapping=TextWrapping.Wrap },HorizontalAlignment=HorizontalAlignment.Stretch };
                button.Click+=(_,_)=> { selectedRule=rule; dialog.Hide(); }; panel.Children.Add(button);
            }
            if(KnownTaskCatalog.Match(entity) is { } bundled) {
                var button=new Button { Content=RuleInspection.ApplicationStatus(bundled.Id,current)+" · Bundled: "+bundled.Label };
                button.Click+=(_,_)=> { selectedBundled=bundled; dialog.Hide(); }; panel.Children.Add(button);
            }
            if(panel.Children.Count==1) panel.Children.Add(new TextBlock { Text="No matching reusable rules are recorded for the current assessment.",TextWrapping=TextWrapping.Wrap });
            await dialog.ShowAsync();
            if(selectedRule is not null) await InspectRuleAsync(selectedRule,entity,current);
            else if(selectedBundled is not null) await InspectBundledRuleAsync(selectedBundled,entity,current);
        } catch(OperationCanceledException) { }
        catch { await MessageAsync("Rule inspection unavailable","The rules or their supporting evidence could not be read."); }
    }
    private async Task InspectRuleAsync(RuleDefinition rule,ResolvedEntity? entity=null,LocalAnalysis? current=null)
    {
        try {
            var records=await Task.Run(()=>knowledge.GetEvidenceAsync(rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds)).Concat(rule.Assessments.SelectMany(a=>a.EvidenceIds)),lifetime.Token),lifetime.Token);
            var changed=false;
            var panel=CreateLearnedRulePanel(rule,records.Concat(entity?.Evidence??[]).DistinctBy(e=>e.Id).ToArray(),()=> { changed=true; return Task.CompletedTask; },entity,current);
            panel.MaxWidth=620;
            await new ContentDialog { Title="Learned rule",Content=new ScrollViewer { Content=panel,MaxHeight=560 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot }.ShowAsync();
            if(changed&&page=="Knowledge") RenderPage();
        } catch(OperationCanceledException) { }
        catch { await MessageAsync("Rule inspection unavailable","The rule or its supporting evidence could not be read."); }
    }
    private async Task InspectBundledRuleAsync(KnownTaskDefinition definition,ResolvedEntity? entity=null,LocalAnalysis? current=null)
    {
        var panel=CreateBundledRulePanel(definition,entity,current); panel.MaxWidth=620;
        await new ContentDialog { Title="Bundled rule",Content=new ScrollViewer { Content=panel,MaxHeight=560 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot }.ShowAsync();
    }
}
