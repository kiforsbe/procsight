using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Section preferences only: bounded app-owned keys, no item identities or displayed values.
    // Keep these for this window session, independently of selection and control recreation.
    private readonly Dictionary<string,bool> detailSectionChoices=[];
    private Expander RememberDetailSection(Expander section,string key)
    {
        if(detailSectionChoices.TryGetValue(key,out var expanded)) section.IsExpanded=expanded;
        section.RegisterPropertyChangedCallback(Expander.IsExpandedProperty,(sender,_)=>
            detailSectionChoices[key]=((Expander)sender).IsExpanded);
        return section;
    }
    private static TextBlock RuleText(string text,string style="BodyTextBlockStyle",bool secondary=false)
    {
        var foreground=secondary?"Foreground=\"{ThemeResource TextFillColorSecondaryBrush}\"":"";
        var block=(TextBlock)XamlReader.Load($"<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Style=\"{{StaticResource {style}}}\" {foreground} TextWrapping=\"Wrap\" IsTextSelectionEnabled=\"True\"/>");
        block.Text=text;
        return block;
    }
    private static Expander RuleSection(string title,UIElement content,bool expanded=false)=>new() {
        Header=title,Content=new Border { Padding=new Thickness(16),Child=content },IsExpanded=expanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
    private static StackPanel RuleFields(params (string Label,string Value)[] fields)
    {
        var panel=new StackPanel { Spacing=12 };
        foreach(var (label,value) in fields) {
            var row=new StackPanel { Spacing=4 }; row.Children.Add(RuleText(label,"CaptionTextBlockStyle",true)); row.Children.Add(RuleText(value)); panel.Children.Add(row);
        }
        return panel;
    }
    private StackPanel CreateLearnedRulePanel(RuleDefinition initial,IReadOnlyList<EvidenceRecord> allEvidence,Func<Task> changed,
        ResolvedEntity? entity=null,LocalAnalysis? current=null)
    {
        var rule=initial;
        var ids=rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds)).Concat(rule.Assessments.SelectMany(a=>a.EvidenceIds)).ToHashSet();
        var evidence=allEvidence.Where(e=>ids.Contains(e.Id)).ToArray();
        var panel=new StackPanel { Spacing=12 };
        panel.Children.Add(RuleText(RuleInspection.Name(rule,evidence),"SubtitleTextBlockStyle"));
        panel.Children.Add(RuleText(RuleInspection.Source(rule),"CaptionTextBlockStyle",true));
        var statusText=RuleText(RuleInspection.State(rule,DateTimeOffset.UtcNow),"BodyStrongTextBlockStyle"); panel.Children.Add(statusText);
        if(rule.State==RuleState.Rejected) panel.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Warning,
            Title="Why this rule was rejected",Message=RuleInspection.Rejection(rule,evidence,DateTimeOffset.UtcNow) });
        panel.Children.Add(RuleText(RuleInspection.Research(rule.Provenance?.Research),"CaptionTextBlockStyle",true));
        if(rule.State!=RuleState.Rejected&&rule.Assessments.All(a=>a.Value=="Unknown")) panel.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,
            Severity=InfoBarSeverity.Informational,Title="Identity remains unresolved",Message="Active means this rule is eligible to match. Its Unknown classifications do not identify this process or establish safety. Check the evidence and research outcome, then Analyze again when more information is available." });
        if(entity is not null) panel.Children.Add(RuleText(RuleInspection.ApplicationStatus(rule.Id,current)));
        var actions=new CommandBar { Background=null,DefaultLabelPosition=CommandBarDefaultLabelPosition.Right };
        var toggle=new AppBarButton { Icon=new SymbolIcon(Symbol.Accept) };
        var delete=new AppBarButton { Label="Delete rule",Icon=new SymbolIcon(Symbol.Delete) };
        actions.PrimaryCommands.Add(toggle); actions.SecondaryCommands.Add(delete); panel.Children.Add(actions);
        var feedback=new InfoBar { IsOpen=false,IsClosable=true };
        var confirmation=new StackPanel { Spacing=8,Visibility=Visibility.Collapsed };
        confirmation.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Warning,Title="Delete this learned rule?",Message="Findings that depend on it will be reconsidered. Bundled rules and credentials are preserved." });
        var confirmRow=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        var confirmDelete=new Button { Content="Delete rule" }; var cancel=new Button { Content="Cancel" }; confirmRow.Children.Add(confirmDelete); confirmRow.Children.Add(cancel); confirmation.Children.Add(confirmRow);
        panel.Children.Add(confirmation); panel.Children.Add(feedback);
        var busy=false; var deleted=false;
        void UpdateActions()
        {
            var now=DateTimeOffset.UtcNow;
            toggle.Label=rule.State==RuleState.Active?"Disable rule":"Enable rule";
            toggle.IsEnabled=!busy&&!deleted&&rule.State!=RuleState.Rejected&&(rule.State==RuleState.Active||rule.ExpiresAt>now&&rule.ReviewAt>now&&new RuleValidator().Validate(rule,evidence,now).Accepted);
            delete.IsEnabled=!busy&&!deleted; confirmDelete.IsEnabled=!busy; cancel.IsEnabled=!busy;
            statusText.Text=deleted?"Deleted":RuleInspection.State(rule,now);
        }
        void Feedback(string message,bool failed=false) { feedback.Message=message; feedback.Severity=failed?InfoBarSeverity.Error:InfoBarSeverity.Success; feedback.IsOpen=true; }
        toggle.Click+=async(_,_)=> {
            busy=true; UpdateActions();
            try {
                var state=rule.State==RuleState.Active?RuleState.Disabled:RuleState.Active;
                await Task.Run(()=>knowledge.SetRuleStateAsync(rule.Id,state,lifetime.Token),lifetime.Token);
                rule=rule with { State=state }; coordinator.InvalidateKnowledge(); Feedback("Rule state saved. Process classifications refresh on collection.");
                await changed();
            } catch(OperationCanceledException) { }
            catch { Feedback("Rule could not change state. Expired or unsupported rules require fresh analysis.",true); }
            finally { busy=false; UpdateActions(); }
        };
        delete.Click+=(_,_)=> { confirmation.Visibility=Visibility.Visible; confirmDelete.Focus(FocusState.Programmatic); };
        cancel.Click+=(_,_)=> { confirmation.Visibility=Visibility.Collapsed; delete.Focus(FocusState.Programmatic); };
        confirmDelete.Click+=async(_,_)=> {
            busy=true; UpdateActions();
            try {
                await Task.Run(()=>knowledge.DeleteRuleAsync(rule.Id,lifetime.Token),lifetime.Token);
                deleted=true; coordinator.InvalidateKnowledge(); confirmation.Visibility=Visibility.Collapsed; Feedback("Rule deleted. Dependent cached findings will be reconsidered.");
                await changed();
            } catch(OperationCanceledException) { }
            catch { Feedback("Rule could not be deleted. Knowledge storage may be unavailable.",true); }
            finally { busy=false; UpdateActions(); }
        };
        UpdateActions();
        panel.Children.Add(RuleText("Classifications","BodyStrongTextBlockStyle"));
        foreach(var a in rule.Assessments) panel.Children.Add(RememberDetailSection(RuleSection(a.Axis+" · "+RuleInspection.Classifications(rule with { Assessments=[a] }),
            RuleFields(("Confidence",a.Confidence.ToString()),("Explanation",a.Reason),("Review by",a.ExpiresAt.LocalDateTime.ToString("g"))),true),"rule/assessment-"+a.Axis));
        var conditions=new StackPanel { Spacing=12 }; conditions.Children.Add(RuleText("All conditions must match. A descriptive name does not expand this rule's scope.","CaptionTextBlockStyle",true));
        foreach(var p in rule.Predicates) {
            var text=RuleInspection.Value(p.Field,p.Value);
            if(entity is not null) text+="\nObserved: "+(entity.Facts.TryGetValue(p.Field,out var actual)?RuleInspection.Value(p.Field,actual)+(PredicateMatcher.Matches(p,actual)?" · matches":" · does not match"):"unavailable");
            conditions.Children.Add(RuleFields((RuleInspection.Field(p.Field)+(p.Kind==PredicateKind.PathRoot?" · under location":" · equals"),text)));
        }
        panel.Children.Add(RememberDetailSection(RuleSection("Matching conditions",conditions,true),"rule/conditions"));
        var validation=new RuleValidator().Validate(rule,evidence,DateTimeOffset.UtcNow);
        var evidenceContent=new StackPanel { Spacing=12 };
        evidenceContent.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=validation.Accepted?InfoBarSeverity.Informational:InfoBarSeverity.Warning,
            Title=validation.Accepted?"Evidence supports this rule":"Evidence needs review",Message=validation.Accepted?"A match does not guarantee contribution; corrections and conflicts can take precedence.":string.Join("; ",validation.Reasons.Distinct()) });
        foreach(var record in evidence) evidenceContent.Children.Add(RuleSection(record.Kind+" · "+record.Origin,RuleText(RuleInspection.Describe(record))));
        panel.Children.Add(RememberDetailSection(RuleSection($"Supporting evidence ({evidence.Length})",evidenceContent),"rule/evidence"));
        var history=RuleFields(("Rule ID",rule.Id.ToString()),("Created",rule.CreatedAt.LocalDateTime.ToString("g")),("Review by",rule.ReviewAt.LocalDateTime.ToString("g")),("Expires",rule.ExpiresAt.LocalDateTime.ToString("g")),("Privacy origin",rule.Origin.ToString()));
        if(rule.Provenance is { } provenance) history.Children.Add(RuleFields(("AI generation",provenance.GeneratedAt.LocalDateTime.ToString("g")),("Destination",provenance.Destination==AnalysisDestination.LocalModel?"Local model":"Online provider")));
        else history.Children.Add(RuleText("Provider/model history was not recorded for this rule."));
        if(rule.IdentityLabel is { } label) history.Children.Add(RuleFields(("Suggested identity",label)));
        if(rule.Role is { } role) history.Children.Add(RuleFields(("Suggested role",role)));
        history.Children.Add(RuleText("Provider provenance is an audit aid, not proof of identity or safety.","CaptionTextBlockStyle",true));
        panel.Children.Add(RememberDetailSection(RuleSection("Provenance & lifecycle",history),"rule/provenance"));
        return panel;
    }
    private StackPanel CreateBundledRulePanel(KnownTaskDefinition definition,ResolvedEntity? entity=null,LocalAnalysis? current=null)
    {
        var panel=new StackPanel { Spacing=12 };
        panel.Children.Add(RuleText(definition.Label,"SubtitleTextBlockStyle"));
        panel.Children.Add(RuleText("Bundled definition · Read-only","CaptionTextBlockStyle",true));
        if(entity is not null) panel.Children.Add(RuleText(RuleInspection.ApplicationStatus(definition.Id,current)));
        panel.Children.Add(RuleText(definition.PublisherOnly?"Publisher reference only; purpose remains unknown.":"Purpose: "+definition.Purpose+". Security and optionality remain unresolved."));
        panel.Children.Add(RememberDetailSection(RuleSection("Matching conditions",RuleFields(("Executable",definition.Executable),("Service",definition.ServiceName??"Process definition"),("Product",definition.Product??"Windows component / publisher gate"),("Original file",definition.OriginalName??definition.Executable),("Required verified signer",string.Join(", ",definition.Signers)),("Allowed locations",RuleInspection.Locations(definition))),true),"rule/conditions"));
        panel.Children.Add(new HyperlinkButton { Content="Open public reference",NavigateUri=new(definition.SourceUrl),HorizontalAlignment=HorizontalAlignment.Left });
        panel.Children.Add(RememberDetailSection(RuleSection("Definition details",RuleFields(("Rule ID",definition.Id.ToString()),("Reference",definition.SourceUrl))),"rule/definition"));
        return panel;
    }
}
