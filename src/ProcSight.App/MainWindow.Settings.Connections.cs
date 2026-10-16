using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private FrameworkElement BuildConnectionSettings()
    {
        var profiles=(policy.Connections??ConnectionRouter.Resolve(policy).ToArray()).ToList();
        var panel=new StackPanel { Spacing=12 };
        var feedback=new TextBlock { Text="Enable connections and order them by priority. Local rules are always checked first.",TextWrapping=TextWrapping.Wrap };
        var list=new ListView { SelectionMode=ListViewSelectionMode.Single,MaxHeight=240,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        var editor=new StackPanel { Spacing=12 }; var commands=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        var up=new Button { Content="Move up" }; var down=new Button { Content="Move down" }; var test=new Button { Content="Test connection" };
        commands.Children.Add(up); commands.Children.Add(down); commands.Children.Add(test);
        var kind=new ComboBox { ItemsSource=new[]{"ChatGPT plan","OpenAI API","Compatible API","Local model"},SelectedIndex=0,MinWidth=180 };
        var add=new Button { Content="Add connection" }; var addRow=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        addRow.Children.Add(kind); addRow.Children.Add(add);
        var fallback=new ToggleSwitch { Header="Try the next enabled connection if unavailable",IsOn=policy.FallbackEnabled };
        panel.Children.Add(feedback); panel.Children.Add(list); panel.Children.Add(commands); panel.Children.Add(addRow); panel.Children.Add(fallback);
        panel.Children.Add(new TextBlock { Text="Fallback is optional and may use separately billed API access. Disabled connections are never used automatically. Tests send synthetic metadata only.",TextWrapping=TextWrapping.Wrap,FontSize=12 });
        panel.Children.Add(editor);
        string? selectedId=profiles.FirstOrDefault()?.ProfileId; bool rebuilding=false; bool validEditor=true; Func<Task>? flush=null;
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
                var title=new TextBlock { Text=profile.Label,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center };
                labels[profile.ProfileId]=title;
                Grid.SetColumn(title,1); row.Children.Add(enabled); row.Children.Add(title);
                var item=new ListViewItem { Content=row,Tag=profile.ProfileId,HorizontalContentAlignment=HorizontalAlignment.Stretch }; list.Items.Add(item);
                if(profile.ProfileId==selectedId) list.SelectedItem=item;
            }
            rebuilding=false; up.IsEnabled=list.SelectedIndex>0; down.IsEnabled=list.SelectedIndex>=0&&list.SelectedIndex<profiles.Count-1;
        }
        void Edit() {
            flush=null; editor.Children.Clear(); validEditor=true;
            var profile=profiles.FirstOrDefault(p=>p.ProfileId==selectedId); test.IsEnabled=profile is not null;
            if(profile is null) return;
            var model=new TextBox { Header="Model",Text=profile.Provider.Model };
            var endpoint=new TextBox { Header="API base URL",Text=profile.Provider.Endpoint.ToString(),IsReadOnly=profile.Provider.Id=="chatgpt" };
            var strict=new CheckBox { Content="Supports schema-constrained JSON",IsChecked=profile.Provider.StructuredOutput };
            var key=new PasswordBox { Header="API key",PlaceholderText="Enter to replace the protected key; Enter or leave this field to apply" };
            var warning=new TextBlock { TextWrapping=TextWrapping.Wrap };
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) }; var closed=false; var editing=false;
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
            void Schedule() { validEditor=false; timer.Stop(); timer.Start(); }
            timer.Tick+=async(_,_)=>await ApplyDraft();
            model.TextChanged+=(_,_)=>Schedule(); endpoint.TextChanged+=(_,_)=>Schedule();
            model.LostFocus+=async(_,_)=>await ApplyDraft(); endpoint.LostFocus+=async(_,_)=>await ApplyDraft();
            strict.Click+=async(_,_)=>await ApplyDraft(); flush=ApplyDraft;
            async Task ApplyKey() {
                if(key.Password.Length==0) return;
                await ApplyDraft(); if(!validEditor) return;
                try { var credentialId="connection-"+Guid.NewGuid().ToString("N");
                    credentials.Set(credentialId,key.Password); key.Password="";
                    var i=profiles.FindIndex(p=>p.ProfileId==profile.ProfileId);
                    profile=profile with { Provider=profile.Provider with { CredentialId=credentialId } }; profiles[i]=profile; await Commit();
                } catch { warning.Text="The key could not be stored in Windows-protected credentials."; }
            }
            key.LostFocus+=async(_,_)=>await ApplyKey(); key.KeyDown+=async(_,e)=> { if(e.Key==global::Windows.System.VirtualKey.Enter) { e.Handled=true; await ApplyKey(); } };
            if(profile.Provider.Id=="chatgpt") {
                editing=true;
                var dummy=new ComboBox { SelectedIndex=-1 }; dummy.Items.Add("API"); dummy.Items.Add("Compatible"); dummy.Items.Add("ChatGPT"); dummy.SelectedIndex=2;
                editor.Children.Add(BuildChatGptSettings(dummy,model,()=> { profiles.Clear(); profiles.AddRange(policy.Connections??[]); RefreshList(); },profile.Provider)); editing=false;
            } else { editor.Children.Add(endpoint); editor.Children.Add(model); editor.Children.Add(strict); if(!profile.IsLocal) editor.Children.Add(key); }
            if(profile.Provider.Id=="openai") {
                var research=new CheckBox { Content="Use this connection for explicitly enabled online research",IsChecked=policy.ResearchProfileId==profile.ProfileId };
                research.Click+=async(_,_)=> { var result=await ApplySettingsAsync(policy with { ResearchProfileId=research.IsChecked==true?profile.ProfileId:null }); feedback.Text=SettingsResultText(result); };
                editor.Children.Add(research);
            }
            var removeKey=new Button { Content="Remove this connection’s API key",IsEnabled=profile.Provider.Id is "openai" or "compatible" };
            removeKey.Click+=(_,_)=> { try { if(profile.Provider.CredentialId is { } id) credentials.Remove(id); warning.Text="Protected key removed."; } catch { warning.Text="Key removal failed."; } };
            editor.Children.Add(removeKey); editor.Children.Add(warning);
            warning.Unloaded+=OnUnloaded;
            void OnUnloaded(object sender,RoutedEventArgs e) { warning.Unloaded-=OnUnloaded; closed=true; if(timer.IsEnabled) _=ApplyDraft(); timer.Stop(); }
        }
        list.SelectionChanged+=async(_,_)=> { if(rebuilding) return; var id=(list.SelectedItem as ListViewItem)?.Tag as string; if(flush is not null) await flush(); selectedId=id; Edit(); up.IsEnabled=list.SelectedIndex>0; down.IsEnabled=list.SelectedIndex>=0&&list.SelectedIndex<profiles.Count-1; };
        async Task Move(int delta) {
            if(flush is not null) await flush(); if(!validEditor) return;
            var i=profiles.FindIndex(p=>p.ProfileId==selectedId); var j=i+delta; if(i<0||j<0||j>=profiles.Count) return;
            (profiles[i],profiles[j])=(profiles[j],profiles[i]); for(var n=0;n<profiles.Count;n++) profiles[n]=profiles[n] with { Priority=n };
            await Commit(); RefreshList();
        }
        up.Click+=async(_,_)=>await Move(-1); down.Click+=async(_,_)=>await Move(1);
        fallback.Toggled+=async(_,_)=> { feedback.Text=SettingsResultText(await ApplySettingsAsync(policy with { FallbackEnabled=fallback.IsOn })); };
        add.Click+=(_,_)=> {
            if(profiles.Count>=32) { feedback.Text="At most 32 connections are supported."; return; }
            var providerKind=kind.SelectedIndex switch { 0=>"chatgpt",1=>"openai",2=>"compatible",_=>"local" };
            var profile=new ConnectionProfile(Guid.NewGuid().ToString("N"),new(providerKind,new(providerKind=="local"?"http://127.0.0.1:1234/v1":"https://api.openai.com/v1"),""),false,profiles.Count);
            profiles.Add(profile); selectedId=profile.ProfileId; RefreshList(); Edit();
        };
        test.Click+=async(_,_)=> {
            if(flush is not null) await flush(); if(!validEditor) { feedback.Text="Complete the selected configuration before testing."; return; }
            var profile=profiles.FirstOrDefault(p=>p.ProfileId==selectedId); if(profile is null) return;
            test.IsEnabled=false;
            try { var result=await new ConnectionTester(CreateProvider,usage,aiActivity).TestAsync(profile,EffectivePolicy,lifetime.Token);
                feedback.Text=result.Error?.Message??"Connection returned a validated response. No real process metadata was sent."; }
            catch(OperationCanceledException) { feedback.Text="Connection test cancelled."; }
            catch { feedback.Text="Connection test unavailable. Check configuration and credential access."; }
            finally { test.IsEnabled=true; }
        };
        RefreshList(); Edit();
        var expander=SettingsExpander("\uE774","Connections and priority","Configure, enable, order and test AI connections",panel);
        expander.IsExpanded=true; return expander;
    }
}
