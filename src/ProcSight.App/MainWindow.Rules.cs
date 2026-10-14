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
                var button=new Button { Content=new TextBlock { Text=RuleInspection.ApplicationStatus(rule.Id,current)+" · "+RuleInspection.Title(rule)+"\n"+RuleInspection.Source(rule),TextWrapping=TextWrapping.Wrap },HorizontalAlignment=HorizontalAlignment.Stretch };
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
            var records=await Task.Run(()=>knowledge.GetEvidenceAsync(rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds))
                .Concat(rule.Assessments.SelectMany(a=>a.EvidenceIds)),lifetime.Token),lifetime.Token);
            var evidence=records.Concat(entity?.Evidence??[]).DistinctBy(e=>e.Id).ToArray();
            var panel=new StackPanel { Spacing=12,MaxWidth=620 };
            var description=new TextBlock { Text=RuleInspection.Describe(rule,evidence,DateTimeOffset.UtcNow,entity,current),TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
            panel.Children.Add(description);
            foreach(var record in evidence) panel.Children.Add(new Expander { Header=$"Evidence · {record.Kind} · {record.Origin}",
                Content=new TextBlock { Text=RuleInspection.Describe(record),TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true },HorizontalAlignment=HorizontalAlignment.Stretch });
            var feedback=new TextBlock { TextWrapping=TextWrapping.Wrap };
            var toggle=new Button { Content=rule.State==RuleState.Active?"Disable rule":"Enable rule",IsEnabled=rule.State!=RuleState.Rejected&&
                (rule.State==RuleState.Active||rule.ExpiresAt>DateTimeOffset.UtcNow&&rule.ReviewAt>DateTimeOffset.UtcNow&&new RuleValidator().Validate(rule,evidence,DateTimeOffset.UtcNow).Accepted) };
            var delete=new Button { Content="Delete rule" }; var changed=false;
            toggle.Click+=async(_,_)=> {
                toggle.IsEnabled=false; delete.IsEnabled=false;
                try { await Task.Run(()=>knowledge.SetRuleStateAsync(rule.Id,rule.State==RuleState.Active?RuleState.Disabled:RuleState.Active,lifetime.Token),lifetime.Token);
                    coordinator.InvalidateKnowledge(); changed=true; feedback.Text="Rule state saved. Close this view to refresh the library; process classifications refresh on collection."; }
                catch { feedback.Text="Rule could not change state. Expired or unsupported rules require fresh analysis."; }
            };
            delete.Click+=async(_,_)=> {
                toggle.IsEnabled=false; delete.IsEnabled=false;
                try { await Task.Run(()=>knowledge.DeleteRuleAsync(rule.Id,lifetime.Token),lifetime.Token); coordinator.InvalidateKnowledge(); changed=true;
                    feedback.Text="Rule deleted. Cached findings depending on it will be reconsidered."; }
                catch { feedback.Text="Rule could not be deleted. Knowledge storage may be unavailable."; }
            };
            panel.Children.Add(toggle); panel.Children.Add(delete); panel.Children.Add(feedback);
            await new ContentDialog { Title="Learned rule",Content=new ScrollViewer { Content=panel,MaxHeight=560 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot }.ShowAsync();
            if(changed&&page=="Knowledge") RenderPage();
        } catch(OperationCanceledException) { }
        catch { await MessageAsync("Rule inspection unavailable","The rule or its supporting evidence could not be read."); }
    }
    private async Task InspectBundledRuleAsync(KnownTaskDefinition definition,ResolvedEntity? entity=null,LocalAnalysis? current=null)
    {
        var panel=new StackPanel { Spacing=12,MaxWidth=620 };
        panel.Children.Add(new TextBlock { Text=RuleInspection.Describe(definition)+(entity is null?"":"\n\nFor selected process/service: "+RuleInspection.ApplicationStatus(definition.Id,current)),
            TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true });
        panel.Children.Add(new HyperlinkButton { Content="Open public reference",NavigateUri=new(definition.SourceUrl) });
        await new ContentDialog { Title="Bundled rule",Content=new ScrollViewer { Content=panel,MaxHeight=560 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot }.ShowAsync();
    }
}
