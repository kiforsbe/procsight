using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using ProcSight.App.ViewModels;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using System.Text;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private TableColumnPreference[] tableColumnLayout=[];
    private IReadOnlyList<TableColumnWidth> tableColumnWidths=[];
    private Grid? tableHeader;
    private ContentDialog? tableConfigurationDialog;
    private double TableMinimumWidth=>tableColumnLayout.Sum(c=>c.Width);
    private IEnumerable<Grid> RealizedColumnRows()=>table?.ItemsPanelRoot is Panel panel
        ?panel.Children.OfType<ListViewItem>().Select(c=>c.ContentTemplateRoot).OfType<Grid>():[];
    private void BindRowChevron(ListViewBase sender,ContainerContentChangingEventArgs args)
    {
        if(args.ItemContainer.ContentTemplateRoot is Grid existing) {
            if(!args.InRecycleQueue) {
                ArrangeColumnCells(existing);
                if(draggedColumnId is { } id) PositionDraggedRow(existing,id);
            }
        }
        if(args.InRecycleQueue) return;
        if(args.Item is ProcessRow resourceRow) RefreshResourceShading(only:resourceRow);
        if(args.Phase==0) { args.RegisterUpdateCallback(1,BindRowChevron); return; }
        void Bind(DependencyObject parent) {
            if(parent is Button { Tag:string } button) { button.Click-=ToggleExpansion; button.Click+=ToggleExpansion; return; }
            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) Bind(VisualTreeHelper.GetChild(parent,i));
        }
        if(args.ItemContainer.ContentTemplateRoot is Grid root) {
            ArrangeColumnCells(root); Bind(root);
            if(draggedColumnId is { } id) PositionDraggedRow(root,id);
        }
    }
    private void InitializeColumns()
    {
        RestoreDraggedColumn();
        tableColumnLayout=TableColumns.Normalize(page=="Services"?presentation.ServiceColumns:presentation.ProcessColumns,page=="Services");
        tableColumnWidths=tableColumnLayout.Select(c=>new TableColumnWidth(c.Width)).ToArray();
    }
    private void ArrangeColumnCells(Grid grid)
    {
        foreach(var cell in grid.Children.OfType<FrameworkElement>().Where(c=>c.Tag is string)) {
            var index=Array.FindIndex(tableColumnLayout,c=>c.Id==(string)cell.Tag);
            if(index>=0&&Grid.GetColumn(cell)!=index) Grid.SetColumn(cell,index);
        }
    }
    private void ApplyColumns(IEnumerable<TableColumnPreference> columns,bool persist=true)
    {
        if(columnMotions.Count>0) RestoreDraggedColumn();
        var normalized=TableColumns.Normalize(columns,page=="Services");
        var sameColumns=normalized.Length==tableColumnLayout.Length&&normalized.Select(c=>c.Id).ToHashSet().SetEquals(tableColumnLayout.Select(c=>c.Id));
        tableColumnLayout=normalized;
        if(sameColumns&&tableHeader is not null&&table is not null) {
            // Permutations retain the native controls, bindings, icons and row template.
            for(var i=0;i<normalized.Length;i++) tableColumnWidths[i].Width=new(normalized[i].Width);
            tableHeader.MinWidth=TableMinimumWidth; ArrangeColumnCells(tableHeader);
            foreach(var grid in RealizedColumnRows()) {
                ArrangeColumnCells(grid);
            }
        } else {
            // Detach old index bindings before changing the number of width slots.
            if(table is not null) table.ItemTemplate=null;
            tableColumnWidths=tableColumnLayout.Select(c=>new TableColumnWidth(c.Width)).ToArray();
            foreach(var row in knownRows.Values) row.TableColumns=tableColumnWidths;
            if(tableHeader is not null) PopulateColumnHeader(tableHeader);
            if(table is not null) table.ItemTemplate=ColumnTemplate();
        }
        if(persist) {
            presentation=page=="Services"?presentation with { ServiceColumns=tableColumnLayout.ToArray() }:presentation with { ProcessColumns=tableColumnLayout.ToArray() };
            SavePresentation();
        }
    }
    private void ResizeColumn(int index,double width,bool save)
    {
        width=Math.Clamp(width,48,2000);
        tableColumnLayout=tableColumnLayout.ToArray();
        tableColumnLayout[index]=tableColumnLayout[index] with { Width=width };
        tableColumnWidths[index].Width=new(width);
        if(tableHeader is not null) tableHeader.MinWidth=TableMinimumWidth;
        if(save) {
            presentation=page=="Services"?presentation with { ServiceColumns=tableColumnLayout.ToArray() }:presentation with { ProcessColumns=tableColumnLayout.ToArray() };
            SavePresentation();
        }
    }
    private void MoveColumn(string source,string target,bool persist=true)
    {
        if(source=="Name"||target=="Name"||source==target) return;
        var list=tableColumnLayout.ToList(); var from=list.FindIndex(c=>c.Id==source); var to=list.FindIndex(c=>c.Id==target);
        if(from<1||to<1) return;
        var item=list[from]; list.RemoveAt(from); list.Insert(to,item); ApplyColumns(list,persist);
    }
    private void PopulateColumnHeader(Grid header)
    {
        header.Children.Clear(); header.ColumnDefinitions.Clear(); sortHeaders.Clear(); cpuSummary=null; memorySummary=null; diskSummary=null; networkSummary=null; gpuSummary=null;
        header.MinWidth=TableMinimumWidth;
        for(var i=0;i<tableColumnLayout.Length;i++) {
            var index=i; var preference=tableColumnLayout[i]; var definition=TableColumns.All.Single(c=>c.Id==preference.Id);
            var column=new ColumnDefinition(); BindingOperations.SetBinding(column,ColumnDefinition.WidthProperty,new Binding { Source=tableColumnWidths[i],Path=new("Width") }); header.ColumnDefinitions.Add(column);
            var numeric=preference.Id is "Pid" or "ParentPid" or "Cpu" or "Memory" or "Disk" or "Network" or "Gpu";
            var stack=new StackPanel { VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=numeric?HorizontalAlignment.Right:HorizontalAlignment.Left };
            if(preference.Id is "Cpu" or "Memory") {
                var total=new TextBlock { FontSize=16,TextAlignment=TextAlignment.Right }; stack.Children.Add(total);
                if(preference.Id=="Cpu") cpuSummary=total; else memorySummary=total;
            }
            if(preference.Id is "Disk" or "Network" or "Gpu") {
                var summary=new TextBlock { Text="—",FontSize=16,TextAlignment=TextAlignment.Right }; stack.Children.Add(summary);
                if(preference.Id=="Disk") diskSummary=summary; else if(preference.Id=="Network") networkSummary=summary; else gpuSummary=summary;
            }
            var label=new TextBlock { Text=definition.Label,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis }; stack.Children.Add(label);
            var button=new Button { Content=stack,Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),BorderThickness=new(0),CornerRadius=new(0),
                HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,HorizontalContentAlignment=numeric?HorizontalAlignment.Right:HorizontalAlignment.Left,
                VerticalContentAlignment=VerticalAlignment.Bottom,Padding=new(i==0?16:8,0,12,8) };
            var sorting=Enum.Parse<ProcessSort>(preference.Id);
            sortHeaders[sorting]=(label,definition.Label,button); button.Click+=(_,_)=> { if(!headerGestureConsumed) SelectSort(sorting); };
            AttachHeaderDrag(button,preference.Id);
            // Center a generous invisible hit target on the divider, above both adjacent buttons.
            var thumb=new ColumnResizeThumb { Tag=preference.Id,Width=20,Margin=new(0,0,-10,0),HorizontalAlignment=HorizontalAlignment.Right,Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),IsTabStop=true };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(thumb,"Resize "+definition.Label);
            int CurrentIndex()=>Array.FindIndex(tableColumnLayout,c=>c.Id==preference.Id);
            thumb.DragDelta+=(_,e)=> { var current=CurrentIndex(); if(current>=0) ResizeColumn(current,tableColumnLayout[current].Width+e.HorizontalChange,false); };
            thumb.DragCompleted+=(_,_)=> { var current=CurrentIndex(); if(current>=0) ResizeColumn(current,tableColumnLayout[current].Width,true); };
            thumb.KeyDown+=(_,e)=> { var current=CurrentIndex(); if(current>=0&&e.Key is global::Windows.System.VirtualKey.Left or global::Windows.System.VirtualKey.Right) { ResizeColumn(current,tableColumnLayout[current].Width+(e.Key==global::Windows.System.VirtualKey.Right?8:-8),true); e.Handled=true; } };
            var cell=new Grid(); cell.Children.Add(button);
            var border=new Border { Tag=preference.Id,Child=cell,BorderThickness=new(i==0?0:1,0,0,0),BorderBrush=header.BorderBrush };
            Grid.SetColumn(border,i); header.Children.Add(border);
            Canvas.SetZIndex(thumb,2); Grid.SetColumn(thumb,i); header.Children.Add(thumb);
        }
        // Fill unused viewport space without changing user-chosen column widths.
        header.ColumnDefinitions.Add(new() { Width=new(1,GridUnitType.Star) });
        var menu=new MenuFlyout(); var choose=new MenuFlyoutItem { Text="Choose columns…",Icon=new SymbolIcon(Symbol.List) };
        choose.Click+=async(_,_)=>await ChooseColumnsAsync(); menu.Items.Add(choose);
        var reset=new MenuFlyoutItem { Text="Restore default columns" }; reset.Click+=(_,_)=>ApplyColumns(TableColumns.Normalize(null,page=="Services")); menu.Items.Add(reset);
        header.ContextFlyout=menu; UpdateSortHeaders(); RefreshResourceSummary();
    }
    private DataTemplate ColumnTemplate()
    {
        var text=new StringBuilder("<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Grid Height=\"28\"><Grid.ColumnDefinitions>");
        for(var i=0;i<tableColumnLayout.Length;i++) text.Append($"<ColumnDefinition Width=\"{{Binding TableColumns[{i}].Width}}\"/>");
        text.Append("<ColumnDefinition Width=\"*\"/></Grid.ColumnDefinitions>");
        text.Append(page=="Services"?"""
            <Grid Tag="Name" Margin="16,0,8,0"><Grid.ColumnDefinitions><ColumnDefinition Width="24"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Grid Width="16" Height="16"><FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE713;" FontSize="16" Visibility="{Binding FallbackIconVisibility}"/><Image Source="{Binding Icon}" Width="16" Height="16"/></Grid>
            <TextBlock Grid.Column="1" Text="{Binding Name}" FontSize="13" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/></Grid>
            """:"""
            <Grid Tag="Name" Margin="{Binding Indent}" ToolTipService.ToolTip="{Binding Owner}"><Grid.ColumnDefinitions><ColumnDefinition Width="24"/><ColumnDefinition Width="24"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <Button Width="24" Height="24" Padding="0" BorderThickness="0" Background="Transparent" Visibility="{Binding ExpanderVisibility}" Tag="{Binding Key}" AutomationProperties.Name="{Binding ExpansionAction}"><FontIcon FontFamily="Segoe Fluent Icons" Glyph="{Binding ExpansionGlyph}" FontSize="10"/></Button>
            <Grid Grid.Column="1"><SymbolIcon Symbol="AllApps" Width="16" Height="16" Visibility="{Binding FallbackIconVisibility}"/><Image Source="{Binding Icon}" Width="16" Height="16"/></Grid>
            <TextBlock Grid.Column="2" Text="{Binding Name}" FontSize="13" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/></Grid>
            """);
        for(var i=1;i<tableColumnLayout.Length;i++) {
            var id=tableColumnLayout[i].Id;
            var binding=id switch { "Assessment"=>"Status","Owner"=>"Ownership",_=>id };
            var value="{Binding "+binding+"}";
            var tooltip=id is "Disk" or "Network" or "Gpu" or "Started"?"{Binding "+id+"Details}":value;
            var background=id is "Cpu" or "Memory" or "Disk" or "Network" or "Gpu"?$" Background=\"{{Binding {id}Brush}}\"":"";
            var alignment=id is "Pid" or "ParentPid" or "Cpu" or "Memory" or "Disk" or "Network" or "Gpu"?" HorizontalAlignment=\"Right\"":"";
            text.Append($"<Border Tag=\"{id}\" Grid.Column=\"{i}\" BorderThickness=\"1,0,0,0\" BorderBrush=\"{{ThemeResource DividerStrokeColorDefaultBrush}}\"{background}><TextBlock Text=\"{value}\" ToolTipService.ToolTip=\"{tooltip}\" FontSize=\"12\" Margin=\"8,0\" VerticalAlignment=\"Center\" TextTrimming=\"CharacterEllipsis\"{alignment}/></Border>");
        }
        text.Append("</Grid></DataTemplate>"); return (DataTemplate)XamlReader.Load(text.ToString());
    }
    private async Task ChooseColumnsAsync()
    {
        var panel=new StackPanel { Spacing=8,MinWidth=320 };
        panel.Children.Add(RuleText("Name is always first and visible. Changes apply immediately. Drag headers to reorder, or use Move up/down here.","CaptionTextBlockStyle",true));
        var list=new ListView { SelectionMode=ListViewSelectionMode.Single,MaxHeight=380 };
        void Load() {
            list.Items.Clear();
            var catalog=TableColumns.Available(page=="Services");
            foreach(var def in tableColumnLayout.Select(c=>catalog.Single(d=>d.Id==c.Id)).Concat(catalog.Where(d=>tableColumnLayout.All(c=>c.Id!=d.Id)))) {
                var row=new Grid { ColumnSpacing=12 }; row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
                var check=new CheckBox { Content=def.Label,IsChecked=tableColumnLayout.Any(c=>c.Id==def.Id),IsEnabled=def.Id!="Name",MinWidth=180 };
                check.Click+=(_,_)=> { ApplyColumns(check.IsChecked==true?tableColumnLayout.Append(new(def.Id,def.Width)):tableColumnLayout.Where(c=>c.Id!=def.Id)); };
                row.Children.Add(check);
                var width=new NumberBox { Value=tableColumnLayout.FirstOrDefault(c=>c.Id==def.Id)?.Width??def.Width,Minimum=48,Maximum=2000,Width=96,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(width,def.Label+" width");
                width.ValueChanged+=(_,e)=> { var index=Array.FindIndex(tableColumnLayout,c=>c.Id==def.Id); if(index>=0&&double.IsFinite(e.NewValue)) ResizeColumn(index,e.NewValue,true); };
                Grid.SetColumn(width,1); row.Children.Add(width); list.Items.Add(new ListViewItem { Content=row,Tag=def.Id,HorizontalContentAlignment=HorizontalAlignment.Stretch });
            }
        }
        Load(); panel.Children.Add(list);
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        foreach(var delta in new[]{-1,1}) {
            var move=new Button { Content=delta<0?"Move up":"Move down" };
            move.Click+=(_,_)=> { if(list.SelectedItem is not ListViewItem { Tag:string id }||id=="Name") return; var index=Array.FindIndex(tableColumnLayout,c=>c.Id==id); var target=index+delta; if(index<1||target<1||target>=tableColumnLayout.Length) return; MoveColumn(id,tableColumnLayout[target].Id); Load(); list.SelectedItem=list.Items.OfType<ListViewItem>().First(i=>i.Tag as string==id); }; actions.Children.Add(move);
        }
        var reset=new Button { Content="Restore defaults" }; reset.Click+=(_,_)=> { ApplyColumns(TableColumns.Normalize(null,page=="Services")); Load(); }; actions.Children.Add(reset); panel.Children.Add(actions);
        tableConfigurationDialog=new ContentDialog { Title="Choose columns",RequestedTheme=WindowRoot.ActualTheme,Content=new ScrollViewer { Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot };
        try { await tableConfigurationDialog.ShowAsync(); } finally { tableConfigurationDialog=null; }
    }
}
