using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Automation;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Application.Presentation;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private sealed record RuleLibraryItem(string Title,string Source,string State,string Glyph,RuleDefinition? Learned=null,KnownTaskDefinition? Bundled=null)
    {
        public Guid Id=>Learned?.Id??Bundled!.Id;
        public string AccessibleName=>Title+", "+Source+", "+State;
    }
    private void RenderKnowledge()
    {
        if(knowledge is null) return;
        var root=new Grid { Margin=new Thickness(24,16,24,24),RowSpacing=12 };
        for(var i=0;i<3;i++) root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.RowDefinitions.Add(new() { Height=new GridLength(1,GridUnitType.Star) });
        var commands=new CommandBar { Background=null,DefaultLabelPosition=CommandBarDefaultLabelPosition.Right,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        commands.Content=RuleText("Rules & knowledge","TitleTextBlockStyle");
        var refresh=new AppBarButton { Label="Refresh",Icon=new SymbolIcon(Symbol.Refresh) };
        commands.PrimaryCommands.Add(refresh);
        var clear=new AppBarButton { Label="Clear generated knowledge",Icon=new SymbolIcon(Symbol.Delete) };
        clear.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.GeneratedKnowledge);
        var corrections=new AppBarButton { Label="Clear user corrections",Icon=new SymbolIcon(Symbol.Delete) };
        corrections.Click+=async(_,_)=>await ClearKnowledgeAsync(ClearScope.UserCorrections);
        commands.SecondaryCommands.Add(clear); commands.SecondaryCommands.Add(corrections); root.Children.Add(commands);
        var filters=new StackPanel { Spacing=8 };
        var searchBox=new TextBox { PlaceholderText="Search rules by app, executable, condition or model" };
        AutomationProperties.SetName(searchBox,"Search rules"); filters.Children.Add(searchBox);
        var source=new ComboBox { ItemsSource=new[]{"All sources","AI-generated","Bundled","Learned (legacy)","User correction"},SelectedIndex=0,MinWidth=160 };
        var state=new ComboBox { ItemsSource=new[]{"All states","Active","Disabled","Rejected","Expired","Review overdue","Available"},SelectedIndex=0,MinWidth=160 };
        AutomationProperties.SetName(source,"Rule source"); AutomationProperties.SetName(state,"Rule state");
        var filterRow=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 }; filterRow.Children.Add(source); filterRow.Children.Add(state); filters.Children.Add(filterRow);
        Grid.SetRow(filters,1); root.Children.Add(filters);
        var count=RuleText("Loading rules…","CaptionTextBlockStyle",secondary:true); Grid.SetRow(count,2); root.Children.Add(count);
        var body=new Grid { ColumnSpacing=16 }; body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new()); Grid.SetRow(body,3); root.Children.Add(body);
        var list=new ListView { SelectionMode=ListViewSelectionMode.Single,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        AutomationProperties.SetName(list,"Rules");
        list.ItemTemplate=(DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Padding="4,8" ColumnSpacing="12" AutomationProperties.Name="{Binding AccessibleName}">
                <Grid.ColumnDefinitions><ColumnDefinition Width="32"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <FontIcon Glyph="{Binding Glyph}" FontFamily="Segoe Fluent Icons" FontSize="20" VerticalAlignment="Center"/>
                <StackPanel Grid.Column="1" Spacing="4">
                  <TextBlock Text="{Binding Title}" Style="{StaticResource BodyStrongTextBlockStyle}" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis" ToolTipService.ToolTip="{Binding Title}"/>
                  <TextBlock Text="{Binding Source}" Style="{StaticResource CaptionTextBlockStyle}" TextWrapping="NoWrap" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextTrimming="CharacterEllipsis" ToolTipService.ToolTip="{Binding Source}"/>
                </StackPanel>
                <TextBlock Grid.Column="2" Text="{Binding State}" Style="{StaticResource CaptionTextBlockStyle}" VerticalAlignment="Center" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
              </Grid>
            </DataTemplate>
            """);
        var listArea=new Grid(); listArea.Children.Add(list);
        var empty=new InfoBar { IsOpen=false,IsClosable=false,Severity=InfoBarSeverity.Informational,Title="No matching rules",Message="Try a different search or reset the source and state filters.",VerticalAlignment=VerticalAlignment.Top };
        listArea.Children.Add(empty); body.Children.Add(listArea);
        var details=new ScrollViewer { HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        var detailFrame=(Border)XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Padding="16" CornerRadius="8" BorderThickness="1"
             BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}" Background="{ThemeResource LayerFillColorDefaultBrush}"/>
            """);
        detailFrame.Child=details; Grid.SetColumn(detailFrame,1); body.Children.Add(detailFrame);
        var back=new Button { Content="Back to rules",HorizontalAlignment=HorizontalAlignment.Left };
        AutomationProperties.SetName(back,"Back to rules list");
        var entries=new List<RuleLibraryItem>(); IReadOnlyList<EvidenceRecord> evidence=[]; var closed=false; var compactDetails=false; var loading=false; var reloadPending=false;
        root.Unloaded+=(_,_)=>closed=true;
        void Layout()
        {
            var wide=root.ActualWidth>=880;
            body.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);
            body.ColumnDefinitions[1].Width=wide?new GridLength(1.15,GridUnitType.Star):new GridLength(0);
            listArea.Visibility=wide||!compactDetails?Visibility.Visible:Visibility.Collapsed;
            detailFrame.Visibility=wide||compactDetails?Visibility.Visible:Visibility.Collapsed;
            Grid.SetColumn(detailFrame,wide?1:0); back.Visibility=wide?Visibility.Collapsed:Visibility.Visible;
        }
        void ShowSelection()
        {
            if(closed) return;
            if(details.Content is StackPanel oldPanel) oldPanel.Children.Remove(back);
            var panel=new StackPanel { Spacing=12 }; panel.Children.Add(back);
            if(list.SelectedItem is RuleLibraryItem entry) {
                compactDetails=true;
                if(entry.Learned is { } rule) panel.Children.Add(CreateLearnedRulePanel(rule,evidence,async()=>await Load()));
                else panel.Children.Add(CreateBundledRulePanel(entry.Bundled!));
            } else {
                compactDetails=false;
                panel.Children.Add(RuleText("Select a rule","SubtitleTextBlockStyle"));
                panel.Children.Add(RuleText("See its classifications, matching conditions, evidence and origin."));
            }
            details.Content=panel; details.ChangeView(null,0,null,true); Layout();
        }
        void Filter(bool preserveScroll=false)
        {
            if(closed) return;
            var selected=(list.SelectedItem as RuleLibraryItem)?.Id;
            var wasDetails=compactDetails;
            var offset=preserveScroll?FindTableScrollViewer(list)?.VerticalOffset:null;
            var filtered=entries.Where(e=>source.SelectedIndex switch {
                1=>e.Learned?.Provenance is not null,2=>e.Bundled is not null,
                3=>e.Learned is { Provenance:null,Origin:not DataOrigin.UserCorrection },4=>e.Learned?.Origin==DataOrigin.UserCorrection,_=>true })
                .Where(e=>state.SelectedIndex==0||e.State==state.SelectedItem?.ToString())
                .Where(e=>(e.Title+" "+e.Source+" "+e.Id+" "+string.Join(" ",e.Learned?.Predicates.Select(p=>p.Field+" "+p.Value)??[])+" "+e.Bundled?.Executable+" "+e.Bundled?.ServiceName+" "+e.Bundled?.Product+" "+string.Join(" ",e.Bundled?.Signers??[])).Contains(searchBox.Text,StringComparison.OrdinalIgnoreCase)).ToArray();
            list.ItemsSource=filtered;
            list.SelectedItem=filtered.FirstOrDefault(e=>e.Id==selected);
            count.Text=$"{filtered.Length} rules · {entries.Count(e=>e.Learned is not null)} learned · {KnownTaskCatalog.Definitions.Count} bundled";
            empty.IsOpen=filtered.Length==0;
            ShowSelection();
            compactDetails=list.SelectedItem is not null&&wasDetails; Layout();
            if(offset is { } position) DispatcherQueue.TryEnqueue(()=> { if(!closed) FindTableScrollViewer(list)?.ChangeView(null,position,null,true); });
        }
        async Task Load()
        {
            if(loading) { reloadPending=true; return; } loading=true; refresh.IsEnabled=false;
            try {
                var rules=await Task.Run(()=>knowledge.GetRulesAsync(lifetime.Token),lifetime.Token);
                var records=await Task.Run(()=>knowledge.GetEvidenceAsync(rules.SelectMany(r=>r.EvidenceIds.Concat(r.Predicates.SelectMany(p=>p.EvidenceIds)).Concat(r.Assessments.SelectMany(a=>a.EvidenceIds))),lifetime.Token),lifetime.Token);
                if(closed||reloadPending) return; evidence=records;
                entries=rules.OrderByDescending(r=>r.CreatedAt).Select(r=>new RuleLibraryItem(RuleInspection.Name(r,evidence)+" · "+RuleInspection.Classifications(r),RuleInspection.Source(r),RuleInspection.State(r,DateTimeOffset.UtcNow),"\uE8BD",r)).ToList();
                entries.AddRange(KnownTaskCatalog.Definitions.OrderBy(d=>d.Label).Select(d=>new RuleLibraryItem(d.Label,"Bundled definition · Read-only","Available","\uE8A5",Bundled:d))); Filter(true);
            } catch(OperationCanceledException) { }
            catch { if(!closed) { count.Text="Rules could not be loaded. Refresh to try again."; } }
            finally {
                loading=false;
                if(!closed&&reloadPending) { reloadPending=false; await Load(); }
                else if(!closed) refresh.IsEnabled=true;
            }
        }
        back.Click+=(_,_)=> { compactDetails=false; Layout(); list.Focus(FocusState.Programmatic); };
        list.SelectionChanged+=(_,_)=>ShowSelection();
        list.ItemClick+=(_,_)=>ShowSelection(); list.IsItemClickEnabled=true;
        root.SizeChanged+=(_,_)=>Layout();
        searchBox.TextChanged+=(_,_)=>Filter(); source.SelectionChanged+=(_,_)=>Filter(); state.SelectionChanged+=(_,_)=>Filter(); refresh.Click+=async(_,_)=>await Load();
        PageContent.Children.Add(root); ShowSelection(); _=Load();
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
