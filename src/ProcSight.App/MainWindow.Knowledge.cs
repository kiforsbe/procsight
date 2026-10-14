using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Application.Presentation;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private sealed record RuleLibraryItem(string Title,string Source,string State,RuleDefinition? Learned=null,KnownTaskDefinition? Bundled=null);
    private void RenderKnowledge()
    {
        if(knowledge is null) return;
        var root=new Grid { Margin=new Thickness(20) };
        root.RowDefinitions.Add(new() { Height=GridLength.Auto }); root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.RowDefinitions.Add(new() { Height=GridLength.Auto }); root.RowDefinitions.Add(new() { Height=new GridLength(1,GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.Children.Add(new TextBlock { Text="Rules & knowledge",FontSize=24,Margin=new Thickness(0,0,0,16) });
        var search=new TextBox { PlaceholderText="Search executable, product, condition, provider or model",HorizontalAlignment=HorizontalAlignment.Stretch };
        var source=new ComboBox { ItemsSource=new[]{"All sources","AI-generated","Bundled","Learned (legacy)","User correction"},SelectedIndex=0 };
        var state=new ComboBox { ItemsSource=new[]{"All states","Active","Disabled","Rejected","Expired","Review overdue","Available"},SelectedIndex=0 };
        var refresh=new Button { Content="Refresh rules" };
        var controls=new StackPanel { Spacing=8 }; controls.Children.Add(search);
        var filters=new Grid { ColumnSpacing=8 }; filters.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); filters.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); filters.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        filters.Children.Add(source); Grid.SetColumn(state,1); filters.Children.Add(state); Grid.SetColumn(refresh,2); filters.Children.Add(refresh); controls.Children.Add(filters);
        Grid.SetRow(controls,1); root.Children.Add(controls);
        var count=new TextBlock { Text="Loading rules…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,8) }; Grid.SetRow(count,2); root.Children.Add(count);
        var list=new ListView { SelectionMode=ListViewSelectionMode.None,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Grid.SetRow(list,3); root.Children.Add(list);
        var clear=new Button { Content="Clear generated knowledge…" }; clear.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.GeneratedKnowledge);
        var corrections=new Button { Content="Clear user corrections…" }; corrections.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.UserCorrections);
        var advanced=SettingsExpander("\uE713","Knowledge maintenance","Usage accounting and credentials are preserved",clear,corrections); Grid.SetRow(advanced,4); root.Children.Add(advanced);
        PageContent.Children.Add(root); var entries=new List<RuleLibraryItem>(); var closed=false;
        root.Unloaded+=(_,_)=>closed=true;
        void Filter()
        {
            if(closed) return;
            list.Items.Clear();
            var filtered=entries.Where(e=>source.SelectedIndex switch {
                1=>e.Learned?.Provenance is not null,2=>e.Bundled is not null,
                3=>e.Learned is { Provenance:null,Origin:not DataOrigin.UserCorrection },4=>e.Learned?.Origin==DataOrigin.UserCorrection,_=>true })
                .Where(e=>state.SelectedIndex==0||e.State==state.SelectedItem?.ToString())
                .Where(e=>(e.Title+" "+e.Source+" "+e.Learned?.Id+" "+string.Join(" ",e.Learned?.Predicates.Select(p=>p.Field+" "+p.Value)??[])+" "+e.Bundled?.Executable+" "+e.Bundled?.ServiceName+" "+e.Bundled?.Product+" "+string.Join(" ",e.Bundled?.Signers??[]))
                    .Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
            count.Text=$"{filtered.Length} shown · {entries.Count(e=>e.Learned is not null)} saved rules · {KnownTaskCatalog.Definitions.Count} bundled definitions. Select Inspect for conditions and evidence.";
            foreach(var entry in filtered) {
                var body=new StackPanel { Spacing=4 }; body.Children.Add(new TextBlock { Text=entry.Title,TextWrapping=TextWrapping.Wrap,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold });
                body.Children.Add(new TextBlock { Text=entry.Source+" · "+entry.State,TextWrapping=TextWrapping.Wrap,FontSize=12,Opacity=0.8 });
                var inspect=new Button { Content="Inspect",Margin=new Thickness(0,4,0,8) }; inspect.Click+=async(_,_)=> {
                    if(entry.Learned is { } rule) await InspectRuleAsync(rule);
                    else if(entry.Bundled is { } definition) await InspectBundledRuleAsync(definition);
                };
                body.Children.Add(inspect); list.Items.Add(new ListViewItem { Content=body,HorizontalContentAlignment=HorizontalAlignment.Stretch });
            }
        }
        async Task Load()
        {
            refresh.IsEnabled=false;
            try {
                var rules=await Task.Run(()=>knowledge.GetRulesAsync(lifetime.Token),lifetime.Token); if(closed) return;
                entries=rules.OrderByDescending(r=>r.CreatedAt).Select(r=>new RuleLibraryItem(RuleInspection.Title(r),RuleInspection.Source(r),RuleInspection.State(r,DateTimeOffset.UtcNow),r)).ToList();
                entries.AddRange(KnownTaskCatalog.Definitions.OrderBy(d=>d.Label).Select(d=>new RuleLibraryItem(d.Label,"Bundled definition","Available",Bundled:d))); Filter();
            } catch { if(!closed) count.Text="Knowledge unavailable. Use Settings to preserve and recover the database."; }
            finally { refresh.IsEnabled=true; }
        }
        search.TextChanged+=(_,_)=>Filter(); source.SelectionChanged+=(_,_)=>Filter(); state.SelectionChanged+=(_,_)=>Filter(); refresh.Click+=async(_,_)=>await Load(); _=Load();
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
