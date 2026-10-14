using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProcSight.Application.Analysis;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private static string ActivityProvider(string id)=>id switch { "chatgpt"=>"ChatGPT plan","openai"=>"OpenAI API","compatible"=>"Compatible API","local"=>"Local model",_=>id };
    private static string ActivityWords(string value)=>System.Text.RegularExpressions.Regex.Replace(value,"(?<=[a-z])(?=[A-Z])"," ");
    private static string ActivityOutcome(ProcSight.Core.Models.ProviderErrorKind? kind)=>kind switch {
        ProcSight.Core.Models.ProviderErrorKind.Auth=>"Sign-in or API credential required",
        ProcSight.Core.Models.ProviderErrorKind.RateLimited=>"Provider usage limit reached",
        ProcSight.Core.Models.ProviderErrorKind.Transient=>"Temporary provider or connection failure",
        ProcSight.Core.Models.ProviderErrorKind.Malformed=>"Explanation was invalid or incomplete",
        ProcSight.Core.Models.ProviderErrorKind.Refused=>"Provider declined this request",
        ProcSight.Core.Models.ProviderErrorKind.Unsupported=>"Provider does not support this request",
        ProcSight.Core.Models.ProviderErrorKind.Cancelled=>"Request cancelled",_=>"No assessments returned" };
    private static string ActivityField(string field)=>field switch {
        "name"=>"Executable","product"=>"Product","signer"=>"Verified publisher","originalName"=>"Original filename",
        "hash"=>"File hash","commandRole"=>"Invocation category","path"=>"File location","owner"=>"Application owner",
        "ownerIdentity"=>"Application identity","service"=>"Service","package"=>"Package identity","version"=>"Version",
        "company"=>"Metadata publisher","commandContext"=>"Invocation matching context",_=>field };
    private void RenderActivity()
    {
        var root=new Grid(); root.RowDefinitions.Add(new() { Height=GridLength.Auto }); root.RowDefinitions.Add(new() { Height=GridLength.Auto });
        root.RowDefinitions.Add(new() { Height=new GridLength(1,GridUnitType.Star) });
        var heading=new CommandBar { DefaultLabelPosition=CommandBarDefaultLabelPosition.Right,
            Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),Content=new TextBlock { Text="AI activity",FontSize=20,Margin=new Thickness(20,12,12,12) } };
        var pause=new AppBarToggleButton { Label="Pause recording",Icon=new SymbolIcon(Symbol.Pause),IsChecked=!aiActivity.Enabled };
        var clear=new AppBarButton { Label="Clear",Icon=new SymbolIcon(Symbol.Delete) };
        heading.PrimaryCommands.Add(pause); heading.PrimaryCommands.Add(clear); root.Children.Add(heading);
        var filter=new ComboBox { ItemsSource=new[]{"All requests","Running","Succeeded","Failed","Cancelled"},SelectedIndex=0,MinWidth=150 };
        var summary=new TextBlock { TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center };
        var bar=new Grid { Margin=new Thickness(20,4,20,12),ColumnSpacing=16 }; bar.ColumnDefinitions.Add(new() { Width=GridLength.Auto }); bar.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        bar.Children.Add(filter); Grid.SetColumn(summary,1); bar.Children.Add(summary); Grid.SetRow(bar,1); root.Children.Add(bar);
        var list=new ListView { SelectionMode=ListViewSelectionMode.Single,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(8,0,8,8) };
        var paneText=new TextBlock { Text="Select a request to inspect its submitted metadata summary and returned results.",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true,Margin=new Thickness(20) };
        var close=new Button { Content="Close details",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(12) };
        var pane=new Grid(); pane.RowDefinitions.Add(new() { Height=GridLength.Auto }); pane.RowDefinitions.Add(new() { Height=new GridLength(1,GridUnitType.Star) });
        pane.Children.Add(close); var paneScroll=new ScrollViewer { Content=paneText,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetRow(paneScroll,1); pane.Children.Add(paneScroll);
        var split=new SplitView { DisplayMode=SplitViewDisplayMode.Overlay,PanePlacement=SplitViewPanePlacement.Right,OpenPaneLength=400,Content=list,Pane=pane };
        split.SizeChanged+=(_,e)=>split.OpenPaneLength=Math.Min(440,Math.Max(240,e.NewSize.Width-24));
        close.Click+=(_,_)=>split.IsPaneOpen=false; Grid.SetRow(split,2); root.Children.Add(split);
        var visible=new Dictionary<Guid,ListViewItem>();
        void ShowDetails(AnalysisActivityEntry entry)
        {
            var details=new List<string> { entry.SourceName+(entry.ProcessId is { } pid?$" · PID {pid}":""),
                $"{ActivityWords(entry.Kind.ToString())} · {entry.Status}",$"Provider: {ActivityProvider(entry.ProviderId)} · Model: {entry.Model}",
                $"Started: {entry.StartedAt.ToLocalTime():G}",$"Duration: {(entry.Elapsed ?? DateTimeOffset.UtcNow-entry.StartedAt).TotalSeconds:0.0} seconds · Attempt {entry.Attempt}",
                $"Destination: {(entry.Destination==ProcSight.Core.Models.AnalysisDestination.LocalModel?"Local model":"Online provider")}",$"Command context sent: {(entry.CommandIncluded?"Yes; full line omitted from activity":"No")}",
                $"Evidence records sent: {entry.EvidenceCount}","","Submitted metadata categories: "+string.Join(", ",entry.MetadataFields.Select(ActivityField)) };
            if(entry.ServiceIdentity is { } service) details.Insert(1,"Service: "+service);
            foreach(var pair in entry.Metadata) details.Add(ActivityField(pair.Key)+": "+pair.Value);
            details.Add(""); details.Add("Returned results");
            foreach(var assessment in entry.Assessments) details.Add($"{assessment.Axis}: {ActivityWords(assessment.Value)} ({assessment.Confidence})\n{assessment.Reason}\n");
            details.Add($"Proposed reusable rules: {entry.ProposedRuleCount}");
            if(entry.Kind.ToString()=="Research") details.Add($"Returned evidence records: {entry.ReturnedEvidenceCount}");
            if(entry.ErrorKind is { } error) {
                var failure=ProcSight.Core.Models.ProviderFailureExplanation.Describe(error,entry.FailureReason);
                details.Add("What went wrong: "+failure.Summary); details.Add(failure.Details);
                details.Add("\nWhat to do next: "+failure.NextStep);
            }
            if(entry.Status.ToString()=="Running") details.Add("Waiting for the provider.");
            details.Add("\nHistory is kept only for this app session. Credentials, paths, full command lines and window captions are omitted.");
            paneText.Text=string.Join("\n",details);
        }
        void Refresh()
        {
            var all=aiActivity.Snapshot(); var entries=all.Where(e=>filter.SelectedIndex==0||e.Status.ToString()==filter.SelectedItem?.ToString()).ToArray();
            summary.Text=$"{all.Count} recent requests · up to 200 retained in memory · {(aiActivity.Enabled?"Recording":"Recording paused")}";
            var ids=entries.Select(e=>e.Id).ToHashSet();
            if(list.SelectedItem is ListViewItem { Tag:AnalysisActivityEntry selected }&&!ids.Contains(selected.Id)) split.IsPaneOpen=false;
            foreach(var id in visible.Keys.Where(id=>!ids.Contains(id)).ToArray()) { list.Items.Remove(visible[id]); visible.Remove(id); }
            for(var index=0;index<entries.Length;index++) {
                var entry=entries[index];
                if(!visible.TryGetValue(entry.Id,out var row)) { row=new ListViewItem { HorizontalContentAlignment=HorizontalAlignment.Stretch }; visible.Add(entry.Id,row); list.Items.Insert(index,row); }
                if(ReferenceEquals(row.Tag,entry)) continue;
                row.Tag=entry; var content=new Grid { Margin=new Thickness(8,8,8,8),ColumnSpacing=16 };
                content.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); content.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
                var text=new StackPanel { Spacing=4 };
                text.Children.Add(new TextBlock { Text=entry.SourceName+(entry.ProcessId is { } pid?$" · PID {pid}":""),TextWrapping=TextWrapping.Wrap,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold });
                text.Children.Add(new TextBlock { Text=$"{entry.StartedAt.ToLocalTime():T} · {ActivityProvider(entry.ProviderId)} / {entry.Model} · {ActivityWords(entry.Kind.ToString())} · attempt {entry.Attempt}",TextWrapping=TextWrapping.Wrap,FontSize=12,Opacity=0.8 });
                text.Children.Add(new TextBlock { Text=entry.Assessments.Count>0?string.Join(" · ",entry.Assessments.Select(a=>$"{a.Axis}: {ActivityWords(a.Value)}")):
                    entry.Status==AnalysisActivityStatus.Running?"Waiting for results":entry.Kind==AnalysisActivityKind.Research&&entry.Status==AnalysisActivityStatus.Succeeded?
                    $"Research returned {entry.ReturnedEvidenceCount} evidence records":entry.ErrorKind is { } error?
                    ProcSight.Core.Models.ProviderFailureExplanation.Describe(error,entry.FailureReason).Summary:ActivityOutcome(entry.ErrorKind),TextWrapping=TextWrapping.Wrap,FontSize=12 });
                content.Children.Add(text); var state=new TextBlock { Text=entry.Status.ToString(),VerticalAlignment=VerticalAlignment.Top,FontSize=12 }; Grid.SetColumn(state,1); content.Children.Add(state); row.Content=content;
                if(ReferenceEquals(list.SelectedItem,row)) ShowDetails(entry);
            }
        }
        list.SelectionChanged+=(_,_)=> { if(list.SelectedItem is ListViewItem { Tag:AnalysisActivityEntry selected }) { ShowDetails(selected); split.IsPaneOpen=true; } };
        pause.Click+=(_,_)=> { aiActivity.Enabled=pause.IsChecked!=true; Refresh(); };
        clear.Click+=(_,_)=> { aiActivity.Clear(); split.IsPaneOpen=false; Refresh(); };
        filter.SelectionChanged+=(_,_)=>Refresh();
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) }; timer.Tick+=(_,_)=>Refresh();
        root.Loaded+=(_,_)=>timer.Start(); root.Unloaded+=(_,_)=>timer.Stop(); Refresh(); PageContent.Children.Add(root);
    }
}
