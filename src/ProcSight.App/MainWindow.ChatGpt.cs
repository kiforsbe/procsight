using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Infrastructure.Authentication;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private string? settingsChatGptAccountId;
    private string? settingsChatGptModel;
    private FrameworkElement BuildChatGptSettings(ComboBox provider,TextBox model,Action signedOut)
    {
        var accounts=new ComboBox { Header="Saved ChatGPT accounts",DisplayMemberPath="Label",HorizontalAlignment=HorizontalAlignment.Stretch };
        var models=new ComboBox { Header="Models available to this account",DisplayMemberPath="DisplayName",HorizontalAlignment=HorizontalAlignment.Stretch };
        var info=new TextBlock { Text="Use your eligible ChatGPT plan. Usage is shared with other apps; API-key billing remains separate. Connecting does not enable automatic analysis.",TextWrapping=TextWrapping.Wrap };
        var feedback=new TextBlock { TextWrapping=TextWrapping.Wrap };
        var connect=new Button { Content="Continue with ChatGPT" }; var refresh=new Button { Content="Refresh models" };
        var signOut=new Button { Content="Sign out of selected account" }; var cancel=new Button { Content="Cancel sign-in",Visibility=Visibility.Collapsed };
        var usage=new HyperlinkButton { Content="Manage ChatGPT usage and app access",NavigateUri=new("https://chatgpt.com/#settings/Usage") };
        CancellationTokenSource? signingIn=null; var updating=false; var unloaded=false; var modelLoadVersion=0;
        void LoadAccounts()
        {
            updating=true;
            try { accounts.ItemsSource=ChatGpt.Accounts; accounts.SelectedItem=ChatGpt.Accounts.FirstOrDefault(a=>a.Id==(policy.Provider?.Id=="chatgpt"?policy.Provider.AccountId:ChatGpt.ActiveAccountId));
                settingsChatGptAccountId=(accounts.SelectedItem as ChatGptAccount)?.Id; settingsChatGptModel=null; }
            catch { feedback.Text="Protected ChatGPT account storage is unavailable."; }
            finally { updating=false; }
        }
        async Task LoadModelsAsync()
        {
            if(unloaded||accounts.SelectedItem is not ChatGptAccount account) return;
            var version=++modelLoadVersion; settingsChatGptModel=null; refresh.IsEnabled=false; models.IsEnabled=false;
            try {
                var list=await ChatGpt.ListModelsAsync(account.Id,lifetime.Token);
                if(unloaded||version!=modelLoadVersion||accounts.SelectedItem is not ChatGptAccount selected||selected.Id!=account.Id) return;
                models.ItemsSource=list; models.SelectedItem=list.FirstOrDefault(m=>m.Slug==model.Text);
                feedback.Text=list.Count==0?"No eligible models returned. Reconnect this account or review its ChatGPT plan permissions.":"Choose a model, then save settings to use this connection.";
            } catch(ChatGptAuthenticationException e) { feedback.Text=e.Message; }
            catch(OperationCanceledException) { feedback.Text="Model discovery cancelled."; }
            catch { feedback.Text="Could not load models. Reconnect this account or check its permissions and connection."; }
            finally { if(version==modelLoadVersion) { refresh.IsEnabled=true; models.IsEnabled=true; } }
        }
        accounts.SelectionChanged+=async(_,_)=> {
            if(updating||unloaded) return;
            settingsChatGptAccountId=(accounts.SelectedItem as ChatGptAccount)?.Id; settingsChatGptModel=null; var selectionVersion=++modelLoadVersion;
            if(accounts.SelectedItem is not ChatGptAccount account) return;
            try { await Task.Run(()=>ChatGpt.SelectAccount(account.Id),lifetime.Token);
                if(unloaded||selectionVersion!=modelLoadVersion||accounts.SelectedItem is not ChatGptAccount current||current.Id!=account.Id) return;
                models.ItemsSource=null; model.Text=""; await LoadModelsAsync(); }
            catch { feedback.Text="The selected account could not be opened."; }
        };
        models.SelectionChanged+=(_,_)=> { if(unloaded) return; settingsChatGptModel=(models.SelectedItem as ChatGptModel)?.Slug;
            if(models.SelectedItem is ChatGptModel selected) { model.Text=selected.Slug; provider.SelectedIndex=2; } };
        connect.Click+=async(_,_)=> {
            signingIn=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); connect.IsEnabled=false; signOut.IsEnabled=false; accounts.IsEnabled=false; cancel.Visibility=Visibility.Visible;
            feedback.Text="Complete sign-in and plan-use consent in your browser.";
            try {
                async Task Launch(Uri uri) { if(!await global::Windows.System.Launcher.LaunchUriAsync(uri)) throw new InvalidOperationException("Browser unavailable."); }
                if(accounts.SelectedItem is ChatGptAccount selected) await ChatGpt.ReauthorizeAsync(selected.Id,Launch,signingIn.Token);
                else await ChatGpt.SignInAsync(Launch,signingIn.Token);
                if(unloaded) return;
                updating=true; accounts.ItemsSource=ChatGpt.Accounts; accounts.SelectedItem=ChatGpt.Accounts.FirstOrDefault(a=>a.Id==ChatGpt.ActiveAccountId);
                settingsChatGptAccountId=(accounts.SelectedItem as ChatGptAccount)?.Id; updating=false;
                provider.SelectedIndex=2; await LoadModelsAsync();
            } catch(OperationCanceledException) { feedback.Text="Sign-in cancelled. Existing provider settings are unchanged."; }
            catch { feedback.Text="ChatGPT sign-in could not be completed. Check browser consent and network access, then try again."; }
            finally { signingIn.Dispose(); signingIn=null; connect.IsEnabled=true; signOut.IsEnabled=true; accounts.IsEnabled=true; cancel.Visibility=Visibility.Collapsed; }
        };
        cancel.Click+=(_,_)=>signingIn?.Cancel();
        refresh.Click+=async(_,_)=>await LoadModelsAsync();
        var add=new Button { Content="Use another account" }; add.Click+=(_,_)=> { accounts.SelectedItem=null; models.ItemsSource=null; model.Text=""; connect.Focus(FocusState.Programmatic); };
        signOut.Click+=async(_,_)=> {
            if(accounts.SelectedItem is not ChatGptAccount account) return;
            signOut.IsEnabled=false;
            try {
                if(policy.Provider?.Id=="chatgpt"&&policy.Provider.AccountId==account.Id) {
                    var next=policy with { AutomaticEnabled=false,Revision=policy.Revision+1 }; preferences.Save(next,presentation); policy=next;
                    await coordinator.ChangePolicyAsync(next,lifetime.Token); await coordinator.WaitForIdleAsync(lifetime.Token);
                    signedOut();
                }
                var revoked=await ChatGpt.SignOutAsync(account.Id,lifetime.Token);
                if(unloaded) return;
                ++modelLoadVersion; models.ItemsSource=null; LoadAccounts();
                feedback.Text=revoked?"Signed out. Automatic analysis for this account is disabled.":"Signed out locally. Remote revocation was not confirmed; disconnect ProcSight in ChatGPT settings.";
            } catch { feedback.Text="Sign-out could not be completed. Disconnect ProcSight in ChatGPT settings."; }
            finally { signOut.IsEnabled=true; }
        };
        LoadAccounts();
        var buttons=new StackPanel { Spacing=8 }; buttons.Children.Add(connect); buttons.Children.Add(cancel); buttons.Children.Add(add); buttons.Children.Add(refresh); buttons.Children.Add(signOut);
        var expander=SettingsExpander("\uE77B","ChatGPT account","Connect with your ChatGPT plan",info,accounts,models,buttons,usage,feedback);
        expander.IsExpanded=policy.Provider?.Id=="chatgpt";
        expander.Expanding+=async(_,_)=>await LoadModelsAsync();
        if(expander.IsExpanded) _=LoadModelsAsync();
        expander.Unloaded+=(_,_)=> { unloaded=true; signingIn?.Cancel(); ++modelLoadVersion; };
        return expander;
    }
}
