using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace FileFlows.Client.Shared;

using System.Collections.Generic;
using System.Linq;
using FileFlows.Shared;
using Microsoft.AspNetCore.Components;

public partial class NavMenu : IDisposable
{
    [Inject] private INavigationService NavigationService { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] public IJSRuntime jSRuntime { get; set; }
    private List<NavMenuGroup> MenuItems = new List<NavMenuGroup>();
    private bool collapseNavMenu = true;

    public NavMenuItem Active { get; private set; }

    private string lblVersion, lblHelp, lblForum, lblDiscord;

    private string NavMenuCssClass => collapseNavMenu ? "collapse" : null;
    private NavMenuItem nmiFlows, nmiLibraries;

    private int Unprocessed = -1, Processing = -1, Failed = -1;

    private BackgroundTask bubblesTask;

    protected override void OnInitialized()
    {
        lblVersion = Translater.Instant("Labels.Version");
        lblHelp = Translater.Instant("Labels.Help");
        lblForum = Translater.Instant("Labels.Forum");
        lblDiscord = Translater.Instant("Labels.Discord");
        
        App.Instance.OnFileFlowsSystemUpdated += FileFlowsSystemUpdated;

        bubblesTask = new BackgroundTask(TimeSpan.FromMilliseconds(10_000), () => _ = RefreshBubbles());
        _ = RefreshBubbles();
        bubblesTask.Start();
        
        this.LoadMenu();
    }

    private async Task RefreshBubbles()
    {
        bool hasFocus = await jSRuntime.InvokeAsync<bool>("eval", "document.hasFocus()");
        if (hasFocus == false)
            return;
        
        var sResult = await HttpHelper.Get<List<LibraryStatus>>("/api/library-file/status");
        if (sResult.Success == false || sResult.Data?.Any() != true)
            return;
        Unprocessed = sResult.Data.Where(x => x.Status == FileStatus.Unprocessed).Select(x => x.Count).FirstOrDefault();
        Processing = sResult.Data.Where(x => x.Status == FileStatus.Processing).Select(x => x.Count).FirstOrDefault();
        Failed = sResult.Data.Where(x => x.Status == FileStatus.ProcessingFailed).Select(x => x.Count).FirstOrDefault();
        this.StateHasChanged();
    }

    void LoadMenu()
    {
        this.MenuItems.Clear();

        MenuItems.Add(new NavMenuGroup
        {
            Name = "Overview",
            Icon = "fas fa-info-circle",
            Items = new List<NavMenuItem>
            {
                new ("Pages.Dashboard.Title", "fas fa-chart-pie", ""),
                new ("Pages.LibraryFiles.Title", "fas fa-copy", "library-files")
            }
        });

        nmiFlows = new("Pages.Flows.Title", "fas fa-sitemap", "flows");
        nmiLibraries = new("Pages.Libraries.Title", "fas fa-folder", "libraries");

        MenuItems.Add(new NavMenuGroup
        {
            Name = "Configuration",
            Icon = "fas fa-code-branch",
            Items = new List<NavMenuItem>
            {
                nmiFlows,
                nmiLibraries,
#if (!DEMO)
                new ("Pages.Nodes.Title", "fas fa-desktop", "nodes")
#endif
            }
        });

#if (!DEMO)
        MenuItems.Add(new NavMenuGroup
        {
            Name = "Extensions",
            Icon = "fas fa-laptop-house",
            Items = new List<NavMenuItem>
            {
                new("Pages.Plugins.Title", "fas fa-puzzle-piece", "plugins"),
                new("Pages.Scripts.Title", "fas fa-scroll", "scripts"),
                new("Pages.Variables.Title", "fas fa-at", "variables"),
            }
        });
        MenuItems.Add(new NavMenuGroup
        {
            Name = "System",
            Icon = "fas fa-desktop",
            Items = new List<NavMenuItem>
            {
                App.Instance.FileFlowsSystem.Licensed ? new ("Pages.Revisions.Title", "fas fa-history", "revisions") : null,
                App.Instance.FileFlowsSystem.Licensed ? new ("Pages.Tasks.Title", "fas fa-clock", "tasks") : null,
                new ("Pages.Settings.Title", "fas fa-cogs", "settings"),
            }
        });

        MenuItems.Add(new NavMenuGroup
        {
            Name = "Information",
            Icon = "fas fa-question-circle",
            Items = new List<NavMenuItem>
            {
                new NavMenuItem("Pages.Log.Title", "fas fa-file-alt", "log")
            }
        });

#endif

        try
        {
            string currentRoute = NavigationManager.Uri.Substring(NavigationManager.BaseUri.Length);
            Active = MenuItems.SelectMany(x => x.Items).Where(x => x.Url == currentRoute).FirstOrDefault() ??
                     MenuItems[0].Items.First();
        }
        catch (Exception)
        {
        }
    }

    private void FileFlowsSystemUpdated(FileFlowsStatus system)
    {
        this.LoadMenu();
        this.StateHasChanged();
    }

    private string? GetStepLabel(NavMenuItem nmi)
    {
        if (nmi == Active)
            return null;
        if ((App.Instance.FileFlowsSystem.ConfigurationStatus & ConfigurationStatus.Flows) !=
            ConfigurationStatus.Flows)
        {
            return nmi == nmiFlows ? "Step 1" : null;
        }

        if ((App.Instance.FileFlowsSystem.ConfigurationStatus & ConfigurationStatus.Libraries) !=
            ConfigurationStatus.Libraries)
        {
            return nmi == nmiLibraries ? "Step 2" : null; 
        }

        return null;
    }
    

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }

    async Task Click(NavMenuItem item)
    {
        bool ok = await NavigationService.NavigateTo(item.Url);
        if (ok)
        {
            await jSRuntime.InvokeVoidAsync("eval", $"document.title = 'FileFlows'");
            SetActive(item);
            collapseNavMenu = true;
            this.StateHasChanged();
        }
    }

    private void SetActive(NavMenuItem item)
    {
        Active = item;
        this.StateHasChanged();
    }


    public void Dispose()
    {
        _ = bubblesTask?.StopAsync();
        bubblesTask = null;
    }
}

public class NavMenuGroup
{
    public string Name { get; set; }
    public string Icon { get; set; }
    public List<NavMenuItem> Items { get; set; } = new List<NavMenuItem>();
}

public class NavMenuItem
{
    public string Title { get; set; }
    public string Icon { get; set; }
    public string Url { get; set; }

    public NavMenuItem(string title = "", string icon = "", string url = "")
    {
        this.Title = Translater.TranslateIfNeeded(title);
        this.Icon = icon;
        this.Url = url;
    }
}