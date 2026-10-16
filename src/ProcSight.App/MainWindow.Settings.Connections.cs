using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private CancellationTokenSource? connectionTestCancellation;
    private ContentDialog? connectionConfigurationDialog;
    private void CancelConnectionTests() { connectionTestCancellation?.Cancel(); }
    private FrameworkElement BuildConnectionSettings()
    {
        var profiles=(policy.Connections??ConnectionRouter.Resolve(policy).ToArray()).ToList();
        var panel=new StackPanel { Spacing=12 };
        var feedback=SettingsFeedback();
        var list=new ListView { SelectionMode=ListViewSelectionMode.Single,MaxHeight=240,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(list,"AI connections in priority order");
        var editor=new StackPanel { Spacing=16 };
        var commands=new CommandBar { DefaultLabelPosition=CommandBarDefaultLabelPosition.Right,HorizontalAlignment=HorizontalAlignment.Stretch };
        var configure=new AppBarButton { Label="Configure",Icon=new SymbolIcon(Symbol.Setting) };
        var add=new AppBarButton { Label="Add",Icon=new SymbolIcon(Symbol.Add) };
        var test=new AppBarButton { Label="Test connection",Icon=new SymbolIcon(Symbol.Play) };
        var up=new AppBarButton { Label="Move up",Icon=new FontIcon { Glyph="\uE70E" } };
        var down=new AppBarButton { Label="Move down",Icon=new FontIcon { Glyph="\uE70D" } };
        var remove=new AppBarButton { Label="Remove",Icon=new SymbolIcon(Symbol.Delete) };
        commands.PrimaryCommands.Add(add); commands.PrimaryCommands.Add(configure); commands.PrimaryCommands.Add(test);
        commands.PrimaryCommands.Add(up); commands.PrimaryCommands.Add(down); commands.PrimaryCommands.Add(remove);
        var kinds=new MenuFlyout(); add.Flyout=kinds;
        var fallback=new ToggleSwitch { IsOn=policy.FallbackEnabled };
        panel.Children.Add(commands); panel.Children.Add(list);
        panel.Children.Add(SettingsCard("\uE8AB","Automatic fallback","Try the next enabled connection if unavailable. API billing may apply.",fallback));
        panel.Children.Add(feedback);
        string? selectedId=profiles.FirstOrDefault()?.ProfileId; bool rebuilding=false; bool validEditor=true; Func<Task>? flush=null; Func<string>? editorFeedback=null;
        var labels=new Dictionary<string,TextBlock>();
        async Task Commit() {
            try {
                var complete=profiles.Where(p=>!string.IsNullOrWhiteSpace(p.Provider.Model)).ToArray();
                ConnectionProfiles.Validate(complete);
                var result=await ApplySettingsAsync(policy with { Connections=complete,Provider=null,LocalModel=null });
                if(result.Failure!=ProcSight.Application.Settings.SettingsFailure.Superseded) feedback.Text=SettingsResultText(result);
            } catch(ArgumentException e) { feedback.Text=e.Message; } catch { feedback.Text="Connection changes could not be saved."; }
        }
        void RefreshList() {
            rebuilding=true; list.Items.Clear(); labels.Clear();
            foreach(var profile in profiles) {
                var row=new Grid { ColumnSpacing=12,Padding=new Thickness(8,6,8,6) };
                row.ColumnDefinitions.Add(new() { Width=GridLength.Auto }); row.ColumnDefinitions.Add(new());
                var enabled=new CheckBox { IsChecked=profile.Enabled,VerticalAlignment=VerticalAlignment.Center,MinWidth=0,Width=24 };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(enabled,"Enable "+profile.Label);
                enabled.Click+=async(_,_)=> { var i=profiles.FindIndex(p=>p.ProfileId==profile.ProfileId); profiles[i]=profiles[i] with { Enabled=enabled.IsChecked==true }; await Commit(); };
                var title=new TextBlock { Text=string.IsNullOrWhiteSpace(profile.Provider.Model)?profile.Label+"Not configured":profile.Label,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center };
                labels[profile.ProfileId]=title;
                Grid.SetColumn(title,1); row.Children.Add(enabled); row.Children.Add(title);
                var item=new ListViewItem { Content=row,Tag=profile.ProfileId,HorizontalContentAlignment=HorizontalAlignment.Stretch }; list.Items.Add(item);
                if(profile.ProfileId==selectedId) list.SelectedItem=item;
            }
            rebuilding=false; UpdateActions();
        }
        void UpdateActions() {
            var selected=profiles.FirstOrDefault(p=>p.ProfileId==selectedId);
            configure.IsEnabled=remove.IsEnabled=selected is not null;
            test.IsEnabled=selected is not null&&!string.IsNullOrWhiteSpace(selected.Provider.Model);
            var index=profiles.FindIndex(p=>p.ProfileId==selectedId);
            up.IsEnabled=index>0; down.IsEnabled=index>=0&&index<profiles.Count-1;
        }
        void Edit() {
            flush=null; editor.Children.Clear(); validEditor=true;
            var profile=profiles.FirstOrDefault(p=>p.ProfileId==selectedId); test.IsEnabled=profile is not null;
            if(profile is null) return;
            var model=new TextBox { Header="Model",Text=profile.Provider.Model };
            var endpoint=new TextBox { Header="API base URL",Text=profile.Provider.Endpoint.ToString(),IsReadOnly=profile.Provider.Id=="chatgpt" };
            var strict=new CheckBox { Content="Supports schema-constrained JSON",IsChecked=profile.Provider.StructuredOutput };
            var key=new PasswordBox { Header="API key",PlaceholderText="Enter a replacement key" };
            ToolTipService.SetToolTip(key,"Stored securely in Windows. Press Enter or leave this field to apply.");
            var warning=new TextBlock { TextWrapping=TextWrapping.Wrap };
            editorFeedback=()=>warning.Text;
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) }; var closed=false; var editing=false; var keyCommitting=false; long editVersion=0;
            Task? pendingKey=null;
            async Task ApplyDraft() {
                timer.Stop(); if(editing) return;
                try {
                    if(!Uri.TryCreate(endpoint.Text.Trim(),UriKind.Absolute,out var uri)) throw new ArgumentException("Enter a complete API URL.");
                    var changedEndpoint=uri!=profile.Provider.Endpoint;
                    var settings=profile.Provider with { Endpoint=uri,Model=model.Text.Trim(),StructuredOutput=strict.IsChecked==true,
                        AccountId=profile.Provider.Id=="chatgpt"?settingsChatGptAccountId:profile.Provider.AccountId,
                        CredentialId=changedEndpoint?null:profile.Provider.CredentialId };
                    if(settings.Id=="chatgpt"&&settings.Model!=settingsChatGptModel) throw new ArgumentException("Choose a model available to the selected ChatGPT account.");
                    var i=profiles.FindIndex(p=>p.ProfileId==profile.ProfileId); if(i<0) return;
                    var next=profiles[i] with { Provider=settings }; ConnectionProfiles.Validate([next]);
                    profiles[i]=next; profile=next; validEditor=true; if(!closed) warning.Text="";
                    if(!closed&&labels.TryGetValue(profile.ProfileId,out var label)) label.Text=profile.Label;
                    await Commit();
                } catch(ArgumentException e) { validEditor=false; if(!closed) warning.Text=e.Message+" Last valid configuration remains active."; }
                catch { validEditor=false; if(!closed) warning.Text="Connection configuration could not be applied."; }
            }
            void Schedule() { ++editVersion; validEditor=false; timer.Stop(); timer.Start(); }
            timer.Tick+=async(_,_)=>await ApplyDraft();
            model.TextChanged+=(_,_)=>Schedule(); endpoint.TextChanged+=(_,_)=>Schedule();
            model.LostFocus+=async(_,_)=>await ApplyDraft(); endpoint.LostFocus+=async(_,_)=>await ApplyDraft();
            strict.Click+=async(_,_)=>await ApplyDraft();
            Task ApplyKey()=>pendingKey is { IsCompleted:false }?pendingKey:pendingKey=ApplyKeyCore();
            async Task ApplyKeyCore() {
                if(key.Password.Length==0||keyCommitting||!Uri.TryCreate(endpoint.Text.Trim(),UriKind.Absolute,out var authorizedEndpoint)) return;
                keyCommitting=true; var secret=key.Password; key.Password=""; var generation=editVersion;
                var authorized=profile with { Provider=profile.Provider with { Endpoint=authorizedEndpoint } };
                try {
                    await ApplyDraft(); if(!validEditor) { warning.Text="Complete configuration and re-enter the key."; return; }
                    ConnectionProfile? Current()=>!closed&&generation==editVersion&&selectedId==authorized.ProfileId?profiles.FirstOrDefault(p=>p.ProfileId==authorized.ProfileId):null;
                    var applied=await ProcSight.Application.Settings.ConnectionCredentials.StoreAsync(authorized,secret,Current,credentials,async bound=> {
                        var i=profiles.FindIndex(p=>p.ProfileId==bound.ProfileId); if(i<0) return;
                        profiles[i]=bound; profile=bound; await Commit();
                    },lifetime.Token);
                    if(!applied&&!closed) warning.Text="The destination changed while the key was being stored. Re-enter the key for the current connection.";
                } catch { if(!closed) warning.Text="The key could not be stored in Windows-protected credentials."; }
                finally { keyCommitting=false; }
            }
            flush=async()=> { await ApplyDraft(); await ApplyKey(); };
            key.LostFocus+=async(_,_)=>await ApplyKey(); key.KeyDown+=async(_,e)=> { if(e.Key==global::Windows.System.VirtualKey.Enter) { e.Handled=true; await ApplyKey(); } };
            if(profile.Provider.Id=="chatgpt") {
                editing=true;
                var dummy=new ComboBox { SelectedIndex=-1 }; dummy.Items.Add("API"); dummy.Items.Add("Compatible"); dummy.Items.Add("ChatGPT"); dummy.SelectedIndex=2;
                editor.Children.Add(BuildChatGptSettings(dummy,model,()=> { profiles.Clear(); profiles.AddRange(policy.Connections??[]); RefreshList(); },profile.Provider,()=>coordinator.RetryConnection(profile.ProfileId))); editing=false;
            } else { editor.Children.Add(endpoint); editor.Children.Add(model); editor.Children.Add(strict); if(!profile.IsLocal) editor.Children.Add(key); }
            if(ConnectionRouter.SupportsResearch(profile.Provider)) {
                var research=new CheckBox { Content="Use this connection for explicitly enabled online research",IsChecked=policy.ResearchProfileId==profile.ProfileId };
                research.Click+=async(_,_)=> { var result=await ApplySettingsAsync(policy with { ResearchProfileId=research.IsChecked==true?profile.ProfileId:null }); feedback.Text=SettingsResultText(result); };
                editor.Children.Add(research);
            }
            var removeKey=new Button { Content="Remove this connection’s API key",IsEnabled=profile.Provider.Id is "openai" or "compatible" };
            removeKey.Click+=(_,_)=> { try { if(profile.Provider.CredentialId is { } id) credentials.Remove(id); warning.Text="Protected key removed."; } catch { warning.Text="Key removal failed."; } };
            if(profile.Provider.Id is "openai" or "compatible") editor.Children.Add(removeKey);
            editor.Children.Add(warning);
            warning.Unloaded+=OnUnloaded;
            void OnUnloaded(object sender,RoutedEventArgs e) { warning.Unloaded-=OnUnloaded; closed=true; if(timer.IsEnabled) _=ApplyDraft(); timer.Stop(); }
        }
        list.SelectionChanged+=(_,_)=> { if(rebuilding) return; selectedId=(list.SelectedItem as ListViewItem)?.Tag as string; UpdateActions(); };
        async Task OpenConfigurationAsync() {
            if(connectionConfigurationDialog is not null||selectedId is null) return;
            Edit();
            var dialog=new ContentDialog {
                Title="Configure connection",CloseButtonText="Close",XamlRoot=PageContent.XamlRoot,RequestedTheme=WindowRoot.ActualTheme,
                Content=new ScrollViewer { Content=editor,MaxWidth=560,MaxHeight=Math.Max(200,WindowRoot.ActualHeight-240),HorizontalContentAlignment=HorizontalAlignment.Stretch,
                    HorizontalScrollMode=ScrollMode.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }
            };
            connectionConfigurationDialog=dialog;
            dialog.Closing+=async(_,args)=> {
                var deferral=args.GetDeferral();
                try { if(flush is not null) await flush(); }
                finally { deferral.Complete(); }
            };
            try { await dialog.ShowAsync(); }
            catch(OperationCanceledException) { }
            catch { feedback.Text="The connection configuration could not be opened."; }
            finally {
                connectionConfigurationDialog=null;
                var id=selectedId;
                var remainingFeedback=editorFeedback?.Invoke(); editorFeedback=null;
                editor.Children.Clear(); flush=null;
                if(id is not null&&policy.Connections?.Any(p=>p.ProfileId==id)!=true) {
                    profiles.RemoveAll(p=>p.ProfileId==id); selectedId=profiles.FirstOrDefault()?.ProfileId;
                }
                if(!string.IsNullOrWhiteSpace(remainingFeedback)) feedback.Text=remainingFeedback;
                else if(!validEditor) feedback.Text="Incomplete edits were not applied. The last valid configuration remains active.";
                validEditor=true;
                RefreshList();
                (configure.IsEnabled?configure:add).Focus(FocusState.Programmatic);
            }
        }
        configure.Click+=async(_,_)=>await OpenConfigurationAsync();
        list.DoubleTapped+=async(_,_)=>await OpenConfigurationAsync();
        async Task Move(int delta) {
            if(flush is not null) await flush(); if(!validEditor) return;
            var i=profiles.FindIndex(p=>p.ProfileId==selectedId); var j=i+delta; if(i<0||j<0||j>=profiles.Count) return;
            (profiles[i],profiles[j])=(profiles[j],profiles[i]); for(var n=0;n<profiles.Count;n++) profiles[n]=profiles[n] with { Priority=n };
            await Commit(); RefreshList();
        }
        up.Click+=async(_,_)=>await Move(-1); down.Click+=async(_,_)=>await Move(1);
        fallback.Toggled+=async(_,_)=> { feedback.Text=SettingsResultText(await ApplySettingsAsync(policy with { FallbackEnabled=fallback.IsOn })); };
        foreach(var (label,providerKind) in new[]{("ChatGPT account","chatgpt"),("OpenAI API","openai"),("Compatible API","compatible"),("Local model","local")}) {
            var item=new MenuFlyoutItem { Text=label };
            item.Click+=async(_,_)=> {
                if(profiles.Count>=32) { feedback.Text="At most 32 connections are supported."; return; }
                var profile=new ConnectionProfile(Guid.NewGuid().ToString("N"),new(providerKind,new(providerKind=="local"?"http://127.0.0.1:1234/v1":"https://api.openai.com/v1"),""),false,profiles.Count);
                profiles.Add(profile); selectedId=profile.ProfileId; RefreshList(); await OpenConfigurationAsync();
            };
            kinds.Items.Add(item);
        }
        remove.Click+=async(_,_)=> {
            var profile=profiles.FirstOrDefault(p=>p.ProfileId==selectedId); if(profile is null) return;
            var dialog=new ContentDialog { Title="Remove connection?",Content=$"Remove {profile.Label} from the priority list? Shared ChatGPT accounts remain signed in.",
                PrimaryButtonText="Remove",CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close,XamlRoot=PageContent.XamlRoot,RequestedTheme=WindowRoot.ActualTheme };
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
            var index=profiles.FindIndex(p=>p.ProfileId==profile.ProfileId);
            var next=ProcSight.Application.Settings.ConnectionProfileActions.Remove(policy,profile.ProfileId);
            try {
                var result=await ApplySettingsAsync(next);
                profiles.RemoveAll(p=>p.ProfileId==profile.ProfileId);
                for(var i=0;i<profiles.Count;i++) profiles[i]=profiles[i] with { Priority=i };
                selectedId=profiles.ElementAtOrDefault(Math.Min(index,profiles.Count-1))?.ProfileId;
                feedback.Text=SettingsResultText(result); RefreshList();
            } catch { feedback.Text="The connection could not be removed. Check settings storage access."; }
        };
        test.Click+=async(_,_)=> {
            if(flush is not null) await flush(); if(!validEditor) { feedback.Text="Complete the selected configuration before testing."; return; }
            var profile=profiles.FirstOrDefault(p=>p.ProfileId==selectedId); if(profile is null) return;
            test.IsEnabled=false;
            using var testing=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); connectionTestCancellation=testing;
            try { var result=await new ConnectionTester(CreateProvider,usage,aiActivity,()=>EffectivePolicy).TestAsync(profile,EffectivePolicy,testing.Token);
                if(result.Error is null) coordinator.RetryConnection(profile.ProfileId);
                feedback.Text=result.Error?.Message??"Connection returned a validated response. No real process metadata was sent."; }
            catch(OperationCanceledException) { feedback.Text="Connection test cancelled."; }
            catch { feedback.Text="Connection test unavailable. Check configuration and credential access."; }
            finally { if(ReferenceEquals(connectionTestCancellation,testing)) connectionTestCancellation=null; test.IsEnabled=true; }
        };
        RefreshList();
        var expander=SettingsExpander("\uE774","Connections and priority","Configure, enable, order and test AI connections",panel);
        expander.IsExpanded=true; return expander;
    }
}
