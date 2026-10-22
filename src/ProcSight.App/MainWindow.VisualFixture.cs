#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Application.Presentation;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Developer-only rendering of the actual XAML tree. No machine collection, preferences,
    // credential access or provider requests occur in this mode.
    private bool TryStartVisualFixture()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Length is <3 or >6 || args[1]!="--visual-fixture") return false;
        var output=Path.GetFullPath(args[2]);
        chatGptAuthenticationLog=new(output+".auth.log");
        if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) {
            elevated=args[5]=="settings-admin";
            var fixtureCredentials=new FixtureCredentials();
            var protectedFixture=new ProcSight.Infrastructure.Authentication.ChatGptProtectedStore(fixtureCredentials);
            protectedFixture.Set("catalog",System.Text.Json.JsonSerializer.Serialize(new { HostId="urn:uuid:00000000-0000-0000-0000-000000000001",ActiveAccountId="fixture-account",
                Accounts=new[]{new ProcSight.Infrastructure.Authentication.ChatGptAccount("fixture-account","person@example.invalid","fixture-client")} }));
            protectedFixture.Set("account:fixture-account",System.Text.Json.JsonSerializer.Serialize(new { Issuer="https://auth.openai.com",Subject="fixture-subject",ClientId="fixture-client",
                AccessToken="synthetic-access",RefreshToken="synthetic-refresh",Scopes="openid offline_access resource.invoke chatgpt.tokens.use.direct",Expires=DateTimeOffset.UtcNow.AddHours(1) }));
            chatGpt=new(fixtureCredentials,new HttpClient(new FixtureModelHandler())); page="Settings";
            policy=new(Connections:[
                new("fixture-local",new("local",new("http://127.0.0.1:1234/v1"),"Local example model"),true,0),
                new("fixture-plan",new("chatgpt",new("https://api.openai.com/v1"),"Example account model",AccountId:"fixture-account"),true,1),
                new("fixture-api",new("openai",new("https://api.openai.com/v1"),"Example API model"),false,2)]);
        }
        presentation=new(Theme:args.Length>3&&args[3]=="Light"?"Light":"Dark",Grouped:args.Length>5&&args[5]=="groups"); sort=ProcSight.Application.Presentation.ProcessSort.Name;
        ConfigureChrome(); WindowRoot.Background=new SolidColorBrush(presentation.Theme=="Light"?Microsoft.UI.Colors.White:global::Windows.UI.Color.FromArgb(255,32,32,32));
        if(elevated) AppTitleBar.Title="ProcSight (Administrator)";
        if(args.Length>4&&int.TryParse(args[4],out var width)&&width is >=600 and <=1600)
        { var scale=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))/96.0; AppWindow.Resize(new((int)(width*scale),(int)(780*scale))); }
        var now=DateTimeOffset.UtcNow;
        string? fixtureRulesDatabase=null;
        var names=new[]{"Code.exe","chrome.exe","node.exe","node.exe","node.exe","svchost.exe","svchost.exe",
            "OneDrive.exe","Plex Media Server.exe","NVDisplay.Container.exe","Notepad.exe","pwsh.exe",
            "RuntimeBroker.exe","SearchIndexer.exe","Widgets.exe","msedgewebview2.exe","msedgewebview2.exe",
            "explorer.exe","ApplicationFrameHost.exe","dllhost.exe","conhost.exe","spoolsv.exe","SecurityHealthService.exe"};
        var processes=names.Select((name,i)=>new ProcessSnapshot(new(1000+i,now,null),null,
            new(name,name=="Notepad.exe"?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe"):$@"C:\Program Files\Example\{name}","fixture-"+i,Product:name=="node.exe"?"Example editor":null),
            new(Observation<double>.Available(i==0?3.2:0),Observation<long>.Available((12+i*5)*1048576L),
                Observation<double>.Missing("Not collected"),Observation<double>.Missing("Not collected"),now,TimeSpan.FromSeconds(1)),[],
                name=="node.exe"?new CommandLineValue(i==2?"node.exe --type=extensionHost":i==3?"node.exe --type=renderer":"node.exe --type=gpu-process",now):null)).ToList();
        if(args.Length>5&&args[5] is "scroll" or "layout" or "live-sort") {
            processes.AddRange(Enumerable.Range(0,60).Select(i=>processes[0] with { Key=new(2000+i,now,null),
                Executable=new("python.exe",Product:"Python"),CommandLine=new($"python.exe script_{i:00}.py --token PRIVATE",now) }));
        }
        var fixtureLifetime=new ProcSight.Windows.Collection.ProcessLifetimeTracker();
        if(args.Length>5&&args[5] is "process-io" or "resource-shading" or "resource-shading-settings" or "live-sort") {
            processes=processes.Select((p,index)=>p with { Resources=p.Resources with {
                DiskBytesPerSecond=index==0?Observation<double>.Missing("Synthetic permission unavailable"):Observation<double>.Available(index*1000000),
                NetworkBytesPerSecond=index==0?Observation<double>.Pending():Observation<double>.Available(index*125000),
                GpuPercent=index==0?Observation<double>.Pending():Observation<double>.Available(17) } }).ToList();
        }
        if(args.Length>5&&args[5]=="details-restricted") {
            processes[2]=processes[2] with { Key=fixtureLifetime.Resolve(1002,null,77,Guid.NewGuid()),Executable=new("restricted.exe"),CommandLine=null,
                MetadataIssues=[new("ExecutablePath",MetadataUnavailableReason.AccessDenied),new("Started",MetadataUnavailableReason.AccessDenied),new("CommandLine",MetadataUnavailableReason.AccessDenied)] };
        }
        lastSnapshot=new(Guid.NewGuid(),now,processes,[],new(36,16L*1024*1024*1024,7L*1024*1024*1024));
        if(args.Length>5&&args[5] is "details-service-selection" or "columns-services") lastSnapshot=lastSnapshot with { Services=[
            new("FixtureServiceA","Fixture service A","Running","Auto",1000,"host.exe --fixture-a",ObservationState.Available,new("fixture-a.dll")),
            new("FixtureServiceB","Fixture service B","Stopped","Manual",null,"host.exe --fixture-b",ObservationState.Available,new("fixture-b.dll"))] };
        entities=new IdentityResolver().Resolve(lastSnapshot,[]);
        var node=entities.First(e=>e.Process?.Key.Pid==1002);
        analysis[node.Identity]=new(node.Identity,AnalysisState.Local,new([
            new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture: application worker",Confidence.Medium,[],now.AddDays(7),[DataOrigin.ShareableMetadata])],
            "Example editor",[],[],new HashSet<AssessmentAxis>{AssessmentAxis.Security,AssessmentAxis.Optionality},"Example editor — extension host","Worker"));
        if(args.Length>5&&(args[5].StartsWith("rules",StringComparison.Ordinal)||args[5]=="details-evidence")) {
            fixtureRulesDatabase=Path.Combine(Path.GetTempPath(),"procsight-rules-fixture-"+Guid.NewGuid()+".db");
            knowledge=new(fixtureRulesDatabase);
            coordinator=new(knowledge,knowledge,(_,_)=>throw new InvalidOperationException("Fixture has no provider."));
            var purpose=analysis[node.Identity].Result.Assessments[0] with { EvidenceIds=[node.Evidence[0].Id] };
            var learned=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(7),
                [new(PredicateKind.Exact,"hash",node.Facts["hash"],[node.Evidence[0].Id])],[purpose],[node.Evidence[0].Id],100,
                Provenance:new("chatgpt","example-model",AnalysisDestination.CloudModel,now));
            if(args[5]=="rules-rejected") learned=learned with { State=RuleState.Rejected,RejectionReasons=[RuleRejectionReason.PrivatePredicateOmitted],
                Provenance=learned.Provenance! with { Research=ResearchOutcome.SourcesFound } };
            if(args[5]=="rules-unknown") learned=learned with { Assessments=[purpose with { Value="Unknown" }],
                Provenance=learned.Provenance! with { Research=ResearchOutcome.Unavailable } };
            knowledge.SaveAsync(new(node.Identity,now,[purpose],node.Evidence,[learned],[]),default).GetAwaiter().GetResult();
            var update=analysis[node.Identity]; analysis[node.Identity]=update with { Result=update.Result with { MatchingRuleIds=[learned.Id],AppliedRuleIds=[learned.Id] } };
            page=args[5].StartsWith("rules",StringComparison.Ordinal)&&args[5]!="rules-process"?"Knowledge":"Processes";
        }
        if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) page="Settings";
        if(args.Length>5&&args[5].StartsWith("activity",StringComparison.Ordinal)) {
            page="AI activity";
            for(var i=0;i<8;i++) {
                var payload=new AnalysisPayload(new("fixture-activity-"+i),new(){["name"]=i%2==0?"node.exe":"example-service.exe",["product"]="Example editor"},[],null,0,AnalysisDestination.CloudModel);
                var request=aiActivity.Begin(payload,new("chatgpt",new("https://api.openai.com/v1"),"example-model"),i%2==0?"Example editor — Renderer":"Example service",i%2==0?1000+i:null);
                if(i==7) continue;
                aiActivity.Complete(request,i==6?new(null,new(ProviderErrorKind.Malformed,"Synthetic validation failure",FailureReason:ProviderFailureReason.UnknownEvidence)):new(new([
                    new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture: this worker renders the application's interface.",Confidence.Medium,[],now.AddDays(1),[DataOrigin.ShareableMetadata]),
                    new(AssessmentAxis.Security,"Unknown","No threat evidence was submitted.",Confidence.Low,[],now.AddDays(1),[DataOrigin.ShareableMetadata])],[])),payload);
            }
        }
        if(args.Length>5&&args[5]=="details-restricted") analysis.Remove(node.Identity);
        if(args.Length>5&&args[5]=="services") {
            var iconFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe");
            lastSnapshot=lastSnapshot with { Services=[
                new("SyntheticImplementation","Implementation icon service","Running","Auto",1002,null,ObservationState.Available,new("notepad.exe",iconFile)),
                new("SyntheticHost","Configured host icon service","Running","Manual",1002,null,ObservationState.Available,HostExecutable:new("notepad.exe",iconFile)),
                new("SyntheticFallback","Service icon unavailable","Stopped","Manual",null,null,ObservationState.Unavailable),
                new("SyntheticLong","Long service name that must stay within the Name column and trim gracefully without covering assessment or PID","Running","Auto",null,null,ObservationState.Unavailable)] };
            entities=new IdentityResolver().Resolve(lastSnapshot,[]); page="Services";
        }
        RenderPage();
        if(args.Length>5&&args[5].StartsWith("details",StringComparison.Ordinal))
        { detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
        WindowRoot.Loaded+=async(_,_)=>
        {
            try
            {
                await Task.Delay(500);
                if(args.Length>5&&args[5]=="shutdown-check") { await CheckShutdownAsync(output); return; }
                if(args.Length>5&&args[5]=="row-performance-checks") {
                    CheckProcessRowPerformanceContracts();
                    File.WriteAllText(output+".checks","Row notification and metadata invalidation contracts pass.");
                }
                if(args.Length>5&&args[5]=="ui-performance") await CheckUiPerformanceAsync(output);
                if(args.Length>5&&args[5]=="ui-memory") await CheckMemoryPerformanceAsync(output);
                if(args.Length>5&&args[5].StartsWith("table-memory-",StringComparison.Ordinal))
                    await CheckTableMemoryAsync(output,args[5]=="table-memory-default"?-1:double.Parse(args[5][13..],System.Globalization.CultureInfo.InvariantCulture));
                if(args.Length>5&&args[5] is "chart-performance" or "chart-reuse") await CheckChartPerformanceAsync(output,args[5]=="chart-reuse");
                if(args.Length>5&&args[5]=="local-sampling") await CheckLocalSamplingAsync(output);
                if(args.Length>5&&args[5]=="ui-performance-rules") await CheckUiPerformanceAsync(output,true);
                if(args.Length>5&&args[5]=="process-io") {
                    var measured=rows.Single(r=>r.Entity.Process?.Key.Pid==1001);
                    var unavailable=rows.Single(r=>r.Entity.Process?.Key.Pid==1000);
                    if(measured.Disk!=ResourceRatePresentation.Text([Observation<double>.Available(1000000)])||
                        measured.Network!=ResourceRatePresentation.Text([Observation<double>.Available(125000)],true)||
                        unavailable.Disk!="—"||unavailable.Network!="…"||!unavailable.DiskDetails.Contains("Synthetic permission")||
                        measured.Gpu!=ResourceRatePresentation.GpuText([Observation<double>.Available(17)])||unavailable.Gpu!="…"||
                        tableColumnLayout.All(c=>c.Id!="Gpu")||processIoNotice is not { IsOpen:true,ActionButton:not null })
                        throw new InvalidOperationException("Process throughput values or availability did not bind to live rows.");
                }
                if(args.Length>5&&args[5] is "resource-shading" or "resource-shading-settings" or "live-sort") await CheckResourceShadingAsync(args[5]);
                if(args.Length>5&&args[5]=="services") {
                    Navigation.SelectedItem=Navigation.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First(i=>i.Tag?.ToString()=="Services"); page="Services"; RenderPage(); await Task.Delay(350);
                }
                if(args.Length>5&&args[5] is "columns" or "columns-services" or "column-drag" or "columns-pointer" or "row-menu" or "column-chooser" or "inventory-export" or "export-picker")
                    await CheckTableCustomizationAsync(args[5],output);
                if(args.Length>5&&args[5].StartsWith("rules",StringComparison.Ordinal)) {
                    if(args[5]!="rules-process") { Navigation.SelectedItem=Navigation.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First(i=>i.Tag?.ToString()=="Knowledge"); page="Knowledge"; RenderPage(); }
                    else { page="Processes"; RenderPage(); detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
                    await Task.Delay(350); WindowRoot.UpdateLayout();
                    if(args[5] is "rules-detail" or "rules-actions" or "rules-rejected" or "rules-unknown" or "rules-selection") {
                        Microsoft.UI.Xaml.Controls.ListView? FindRulesList(DependencyObject parent) {
                            if(parent is Microsoft.UI.Xaml.Controls.ListView found) return found;
                            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindRulesList(VisualTreeHelper.GetChild(parent,i)) is { } nested) return nested;
                            return null;
                        }
                        var ruleList=FindRulesList(PageContent) ?? throw new InvalidOperationException("Missing rules list.");
                        if(ruleList.Items.Count!=374) throw new InvalidOperationException("Rules were not loaded.");
                        ruleList.SelectedIndex=0; await Task.Delay(350); WindowRoot.UpdateLayout();
                        if(args[5]=="rules-selection") {
                            Microsoft.UI.Xaml.Controls.Expander? Conditions(DependencyObject parent) {
                                if(parent is Microsoft.UI.Xaml.Controls.Expander e&&e.Header as string=="Matching conditions") return e;
                                for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(Conditions(VisualTreeHelper.GetChild(parent,i)) is { } found) return found;
                                return null;
                            }
                            Conditions(PageContent)!.IsExpanded=false;
                            ruleList.SelectedIndex=1; await Task.Delay(150);
                            if(Conditions(PageContent)!.IsExpanded) throw new InvalidOperationException("Rule selection reset collapsed conditions.");
                            Conditions(PageContent)!.IsExpanded=true;
                            ruleList.SelectedIndex=2; await Task.Delay(150);
                            if(!Conditions(PageContent)!.IsExpanded) throw new InvalidOperationException("Rule selection reset expanded conditions.");
                            ruleList.SelectedIndex=0; await Task.Delay(150);
                            if(!Conditions(PageContent)!.IsExpanded) throw new InvalidOperationException("Learned rule did not retain shared section choice.");
                            File.WriteAllText(output+".checks","Learned/bundled rule selections retain both expanded and collapsed section choices.");
                        }
                        if(args[5]=="rules-actions") {
                            T? FindControl<T>(DependencyObject parent,Func<T,bool> predicate) where T:DependencyObject {
                                if(parent is T match&&predicate(match)) return match;
                                for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindControl(VisualTreeHelper.GetChild(parent,i),predicate) is { } nested) return nested;
                                return null;
                            }
                            void Invoke(Microsoft.UI.Xaml.Controls.Button button) {
                                var peer=Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(button);
                                var provider=(Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke);
                                provider.Invoke();
                            }
                            Microsoft.UI.Xaml.Controls.AppBarButton Action(string label) {
                                var bar=FindControl<Microsoft.UI.Xaml.Controls.CommandBar>(PageContent,b=>b.PrimaryCommands.Concat(b.SecondaryCommands).OfType<Microsoft.UI.Xaml.Controls.AppBarButton>().Any(a=>a.Label==label));
                                return bar?.PrimaryCommands.Concat(bar.SecondaryCommands).OfType<Microsoft.UI.Xaml.Controls.AppBarButton>().First(a=>a.Label==label)??throw new InvalidOperationException("Missing rule action: "+label);
                            }
                            var selectedRuleId=(ruleList.SelectedItem as RuleLibraryItem)?.Id;
                            Invoke(Action("Refresh")); await Task.Delay(400);
                            if((ruleList.SelectedItem as RuleLibraryItem)?.Id!=selectedRuleId) throw new InvalidOperationException("Refresh lost selection.");
                            Invoke(Action("Disable rule")); await Task.Delay(400);
                            if((await knowledge.GetRulesAsync(default)).Single().State!=RuleState.Disabled) throw new InvalidOperationException("Disable did not save.");
                            Invoke(Action("Enable rule")); await Task.Delay(400);
                            if((await knowledge.GetRulesAsync(default)).Single().State!=RuleState.Active) throw new InvalidOperationException("Enable did not save.");
                            Invoke(Action("Delete rule")); await Task.Delay(100);
                            if((await knowledge.GetRulesAsync(default)).Count!=1) throw new InvalidOperationException("Delete skipped confirmation.");
                            var cancel=FindControl<Microsoft.UI.Xaml.Controls.Button>(PageContent,b=>b.Content as string=="Cancel")!;
                            Invoke(cancel); await Task.Delay(100);
                            if((await knowledge.GetRulesAsync(default)).Count!=1) throw new InvalidOperationException("Cancel deleted the rule.");
                            Invoke(Action("Delete rule")); await Task.Delay(100);
                            var confirm=FindControl<Microsoft.UI.Xaml.Controls.Button>(PageContent,b=>b.Content as string=="Delete rule")!;
                            Invoke(confirm); await Task.Delay(400);
                            if((await knowledge.GetRulesAsync(default)).Count!=0||ruleList.Items.Count!=373) throw new InvalidOperationException("Confirmed delete did not refresh.");
                            var sourceFilter=FindControl<Microsoft.UI.Xaml.Controls.ComboBox>(PageContent,b=>b.Items.Contains("All sources"))!;
                            sourceFilter.SelectedIndex=1; await Task.Delay(100);
                            if(ruleList.Items.Count!=0) throw new InvalidOperationException("Source filter did not update.");
                            sourceFilter.SelectedIndex=0;
                            var ruleSearch=FindControl<Microsoft.UI.Xaml.Controls.TextBox>(PageContent,b=>b.PlaceholderText.StartsWith("Search rules"))!;
                            ruleSearch.Text=".NET application host"; await Task.Delay(100);
                            if(ruleList.Items.Count!=1) throw new InvalidOperationException("Search did not narrow the list: "+ruleList.Items.Count);
                            ruleSearch.Text=""; await Task.Delay(100);
                            if(ruleList.Items.Count!=373) throw new InvalidOperationException("Search reset did not restore bundled rules.");
                        }
                    }
                }
                if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) {
                    Navigation.SelectedItem=Navigation.SettingsItem; page="Settings"; RenderPage();
                    if(args[5]=="settings-expanded"&&PageContent.Children.FirstOrDefault() is Microsoft.UI.Xaml.Controls.ScrollViewer { Content:Microsoft.UI.Xaml.Controls.StackPanel settingsPanel })
                        settingsPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().First().IsExpanded=true;
                    await Task.Delay(250); WindowRoot.UpdateLayout();
                    if(PageContent.Children.FirstOrDefault() is not Microsoft.UI.Xaml.Controls.ScrollViewer { Content:Microsoft.UI.Xaml.Controls.StackPanel footerPanel } footerScroll||
                        footerPanel.Children.LastOrDefault() is not Microsoft.UI.Xaml.Controls.TextBlock versionFooter)
                        throw new InvalidOperationException("Settings must end with the application version footer.");
                    var runningVersion=typeof(App).Assembly.GetName().Version??throw new InvalidOperationException("Running assembly version missing.");
                    if(versionFooter.Text!=$"ProcSight {runningVersion.Major}.{runningVersion.Minor}.{runningVersion.Build}"||!versionFooter.IsTextSelectionEnabled||
                        Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(versionFooter)!="Application version: "+versionFooter.Text)
                        throw new InvalidOperationException("Settings version footer must expose the running product version as accessible selectable text.");
                    if(args[5]=="settings-version") {
                        footerScroll.ChangeView(null,footerScroll.ScrollableHeight,null,true);
                        await Task.Delay(250); WindowRoot.UpdateLayout();
                        var footerPosition=versionFooter.TransformToVisual(footerScroll).TransformPoint(new global::Windows.Foundation.Point());
                        if(footerPosition.Y<0||footerPosition.Y+versionFooter.ActualHeight>footerScroll.ActualHeight+1||
                            footerPosition.X<0||footerPosition.X+versionFooter.ActualWidth>footerScroll.ActualWidth+1)
                            throw new InvalidOperationException("Settings version footer cannot be fully reached within its scroll viewport.");
                        File.WriteAllText(output+".checks","Settings footer matches the running assembly product version and is selectable, accessible and visible after scrolling.");
                    }
                    Microsoft.UI.Xaml.Controls.Button? FindSettingsButton(DependencyObject root,string label) {
                        if(root is Microsoft.UI.Xaml.Controls.CommandBar bar&&bar.PrimaryCommands.OfType<Microsoft.UI.Xaml.Controls.AppBarButton>().FirstOrDefault(b=>b.Label==label) is { } action) return action;
                        if(root is Microsoft.UI.Xaml.Controls.Button button&&button.Content?.ToString()==label) return button;
                        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++)
                            if(FindSettingsButton(VisualTreeHelper.GetChild(root,index),label) is { } found) return found;
                        return null;
                    }
                    if(FindSettingsButton(PageContent,"Save settings") is not null)
                        throw new InvalidOperationException("Settings still require Save.");
                    if(FindSettingsButton(PageContent,"Test connection") is null)
                        throw new InvalidOperationException("Selected-connection test action missing.");
                    if(FindSettingsButton(PageContent,"Configure") is null||FindSettingsButton(PageContent,"Remove") is null)
                        throw new InvalidOperationException("Connection configuration/removal actions missing.");
                    bool HasInlineConfiguration(DependencyObject root) {
                        if(root is Microsoft.UI.Xaml.Controls.TextBox box&&box.Header?.ToString() is "API base URL" or "Model") return true;
                        for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++) if(HasInlineConfiguration(VisualTreeHelper.GetChild(root,index))) return true;
                        return false;
                    }
                    if(HasInlineConfiguration(PageContent)) throw new InvalidOperationException("Provider fields remain inline in the connection list.");
                    if(args[5] is "settings-connection" or "settings-connection-api" or "settings-connection-chatgpt") {
                        if(args[5] is "settings-connection-api" or "settings-connection-chatgpt") {
                            Microsoft.UI.Xaml.Controls.ListView? FindConnections(DependencyObject root) {
                                if(root is Microsoft.UI.Xaml.Controls.ListView found) return found;
                                for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++) if(FindConnections(VisualTreeHelper.GetChild(root,index)) is { } nested) return nested;
                                return null;
                            }
                            FindConnections(PageContent)!.SelectedIndex=args[5]=="settings-connection-chatgpt"?1:2;
                        }
                        var configure=(Microsoft.UI.Xaml.Controls.AppBarButton)FindSettingsButton(PageContent,"Configure")!;
                        var peer=new Microsoft.UI.Xaml.Automation.Peers.AppBarButtonAutomationPeer(configure);
                        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
                        await Task.Delay(400); WindowRoot.UpdateLayout();
                        if(connectionConfigurationDialog is null) throw new InvalidOperationException("Configure did not open the native dialog.");
                        if(connectionConfigurationDialog.ActualTheme!=WindowRoot.ActualTheme) throw new InvalidOperationException("Connection dialog does not follow the selected app theme.");
                        if(args[5]!="settings-connection-chatgpt"&&!HasInlineConfiguration(connectionConfigurationDialog)) throw new InvalidOperationException("Configuration dialog is missing provider fields.");
                        if(args[5]=="settings-connection-chatgpt"&&connectionConfigurationDialog.Content is Microsoft.UI.Xaml.Controls.ScrollViewer modelViewport&&modelViewport.Content is Microsoft.UI.Xaml.Controls.StackPanel modelEditor) {
                            var accountPanel=modelEditor.Children[0]; modelEditor.Children.RemoveAt(0); await Task.Delay(50); modelEditor.Children.Insert(0,accountPanel);
                            await Task.Delay(1100); WindowRoot.UpdateLayout();
                            Microsoft.UI.Xaml.Controls.ComboBox? FindModels(DependencyObject root) {
                                if(root is Microsoft.UI.Xaml.Controls.ComboBox combo&&combo.Header?.ToString()=="Models available to this account") return combo;
                                for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++) if(FindModels(VisualTreeHelper.GetChild(root,index)) is { } nested) return nested;
                                return null;
                            }
                            var modelPicker=FindModels(modelEditor);
                            if(modelPicker is not { IsEnabled:true,Items.Count:2,SelectedItem:not null }) throw new InvalidOperationException("Model discovery did not recover after dialog reattachment.");
                        }
                        if(connectionConfigurationDialog.Content is Microsoft.UI.Xaml.Controls.ScrollViewer viewport) {
                            void AssertFits(DependencyObject root) {
                                if(root is Microsoft.UI.Xaml.Controls.Button or Microsoft.UI.Xaml.Controls.ComboBox or Microsoft.UI.Xaml.Controls.TextBox) {
                                    var control=(FrameworkElement)root; var point=control.TransformToVisual(viewport).TransformPoint(new global::Windows.Foundation.Point());
                                    if(control.Visibility==Visibility.Visible&&control.ActualWidth>0&&(point.X< -1||point.X+control.ActualWidth>viewport.ActualWidth+1))
                                        throw new InvalidOperationException("Connection dialog control exceeds its viewport: "+control.GetType().Name);
                                }
                                for(var index=0;index<VisualTreeHelper.GetChildrenCount(root);index++) AssertFits(VisualTreeHelper.GetChild(root,index));
                            }
                            AssertFits((DependencyObject)viewport.Content);
                        }
                    }
                    if(args[5] is "settings-admin" or "settings-tray"&&PageContent.Children.FirstOrDefault() is Microsoft.UI.Xaml.Controls.ScrollViewer settingsScroll&&settingsScroll.Content is Microsoft.UI.Xaml.Controls.StackPanel sections) {
                        foreach(var expander in sections.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>()) expander.IsExpanded=false;
                        var chosen=sections.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().ElementAt(args[5]=="settings-tray"?0:5);
                        chosen.IsExpanded=true; await Task.Delay(350); WindowRoot.UpdateLayout();
                        if(args[5]=="settings-admin") settingsScroll.ChangeView(null,settingsScroll.ScrollableHeight,null,true);
                    }
                }
                if(args.Length>5&&args[5].StartsWith("performance",StringComparison.Ordinal)) {
                    T? FindPerformance<T>(DependencyObject root,Func<T,bool> match) where T:DependencyObject {
                        if(root is T control&&match(control)) return control;
                        for(var child=0;child<VisualTreeHelper.GetChildrenCount(root);child++)
                            if(FindPerformance(VisualTreeHelper.GetChild(root,child),match) is { } found) return found;
                        return null;
                    }
                    performanceSubsystem=args[5] switch {
                        "performance-cpu"=>PerformanceSubsystem.Cpu,"performance-memory"=>PerformanceSubsystem.Memory,
                        "performance-disks"=>PerformanceSubsystem.Disks,"performance-network"=>PerformanceSubsystem.Network,
                        "performance-gpu"=>PerformanceSubsystem.Gpu,"performance-log-time"=>PerformanceSubsystem.Network,_=>PerformanceSubsystem.Overview };
                    for(var index=0;index<(args[5]=="performance-empty"?0:60);index++) {
                        double Value(double baseline)=>Math.Clamp(baseline+Math.Sin(index*.3)*12,0,100);
                        var missing=Observation<double>.Missing("Synthetic unavailable counter");
                        var gpu=args[5]=="performance-unavailable"?missing:Observation<double>.Available(Value(26));
                        performanceHistory.Add(new(now.AddSeconds(index-60),Observation<double>.Available(Value(34)),Observation<double>.Available(62.5),
                            Observation<double>.Available(Value(18)),Observation<double>.Available(24000000+index*50000),Observation<double>.Available(3500000+index*10000),gpu,
                            32L*1073741824,12L*1073741824,16,Observation<double>.Available(20000000),Observation<double>.Available(4000000+index*50000),
                            Observation<double>.Available(400000),Observation<double>.Available(3100000+index*10000),"Synthetic 16-thread processor"));
                    }
                    Navigation.SelectedItem=Navigation.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First(i=>i.Tag?.ToString()=="Performance");
                    page="Performance"; RenderPage(); await Task.Delay(300); WindowRoot.UpdateLayout();
                    var sections=FindPerformance<Microsoft.UI.Xaml.Controls.ListView>(PageContent,v=>v.Name=="PerformanceSubsystems");
                    if(sections?.Items.Count!=6) throw new InvalidOperationException("Performance navigation must include overview and five subsystems.");
                    if(performanceSubsystem==PerformanceSubsystem.Overview&&FindPerformance<Microsoft.UI.Xaml.Controls.Grid>(PageContent,g=>g.Name=="PerformanceOverviewTiles")?.Children.Count!=5)
                        throw new InvalidOperationException("Performance overview must have five tiles.");
                    if(args[5]=="performance-navigation") {
                        var originalSidebar=sections!;
                        var originalItems=originalSidebar.Items.Cast<object>().ToArray();
                        var originalPicker=FindPerformance<Microsoft.UI.Xaml.Controls.ComboBox>(PageContent,c=>c.Name=="PerformanceCompactPicker");
                        sections.SelectedIndex=5; await Task.Delay(100);
                        if(!ReferenceEquals(originalSidebar,FindPerformance<Microsoft.UI.Xaml.Controls.ListView>(PageContent,v=>v.Name=="PerformanceSubsystems")))
                            throw new InvalidOperationException("Subsystem navigation rebuilt the sidebar and replays its entrance animation.");
                        if(performanceSubsystem!=PerformanceSubsystem.Gpu||FindPerformance<Microsoft.UI.Xaml.Controls.Grid>(PageContent,g=>g.Name=="PerformanceDetailReadings") is null)
                            throw new InvalidOperationException("GPU detail navigation failed.");
                        sections=FindPerformance<Microsoft.UI.Xaml.Controls.ListView>(PageContent,v=>v.Name=="PerformanceSubsystems");
                        sections!.SelectedIndex=0; await Task.Delay(100);
                        if(performanceSubsystem!=PerformanceSubsystem.Overview) throw new InvalidOperationException("Overview navigation failed.");
                        foreach(var section in Enum.GetValues<PerformanceSubsystem>()) {
                            originalSidebar.SelectedIndex=(int)section; await Task.Delay(40);
                            WindowRoot.UpdateLayout();
                            if(!ReferenceEquals(originalSidebar,FindPerformance<Microsoft.UI.Xaml.Controls.ListView>(PageContent,v=>v.Name=="PerformanceSubsystems"))||
                                !originalItems.SequenceEqual(originalSidebar.Items.Cast<object>())||
                                !ReferenceEquals(originalPicker,FindPerformance<Microsoft.UI.Xaml.Controls.ComboBox>(PageContent,c=>c.Name=="PerformanceCompactPicker"))||
                                originalPicker!.SelectedIndex!=(int)section||performanceSubsystem!=section)
                                throw new InvalidOperationException("Performance navigation replaced its shell or lost selection synchronization.");
                        }
                        originalPicker!.SelectedIndex=0; await Task.Delay(40);
                        if(originalSidebar.SelectedIndex!=0||performanceSubsystem!=PerformanceSubsystem.Overview)
                            throw new InvalidOperationException("Compact navigation did not synchronize the persistent sidebar.");
                    }
                    if(args[5]=="performance-log-time") {
                        var originalSidebar=sections!;
                        var toggle=FindPerformance<Microsoft.UI.Xaml.Controls.ToggleSwitch>(PageContent,t=>t.Name=="PerformanceLogTimeAxis")
                            ??throw new InvalidOperationException("Missing logarithmic time-axis control.");
                        if(toggle.IsOn) throw new InvalidOperationException("Linear time should be the default.");
                        toggle.IsOn=true; await Task.Delay(100); WindowRoot.UpdateLayout();
                        if(!presentation.PerformanceLogTimeAxis||!ReferenceEquals(originalSidebar,FindPerformance<Microsoft.UI.Xaml.Controls.ListView>(PageContent,v=>v.Name=="PerformanceSubsystems")))
                            throw new InvalidOperationException("Time-axis setting failed or recreated the sidebar.");
                        var labels=FixtureControls<Microsoft.UI.Xaml.Controls.TextBlock>(PageContent).Select(t=>t.Text).ToArray();
                        if(!labels.Contains("1s")||!labels.Contains("3s")||!labels.Any(t=>t.StartsWith("Logarithmic time",StringComparison.Ordinal)))
                            throw new InvalidOperationException("Logarithmic time labels are missing.");
                        toggle.IsOn=false; await Task.Delay(40);
                        if(presentation.PerformanceLogTimeAxis) throw new InvalidOperationException("Linear time could not be restored.");
                        toggle.IsOn=true; await Task.Delay(60);
                    }
                }
                if(args.Length>5&&args[5].StartsWith("activity",StringComparison.Ordinal)) {
                    Navigation.SelectedItem=Navigation.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First(i=>i.Tag?.ToString()=="AI activity");
                    page="AI activity"; RenderPage(); await Task.Delay(250); WindowRoot.UpdateLayout();
                    if(args[5]=="activity-details") {
                        Microsoft.UI.Xaml.Controls.ListView? FindList(DependencyObject parent) {
                            if(parent is Microsoft.UI.Xaml.Controls.ListView found) return found;
                            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindList(VisualTreeHelper.GetChild(parent,i)) is { } nested) return nested;
                            return null;
                        }
                        var activityList=FindList(PageContent)!; activityList.SelectedIndex=2; await Task.Delay(150);
                    }
                }
                if(args.Length>5&&args[5]=="scroll") {
                    var scrolling=FindTableScrollViewer(table!) ?? throw new InvalidOperationException("Missing native scrolling viewport.");
                    if(scrolling.ScrollableWidth<=0 || scrolling.ScrollableHeight<=0) throw new InvalidOperationException("Fixture must overflow both axes.");
                    DependencyObject? FindBar(DependencyObject parent) {
                        if(parent is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar { Orientation:Microsoft.UI.Xaml.Controls.Orientation.Vertical }) return parent;
                        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                            if(FindBar(VisualTreeHelper.GetChild(parent,i)) is { } found) return found;
                        return null;
                    }
                    var bar=(FrameworkElement)(FindBar(scrolling) ?? throw new InvalidOperationException("Missing vertical scrollbar."));
                    var origin=bar.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                    if(origin.X<0 || origin.X+bar.ActualWidth>WindowRoot.ActualWidth+1) throw new InvalidOperationException("Vertical bar is outside window.");
                    scrolling.ChangeView(scrolling.ScrollableWidth/2,100,null,true); await Task.Delay(250);
                    var moved=bar.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                    if(Math.Abs(moved.X-origin.X)>1) throw new InvalidOperationException("Horizontal scroll moved vertical bar.");
                    foreach(var header in sortHeaders.Values)
                        if(header.Button.ActualHeight<49) throw new InvalidOperationException("Header button does not fill the header height.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Pid);
                    if(sortDescending) throw new InvalidOperationException("PID sort must initially ascend.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Pid);
                    if(!sortDescending) throw new InvalidOperationException("Repeated click must descend.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Name);
                    scrolling.ChangeView(0,0,null,true); await Task.Delay(150);
                    File.WriteAllText(output+".checks","Both axes overflow. Vertical bar stays within the window during horizontal scrolling. Full-height header hit targets and sort direction toggling pass.");
                }
                if(args.Length>5&&args[5]=="groups")
                {
                    var group=rows.First(r=>r.IsGroup&&r.Projection.Members.Count>1); table!.SelectedItem=group;
                    var before=rows.Count; var button=new Microsoft.UI.Xaml.Controls.Button { Tag=group.Key };
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count<=before) throw new InvalidOperationException("Group did not expand.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count!=before) throw new InvalidOperationException("Selected group did not collapse.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    var child=rows.First(r=>!r.IsGroup&&r.Projection.Depth>0); table.SelectedItem=child;
                    var originalSnapshot=lastSnapshot; var originalEntities=entities;
                    var originalAnalysis=analysis.ToDictionary(p=>p.Key,p=>p.Value);
                    var siblings=group.Projection.Members.Select(m=>m.Process!.Key.Pid).ToHashSet();
                    lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Where(p=>!siblings.Contains(p.Key.Pid)||p.Key==child.Entity.Process!.Key).ToList() };
                    entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows();
                    if(table.SelectedItem is not ViewModels.ProcessRow { IsGroup:false } single ||
                        single.Pid!=child.Entity.Process!.Key.Pid.ToString() || single.Projection.HasChildren || single.Name.EndsWith("(1)"))
                        throw new InvalidOperationException("Singleton must be a selected process with PID and no group header.");
                    lastSnapshot=originalSnapshot; entities=originalEntities;
                    foreach(var pair in originalAnalysis) analysis[pair.Key]=pair.Value;
                    RefreshRows();
                    File.WriteAllText(output+".checks","Group expands/collapses without reselection; selected child survives as a standalone process with PID and no group header when siblings exit.");
                    await Task.Delay(250); WindowRoot.UpdateLayout();
                }
                if(args.Length>5&&args[5].StartsWith("details",StringComparison.Ordinal))
                { detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
                if(args.Length>5&&args[5] is "details-selection" or "details-service-selection") {
                    if(args[5]=="details-service-selection") {
                        page="Services"; RenderPage(); await Task.Delay(200); detailsOpen=true; UpdateDetailsLayout();
                    }
                    var first=rows.First(r=>!r.IsGroup); var second=rows.First(r=>!r.IsGroup&&r.Key!=first.Key);
                    table!.SelectedItem=first; ShowDetail(first); await Task.Delay(100);
                    Microsoft.UI.Xaml.Controls.Expander Section(string key)=>detailPanel!.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string==key);
                    Section("command").IsExpanded=true; Section("assessment-Purpose").IsExpanded=true;
                    table.SelectedItem=second; await Task.Delay(150);
                    if(!Section("command").IsExpanded||!Section("assessment-Purpose").IsExpanded) throw new InvalidOperationException("Selection reset expanded details.");
                    var selectedSummary=second.Entity.Process is { } selectedProcess?$"PID {selectedProcess.Key.Pid} · {selectedProcess.Executable.Name}":$"Service {second.Entity.Service!.Name} · {second.Entity.Service.State}";
                    if(!detailPanel!.Children.OfType<Microsoft.UI.Xaml.Controls.TextBlock>().Any(t=>t.Text==selectedSummary)) throw new InvalidOperationException("Selection preserved stale item metadata.");
                    Section("command").IsExpanded=false;
                    table.SelectedItem=first; await Task.Delay(150);
                    if(Section("command").IsExpanded||!Section("assessment-Purpose").IsExpanded) throw new InvalidOperationException("Selection reset section choices.");
                    // Empty selection and page recreation must not discard preferences for temporarily absent sections.
                    table.SelectedItem=null; ShowDetail(null); RenderPage(); await Task.Delay(150);
                    detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Key==first.Key); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); await Task.Delay(150);
                    if(Section("command").IsExpanded||!Section("assessment-Purpose").IsExpanded) throw new InvalidOperationException("Page recreation reset detail choices.");
                    File.WriteAllText(output+".checks","Selection, empty selection and page recreation retain expanded/collapsed detail choices; selected data is rebuilt.");
                }
                if(args.Length>5&&args[5] is "details-stability" or "details-command") {
                    var commandSection=detailPanel!.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="command");
                    commandSection.IsExpanded=true; await Task.Delay(350);
                    detailScroll!.ChangeView(null,args[5]=="details-stability"?200:450,null,true); await Task.Delay(100);
                    if(args[5]=="details-stability") {
                        var originalPanel=commandSection; var originalOffset=detailScroll.VerticalOffset;
                        lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Select(p=>p with { Resources=p.Resources with { CpuPercent=Observation<double>.Available(8) } }).ToList() };
                        entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows(); await Task.Delay(100);
                        if(!ReferenceEquals(originalPanel,detailPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="command"))||!originalPanel.IsExpanded||Math.Abs(detailScroll.VerticalOffset-originalOffset)>1)
                            throw new InvalidOperationException("Resource refresh disturbed details.");
                        lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Select(p=>p.Key==node.Process!.Key?p with { Executable=p.Executable with { Version="2.0",Signature=SignatureStatus.Verified,VerifiedSigner="Fixture signer" } }:p).ToList() };
                        entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows(); await Task.Delay(150);
                        if(!detailPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="command").IsExpanded||Math.Abs(detailScroll.VerticalOffset-originalOffset)>1)
                            throw new InvalidOperationException("Metadata refresh lost detail state.");
                    }
                }
                if(args.Length>5&&args[5]=="details-evidence") {
                    detailsOpen=false; UpdateDetailsLayout(); await Task.Delay(150);
                    detailsOpen=true; UpdateDetailsLayout(); ShowDetail(table!.SelectedItem as ViewModels.ProcessRow); await Task.Delay(150);
                    var outer=detailPanel!.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="evidence");
                    outer.IsExpanded=true;
                    var record=evidencePanel!.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().First(e=>(e.Tag as string)?.StartsWith("record-")==true);
                    record.IsExpanded=true; var recordKey=(string)record.Tag;
                    await Task.Delay(350); detailScroll!.ChangeView(null,800,null,true); await Task.Delay(100);
                    var before=detailScroll.VerticalOffset;
                    lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Select(p=>p.Key==node.Process!.Key?p with { Executable=p.Executable with { Version="3.0" } }:p).ToList() };
                    entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows(); await Task.Delay(450);
                    if(!evidencePanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string==recordKey).IsExpanded||Math.Abs(detailScroll.VerticalOffset-before)>1)
                        throw new InvalidOperationException("Evidence refresh lost nested expansion or scroll.");
                }
                if(args.Length>5&&args[5]=="layout") {
                    RefreshRows(); await Task.Delay(150);
                    T? FindLayoutControl<T>(DependencyObject parent,Func<T,bool> predicate) where T:DependencyObject {
                        if(parent is T match&&predicate(match)) return match;
                        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindLayoutControl(VisualTreeHelper.GetChild(parent,i),predicate) is { } nested) return nested;
                        return null;
                    }
                    void CheckAlignment() {
                        var container=(Microsoft.UI.Xaml.Controls.ListViewItem)table!.ContainerFromIndex(0);
                        var rowGrid=FindLayoutControl<Microsoft.UI.Xaml.Controls.Grid>(container,g=>g.Height==28&&g.ColumnDefinitions.Count==tableColumnLayout.Length+1)??throw new InvalidOperationException("Missing realized process row.");
                        var start=rowGrid.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                        var headerStart=sortHeaders[ProcSight.Application.Presentation.ProcessSort.Name].Button.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                        if(Math.Abs(start.X-headerStart.X)>1) throw new InvalidOperationException("Name header offset from rows: "+(headerStart.X-start.X));
                        foreach(var cell in rowGrid.Children.OfType<Microsoft.UI.Xaml.Controls.Border>()) {
                            var col=Microsoft.UI.Xaml.Controls.Grid.GetColumn(cell);
                            var sortColumn=col switch {1=>ProcSight.Application.Presentation.ProcessSort.Assessment,2=>ProcSight.Application.Presentation.ProcessSort.Pid,3=>ProcSight.Application.Presentation.ProcessSort.Cpu,4=>ProcSight.Application.Presentation.ProcessSort.Memory,5=>ProcSight.Application.Presentation.ProcessSort.Disk,_=>ProcSight.Application.Presentation.ProcessSort.Network};
                            var cellStart=cell.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                            var button=sortHeaders[sortColumn].Button; var buttonStart=button.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                            if(Math.Abs(cellStart.X-buttonStart.X)>1||Math.Abs(cell.ActualWidth-button.ActualWidth)>1) throw new InvalidOperationException("Header width/alignment differs for "+sortColumn);
                        }
                        var bar=FindLayoutControl<Microsoft.UI.Xaml.Controls.CommandBar>(PageContent,b=>b.Content is Microsoft.UI.Xaml.Controls.TextBlock)!;
                        var heading=(Microsoft.UI.Xaml.Controls.TextBlock)bar.Content;
                        var titleY=heading.TransformToVisual(WindowRoot).TransformPoint(new(0,0)).Y+heading.ActualHeight/2;
                        var actionY=analyzeButton!.TransformToVisual(WindowRoot).TransformPoint(new(0,0)).Y+analyzeButton.ActualHeight/2;
                        if(Math.Abs(titleY-actionY)>2) throw new InvalidOperationException("Page title and toolbar differ vertically: "+(titleY-actionY));
                    }
                    CheckAlignment();
                    var layoutScale=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))/96.0;
                    AppWindow.Resize(new((int)(700*layoutScale),(int)(780*layoutScale))); await Task.Delay(200);
                    var layoutScroll=FindTableScrollViewer(table!)!; layoutScroll.ChangeView(layoutScroll.ScrollableWidth/2,null,null,true); await Task.Delay(150); CheckAlignment();
                    AppWindow.Resize(new((int)(1100*layoutScale),(int)(780*layoutScale))); await Task.Delay(200); CheckAlignment();
                }
                if(args.Length>5&&args[5]=="details-restricted") {
                    var commandSection=detailPanel!.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="command");
                    commandSection.IsExpanded=true; var initialIdentity=node.Identity;
                    for(var tick=0;tick<3;tick++) {
                        lastSnapshot=lastSnapshot with { Id=Guid.NewGuid(),Processes=lastSnapshot.Processes.Select(p=>p.Key.Pid==1002?p with { Key=fixtureLifetime.Resolve(1002,null,77,Guid.NewGuid()) }:p).ToList() };
                        entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows(); await Task.Delay(80);
                        var selected=table!.SelectedItem as ViewModels.ProcessRow;
                        if(selected?.Entity.Identity!=initialIdentity||!ReferenceEquals(commandSection,detailPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().Single(e=>e.Tag as string=="command"))||!commandSection.IsExpanded)
                            throw new InvalidOperationException("Restricted process inspection reset during collection.");
                    }
                }
                if(args.Length>5&&args[5]=="services") {
                    if(rows.Count!=4||rows.Any(r=>r.Entity.Service is null)) throw new InvalidOperationException("Services fixture did not select the service page.");
                    for(var attempt=0;attempt<20&&rows.Count(r=>r.Icon is not null)<2;attempt++) await Task.Delay(100);
                    if(rows.Count(r=>r.Icon is not null)!=2||rows.Single(r=>r.Entity.Service!.Name=="SyntheticFallback").Icon is not null) throw new InvalidOperationException("Service icon/fallback selection failed.");
                    WindowRoot.UpdateLayout();
                }
                var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(connectionConfigurationDialog is not null?connectionConfigurationDialog:tableConfigurationDialog is not null?tableConfigurationDialog:WindowRoot);
                var pixels=await bitmap.GetPixelsAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var folder=await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(output)!);
                var file=await folder.CreateFileAsync(Path.GetFileName(output),CreationCollisionOption.ReplaceExisting);
                using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());
                await encoder.FlushAsync();
            }
            catch(Exception e) { File.WriteAllText(output+".error",e.GetType().Name+": "+e.Message); }
            finally {
                tableConfigurationDialog?.Hide();
                Close();
                if(fixtureRulesDatabase is { } database) {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    foreach(var file in new[]{database,database+"-wal",database+"-shm"}) if(File.Exists(file)) File.Delete(file);
                }
            }
        };
        Activate(); return true;
    }
    private sealed class FixtureCredentials : ProcSight.Application.Settings.ICredentialStore
    {
        private readonly Dictionary<string,string> values=[];
        public string? Get(string id)=>values.GetValueOrDefault(id);
        public void Set(string id,string value)=>values[id]=value;
        public void Remove(string id)=>values.Remove(id);
    }
    private sealed class FixtureModelHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(request.RequestUri?.AbsolutePath!="/v1/models") throw new InvalidOperationException("Unexpected fixture request.");
            await Task.Delay(700,ct);
            return new(System.Net.HttpStatusCode.OK) { Content=new StringContent("""{"models":[{"slug":"Example account model","display_name":"Example account model","visibility":"list"},{"slug":"Alternative example model","display_name":"Alternative example model","visibility":"list"}]}""") };
        }
    }
}
#endif
