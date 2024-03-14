using BlazorContextMenu;
using FileFlows.Client.Components.Dialogs;

namespace FileFlows.Client.Pages;

using FileFlows.Client.Components;

/// <summary>
/// Page for processing nodes
/// </summary>
public partial class Nodes : ListPage<Guid, ProcessingNode>
{
    public override string ApiUrl => "/api/node";
    const string FileFlowsServer = "FileFlowsServer";

    private ProcessingNode EditingItem = null;

    private ProcessingNode SelectedItem = null;

    private string lblInternal, lblAddress, lblRunners, lblVersion, lblDownloadNode, lblUpgradeRequired, 
        lblUpgradeRequiredHint, lblRunning, lblDisconnected, lblPossiblyDisconnected;
     
#if(DEBUG)
    string DownloadUrl = "http://localhost:6868/download";
#else
    string DownloadUrl = "/download";
#endif
    protected override void OnInitialized()
    {
        base.OnInitialized();
        lblInternal= Translater.Instant("Pages.Nodes.Labels.Internal");
        lblAddress = Translater.Instant("Pages.Nodes.Labels.Address");
        lblRunners = Translater.Instant("Pages.Nodes.Labels.Runners");
        lblVersion = Translater.Instant("Pages.Nodes.Labels.Version");
        lblDownloadNode = Translater.Instant("Pages.Nodes.Labels.DownloadNode");
        lblUpgradeRequired = Translater.Instant("Pages.Nodes.Labels.UpgradeRequired");
        lblUpgradeRequiredHint = Translater.Instant("Pages.Nodes.Labels.UpgradeRequiredHint");

        lblRunning = Translater.Instant("Labels.Running");
        lblPossiblyDisconnected = Translater.Instant("Labels.PossiblyDisconnected");
        lblDisconnected = Translater.Instant("Labels.Disconnected");
    }

    /// <summary>
    /// we only want to do the sort the first time, otherwise the list will jump around for the user
    /// </summary>
    private List<Guid> initialSortOrder;
    
    /// <inheritdoc />
    public override Task PostLoad()
    {
        var serverNode = this.Data?.Where(x => x.Address == FileFlowsServer).FirstOrDefault();
        if(serverNode != null)
        {
            serverNode.Name = Translater.Instant("Pages.Nodes.Labels.FileFlowsServer");                
        }

        if (initialSortOrder == null)
        {
            Data = Data?.OrderByDescending(x => x.Enabled)?.ThenByDescending(x => x.Priority).ThenBy(x => x.Name)
                ?.ToList();
            initialSortOrder = Data?.Select(x => x.Uid)?.ToList();
        }
        else
        {
            Data = Data?.OrderBy(x => initialSortOrder.Contains(x.Uid) ? initialSortOrder.IndexOf(x.Uid) : 1000000)
                .ThenByDescending(x => x.Priority).ThenBy(x => x.Name)
                ?.ToList();
        }

        return base.PostLoad();
    }

    /// <summary>
    /// if currently enabling, this prevents double calls to this method during the updated list binding
    /// </summary>
    private bool enabling = false;
    async Task Enable(bool enabled, ProcessingNode node)
    {
        if(enabling || node.Enabled == enabled)
            return;
        Blocker.Show();
        enabling = true;
        try
        {
            await HttpHelper.Put<ProcessingNode>($"{ApiUrl}/state/{node.Uid}?enable={enabled}");
            await Refresh();
        }
        finally
        {
            enabling = false;
            Blocker.Hide();
        }
    }

    async Task<bool> Save(ExpandoObject model)
    {
        Blocker.Show();
        this.StateHasChanged();

        try
        {
            var saveResult = await HttpHelper.Post<ProcessingNode>($"{ApiUrl}", model);
            if (saveResult.Success == false)
            {
                Toast.ShowError( saveResult.Body?.EmptyAsNull() ?? Translater.Instant("ErrorMessages.SaveFailed"));
                return false;
            }

            int index = this.Data.FindIndex(x => x.Uid == saveResult.Data.Uid);
            if (index < 0)
                this.Data.Add(saveResult.Data);
            else
                this.Data[index] = saveResult.Data;
            await this.Load(saveResult.Data.Uid);

            return true;
        }
        finally
        {
            Blocker.Hide();
            this.StateHasChanged();
        }
    }

    public async Task DeleteItem(ProcessingNode item)
    {
        if (await Confirm.Show("Labels.Delete",
                Translater.Instant("Pages.Nodes.Messages.DeleteNode", new { name = item.Name })) == false)
            return; // rejected the confirm

        Blocker.Show();
        this.StateHasChanged();

        try
        {
            var deleteResult = await HttpHelper.Delete(DeleteUrl, new ReferenceModel<Guid> { Uids = new [] { item.Uid } });
            if (deleteResult.Success == false)
            {
                if(Translater.NeedsTranslating(deleteResult.Body))
                    Toast.ShowError( Translater.Instant(deleteResult.Body));
                else
                    Toast.ShowError( Translater.Instant("ErrorMessages.DeleteFailed"));
                return;
            }
            this.Data.Remove(item);
        }
        finally
        {
            Blocker.Hide();
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Checks if two versions are the same
    /// </summary>
    /// <param name="versionA">the first version</param>
    /// <param name="versionB">the second version</param>
    /// <returns>true if same, otherwise false</returns>
    private bool VersionsAreSame(string versionA, string versionB)
    {
        if (versionA == versionB)
            return true;
        if(versionA == null || versionB == null)
            return false;
        if (string.Equals(versionA, versionB, StringComparison.InvariantCultureIgnoreCase))
            return true;
        if (Version.TryParse(versionA, out Version va) == false)
            return false;
        if (Version.TryParse(versionB, out Version vb) == false)
            return false;
        return va == vb;
    }
}