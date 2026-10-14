using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private void RenderKnowledge()
    {
        if(knowledge is null) return;
        var panel=new StackPanel { Spacing=12,Margin=new Thickness(24) };
        panel.Children.Add(new TextBlock { Text="Knowledge",FontSize=24 });
        panel.Children.Add(new TextBlock { Text=$"{ProcSight.Core.Rules.KnownTaskCatalog.Definitions.Count} bundled common-task rules · purpose only · require verified identity metadata",TextWrapping=TextWrapping.Wrap });
        var common=new Button { Content="Inspect bundled common-task rules…" };
        common.Click+=async(_,_)=>await InspectCommonTasksAsync();
        panel.Children.Add(common);
        var count=new TextBlock { Text="Loading reusable rules…",TextWrapping=TextWrapping.Wrap }; panel.Children.Add(count);
        var clear=new Button { Content="Clear generated knowledge…" }; clear.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.GeneratedKnowledge);
        var corrections=new Button { Content="Clear user corrections…" }; corrections.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.UserCorrections);
        panel.Children.Add(clear); panel.Children.Add(corrections);
        var list=new StackPanel { Spacing=8 }; panel.Children.Add(list);
        PageContent.Children.Add(new ScrollViewer { Content=panel });
        _=LoadRulesAsync();
        async Task LoadRulesAsync()
        {
            try
            {
                var rules=await Task.Run(()=>knowledge.GetRulesAsync(lifetime.Token),lifetime.Token);
                count.Text=$"{rules.Count} rules. Rules require matching evidence; stale and rejected proposals do not classify new processes.";
                foreach(var rule in rules.OrderByDescending(r=>r.CreatedAt))
                {
                    var item=new StackPanel { Spacing=4 };
                    var displayState=rule.ReviewAt<=DateTimeOffset.UtcNow||rule.ExpiresAt<=DateTimeOffset.UtcNow?"Stale (history)":rule.State.ToString();
                    item.Children.Add(new TextBlock { Text=$"{displayState} · {string.Join(", ",rule.Assessments.Select(a=>a.Value))} · review {rule.ReviewAt.LocalDateTime:g}",TextWrapping=TextWrapping.Wrap });
                    item.Children.Add(new TextBlock { Text=string.Join("\n",rule.Predicates.Select(p=>$"{p.Field} {p.Kind}: {p.Value}")),TextWrapping=TextWrapping.Wrap,FontSize=12 });
                    var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
                    var inspect=new Button { Content="Inspect" }; inspect.Click+=async(_,_)=>
                    {
                        try
                        {
                            var records=await Task.Run(()=>knowledge.GetEvidenceAsync(rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds))
                                .Concat(rule.Assessments.SelectMany(a=>a.EvidenceIds)),lifetime.Token),lifetime.Token);
                            await MessageAsync("Rule evidence and provenance",System.Text.Json.JsonSerializer.Serialize(new { Rule=rule,Evidence=records },new System.Text.Json.JsonSerializerOptions { WriteIndented=true }));
                        }
                        catch { await MessageAsync("Rule evidence unavailable","Knowledge storage could not be read."); }
                    };
                    var toggle=new Button { Content=rule.State==RuleState.Active?"Disable":"Enable",IsEnabled=rule.State!=RuleState.Rejected };
                    toggle.Click+=async(_,_)=>
                    {
                        try { await Task.Run(()=>knowledge.SetRuleStateAsync(rule.Id,rule.State==RuleState.Active?RuleState.Disabled:RuleState.Active,lifetime.Token),lifetime.Token); coordinator.InvalidateKnowledge(); RenderPage(); }
                        catch { await MessageAsync("Rule cannot activate","Expired or invalid rules require fresh analysis."); }
                    };
                    var delete=new Button { Content="Delete" }; delete.Click+=async(_,_)=>
                    { try { await Task.Run(()=>knowledge.DeleteRuleAsync(rule.Id,lifetime.Token),lifetime.Token); coordinator.InvalidateKnowledge(); RenderPage(); }
                        catch { await MessageAsync("Rule could not be deleted","Knowledge storage is unavailable. Local collection continues."); } };
                    actions.Children.Add(inspect); actions.Children.Add(toggle); actions.Children.Add(delete); item.Children.Add(actions);
                    list.Children.Add(new Border { Padding=new Thickness(16),Child=item,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),BorderBrush=(Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"] });
                }
            }
            catch { count.Text="Knowledge unavailable. Local collection continues; use Settings to preserve and recover the database."; }
        }
    }
    private async Task InspectCommonTasksAsync()
    {
        var panel=new StackPanel { Spacing=8,Width=560 };
        var search=new TextBox { PlaceholderText="Filter by process, service, product, or publisher" };
        var list=new ListView { Height=240,SelectionMode=ListViewSelectionMode.Single };
        var information=new TextBlock { TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
        var link=new HyperlinkButton { Content="Open reference",Visibility=Visibility.Collapsed };
        panel.Children.Add(search); panel.Children.Add(list);
        panel.Children.Add(new ScrollViewer { Content=information,MaxHeight=160 }); panel.Children.Add(link);
        void Filter() {
            list.ItemsSource=ProcSight.Core.Rules.KnownTaskCatalog.Definitions.Where(d=>
                (d.Label+" "+d.Executable+" "+d.ServiceName+" "+d.Product+" "+string.Join(" ",d.Signers)).Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        list.DisplayMemberPath="Label";
        list.SelectionChanged+=(_,_)=> {
            if(list.SelectedItem is not ProcSight.Core.Rules.KnownTaskDefinition d) return;
            information.Text=$"{d.Executable}\nService: {d.ServiceName ?? "Process rule"}\nProduct: {d.Product ?? "Unknown"}\nOriginal file: {d.OriginalName ?? d.Executable}\nVerified signer: {string.Join(", ",d.Signers)}\n"+
                (d.PublisherOnly?"Publisher reference only; purpose remains unknown.":$"Purpose: {d.Purpose}. Security and optionality remain unresolved.");
            link.NavigateUri=new Uri(d.SourceUrl); link.Visibility=Visibility.Visible;
        };
        search.TextChanged+=(_,_)=>Filter(); Filter();
        await new ContentDialog { XamlRoot=PageContent.XamlRoot,Title="Bundled process and service rules",Content=panel,CloseButtonText="Close" }.ShowAsync();
    }
    private async Task ClearKnowledgeAsync(ClearScope scope)
    {
        var dialog=new ContentDialog { Title=scope==ClearScope.GeneratedKnowledge?"Clear generated knowledge?":"Clear user corrections?",
            Content="This removes the selected knowledge. Usage accounting and API credentials remain intact.",PrimaryButtonText="Clear",CloseButtonText="Cancel",XamlRoot=PageContent.XamlRoot };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
        try
        {
            await knowledgeMaintenance.WaitAsync(lifetime.Token);
            try
            {
                var next=policy with { Revision=policy.Revision+1 }; preferences.Save(next,presentation); policy=next;
                await coordinator.ChangePolicyAsync(policy,lifetime.Token); await coordinator.WaitForIdleAsync(lifetime.Token);
                await Task.Run(()=>knowledge.ClearAsync(scope,lifetime.Token),lifetime.Token);
                analysis.Clear(); coordinator.InvalidateKnowledge(); RenderPage();
            } finally { knowledgeMaintenance.Release(); }
        }
        catch { await MessageAsync("Knowledge could not be cleared","Check local database access. Local collection continues."); }
    }
}
