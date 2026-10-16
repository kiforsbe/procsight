using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Infrastructure.Authentication;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private string? settingsChatGptAccountId;
    private string? settingsChatGptModel;
    private FrameworkElement BuildChatGptSettings(ComboBox provider,TextBox model,Action signedOut,ProcSight.Core.Models.ProviderSettings? configured=null,Action? reauthorized=null)
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
            try { accounts.ItemsSource=ChatGpt.Accounts; accounts.SelectedItem=ChatGpt.Accounts.FirstOrDefault(a=>a.Id==(configured?.AccountId??(policy.Provider?.Id=="chatgpt"?policy.Provider.AccountId:ChatGpt.ActiveAccountId)));
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
                feedback.Text=list.Count==0?"No eligible models returned. Reconnect this account or review its ChatGPT plan permissions.":"Model changes apply automatically. Sign-in does not enable analysis.";
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
                reauthorized?.Invoke(); provider.SelectedIndex=2; await LoadModelsAsync();
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
                if(policy.Provider?.AccountId==account.Id||policy.Connections?.Any(p=>p.Provider.AccountId==account.Id)==true) {
                    await ApplySettingsAsync(policy with { Connections=policy.Connections?.Select(p=>p.Provider.AccountId==account.Id?p with { Enabled=false }:p).ToArray() });
                    await coordinator.WaitForIdleAsync(lifetime.Token);
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
        var selectors=new Grid { ColumnSpacing=16,RowSpacing=12 };
        selectors.ColumnDefinitions.Add(new()); selectors.ColumnDefinitions.Add(new());
        selectors.RowDefinitions.Add(new() { Height=GridLength.Auto }); selectors.RowDefinitions.Add(new() { Height=GridLength.Auto });
        selectors.Children.Add(accounts);
        var modelRow=new Grid { ColumnSpacing=8 }; modelRow.ColumnDefinitions.Add(new()); modelRow.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        modelRow.Children.Add(models); Grid.SetColumn(refresh,1); refresh.VerticalAlignment=VerticalAlignment.Bottom; modelRow.Children.Add(refresh); selectors.Children.Add(modelRow);
        var buttons=new Grid { ColumnSpacing=8,RowSpacing=8,HorizontalAlignment=HorizontalAlignment.Left };
        for(var i=0;i<3;i++) buttons.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        buttons.RowDefinitions.Add(new() { Height=GridLength.Auto }); buttons.RowDefinitions.Add(new() { Height=GridLength.Auto });
        buttons.Children.Add(connect); buttons.Children.Add(add); buttons.Children.Add(signOut); buttons.Children.Add(cancel);
        void Arrange(double width) {
            var compact=width<660;
            Grid.SetColumn(modelRow,compact?0:1); Grid.SetRow(modelRow,compact?1:0); Grid.SetColumnSpan(modelRow,compact?2:1);
            Grid.SetColumnSpan(accounts,compact?2:1);
            Grid.SetColumn(add,1); Grid.SetColumn(signOut,compact?0:2); Grid.SetRow(signOut,compact?1:0);
            Grid.SetColumn(cancel,compact?1:0); Grid.SetRow(cancel,1);
        }
        selectors.SizeChanged+=(_,e)=>Arrange(e.NewSize.Width); Arrange(0);
        usage.Padding=new Thickness(0); usage.HorizontalAlignment=HorizontalAlignment.Left;
        var expander=SettingsExpander("\uE77B","ChatGPT account","Connect with your ChatGPT plan",info,selectors,buttons,usage,feedback);
        expander.IsExpanded=configured?.Id=="chatgpt"||policy.Provider?.Id=="chatgpt";
        expander.Expanding+=async(_,_)=>await LoadModelsAsync();
        if(expander.IsExpanded) _=LoadModelsAsync();
        expander.Unloaded+=(_,_)=> { unloaded=true; signingIn?.Cancel(); ++modelLoadVersion; };
        return expander;
    }
}
