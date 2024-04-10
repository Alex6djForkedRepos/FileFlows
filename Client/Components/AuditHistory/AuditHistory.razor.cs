using FileFlows.Client.Components.Common;
using Humanizer;
using Microsoft.AspNetCore.Components;

namespace FileFlows.Client.Components;

/// <summary>
/// Audit history popup
/// </summary>
public partial class AuditHistory
{
    /// <summary>
    /// Gets the static instance of the audit history
    /// </summary>
    public static AuditHistory Instance { get;private set; }
    
    /// <summary>
    /// Gets or sets the blocker tho show
    /// </summary>
    [CascadingParameter] public Blocker Blocker { get; set; }
    
    /// <summary>
    /// The task to complete when the show is complete
    /// </summary>
    TaskCompletionSource ShowTask;
    /// <summary>
    /// The UID of the object being audited
    /// </summary>
    private Guid Uid;
    /// <summary>
    /// The type of object being audited
    /// </summary>
    private string Type;
    /// <summary>
    /// The title to show
    /// </summary>
    private string Title;
    /// <summary>
    /// If this is visible or not
    /// </summary>
    private bool Visible;
    /// <summary>
    /// Close label
    /// </summary>
    private string lblClose;
    /// <summary>
    /// Data in the table
    /// </summary>
    private List<AuditEntry> Data = new ();
    /// <summary>
    /// If the component is waiting a render
    /// </summary>
    private bool AwaitingRender = false;
    /// <summary>
    /// The table instance
    /// </summary>
    public FlowTable<AuditEntry> Table { get; set; }

    /// <summary>
    /// Constructs a new instance of the Audit history component
    /// </summary>
    public AuditHistory()
    {
        Instance = this;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        lblClose = Translater.Instant("Labels.Close");
    }
    
    /// <summary>
    /// Closes the audit history
    /// </summary>
    private void Close()
    {
        this.Visible = false;
        this.Data.Clear();
        this.ShowTask.SetResult();
    }

    /// <summary>
    /// Waits for the component to re-render
    /// </summary>
    private async Task AwaitRender()
    {
        AwaitingRender = true;
        this.StateHasChanged();
        await Task.Delay(10);
        while (AwaitingRender)
            await Task.Delay(10);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (AwaitingRender)
            AwaitingRender = false;
    }

    
    /// <summary>
    /// Shows the audit history for an object
    /// </summary>
    /// <param name="uid">the UID of the object</param>
    /// <param name="type">the type of object</param>
    public Task Show(Guid uid, string type)
    {
        this.Uid = uid;
        this.Type = type;
        this.Title = Translater.Instant("Labels.Audit");
        this.Blocker.Show();
        Instance.ShowTask = new ();
        _ = ShowActual(uid, type);
        return Instance.ShowTask.Task;
    }
    
    /// <summary>
    /// Performs the actual show and loads the data from the server
    /// </summary>
    /// <param name="uid">the UID of the object</param>
    /// <param name="type">the type of object</param>
    private async Task ShowActual(Guid uid, string type)
    {
        try
        {  
            var response = await HttpHelper.Get<AuditEntry[]>($"/api/audit/{type}/{uid}");
            if (response.Success == false)
            {
                ShowTask.SetResult();
                return;
            }

            if (response.Data?.Any() != true)
            {
                ShowTask.SetResult();
                Toast.ShowWarning(Translater.Instant("Labels.NoAuditHistoryAvailable"));
                return;
            }

            if (response.Data.First().Parameters.TryGetValue("Name", out object oName))
                this.Title = oName.ToString();

            foreach (var d in response.Data)
            {
                d.Parameters ??= new();
                if(string.IsNullOrEmpty(d.ObjectType) == false)
                    d.Parameters["Type"] = d.ObjectType[(d.ObjectType.LastIndexOf(".", StringComparison.Ordinal) + 1)..].Humanize();
                d.Parameters["User"] = d.OperatorName;
                d.Summary = Translater.Instant($"AuditActions.{d.Action}", d.Parameters);
            }
            
            Data = response.Data.ToList();
            this.Visible = true;
            this.StateHasChanged();
            await AwaitRender();
            this.StateHasChanged();
        }
        finally
        {
            Blocker.Hide();
        }
    }
    
    /// <summary>
    /// Gets or sets if this is maximised
    /// </summary>
    protected bool Maximised { get; set; }
    /// <summary>
    /// Maximises the viewer
    /// </summary>
    /// <param name="maximised">true to maximise otherwise false to return to normal</param>
    protected void OnMaximised(bool maximised)
    {
        this.Maximised = maximised;
    }

    /// <summary>
    /// Views the object
    /// </summary>
    /// <param name="entry">the audit entry</param>
    private async Task View(AuditEntry entry)
    {
        if (entry?.Changes?.Any() != true)
            return;
        await AuditEntryViewer.Instance.Show(entry);
    } 
}
