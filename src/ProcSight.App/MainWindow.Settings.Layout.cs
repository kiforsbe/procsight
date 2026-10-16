using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private static TextBlock SettingsFeedback()
    {
        var text=new TextBlock { TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed };
        text.RegisterPropertyChangedCallback(TextBlock.TextProperty,(_,_)=>text.Visibility=
            string.IsNullOrWhiteSpace(text.Text)?Visibility.Collapsed:Visibility.Visible);
        return text;
    }
    private static void SettingsSection(StackPanel panel,string title)=>panel.Children.Add(new TextBlock {
        Text=title,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,18,0,4) });
    private static Grid SettingsHeader(string glyph,string title,string description)
    {
        var grid=new Grid { ColumnSpacing=16,MinHeight=40,VerticalAlignment=VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new() { Width=new GridLength(28) }); grid.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        var icon=new FontIcon { Glyph=glyph,FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=20,VerticalAlignment=VerticalAlignment.Center };
        var text=new StackPanel { Spacing=2 }; text.Children.Add(new TextBlock { Text=title,TextWrapping=TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text=description,FontSize=12,Opacity=0.8,TextWrapping=TextWrapping.Wrap });
        Grid.SetColumn(text,1); grid.Children.Add(icon); grid.Children.Add(text); return grid;
    }
    private Border SettingsCard(string glyph,string title,string description,FrameworkElement control)
    {
        var header=SettingsHeader(glyph,title,description); var grid=new Grid { ColumnSpacing=20 };
        grid.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height=GridLength.Auto }); grid.RowDefinitions.Add(new() { Height=GridLength.Auto });
        grid.Children.Add(header); Grid.SetColumn(control,1); control.VerticalAlignment=VerticalAlignment.Center; grid.Children.Add(control);
        var card=new Border { Padding=new Thickness(20,16,20,16),CornerRadius=new CornerRadius(4),Child=grid,
            Background=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],BorderThickness=new Thickness(1) };
        card.SizeChanged+=(_,e)=> { var compact=e.NewSize.Width<620; grid.RowSpacing=compact?12:0; Grid.SetColumn(control,compact?0:1); Grid.SetRow(control,compact?1:0);
            Grid.SetColumnSpan(header,compact?2:1); Grid.SetColumnSpan(control,compact?2:1); control.HorizontalAlignment=compact?HorizontalAlignment.Stretch:HorizontalAlignment.Right; };
        return card;
    }
    private static Expander SettingsExpander(string glyph,string title,string description,params UIElement[] controls)
    {
        var content=new StackPanel { Spacing=16,Margin=new Thickness(44,4,4,8) }; foreach(var control in controls) content.Children.Add(control);
        return new Expander { Header=SettingsHeader(glyph,title,description),Content=content,HorizontalAlignment=HorizontalAlignment.Stretch,
            HorizontalContentAlignment=HorizontalAlignment.Stretch,IsExpanded=false };
    }
}
