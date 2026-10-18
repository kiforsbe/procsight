namespace ProcSight.Application.Presentation;
public enum PageId { Processes, Services, Knowledge, Settings }
public sealed class NavigationState
{
    public PageId Current { get; private set; } = PageId.Processes;
    public void Select(PageId page) => Current = page;
}
