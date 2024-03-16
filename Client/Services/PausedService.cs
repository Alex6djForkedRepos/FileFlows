using FileFlows.Client.Components.Dialogs;
namespace FileFlows.Client.Services;
/// <summary>
/// Represents the method that handles the PausedLabelChanged event.
/// </summary>
/// <param name="label">The argument passed to the event handler.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate void OnPausedLabelChangedEventHandler(string label);

/// <summary>
/// Represents the method that handles the OnPaused event.
/// </summary>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task OnPausedEventHandler();

/// <summary>
/// Represents the method that handles the OnResume event.
/// </summary>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task OnResumeEventHandler();

/// <summary>
/// Service that monitors the system's paused status.
/// </summary>
public interface IPausedService
{
    /// <summary>
    /// Gets the label representing the paused state
    /// </summary>
    string PausedLabel { get;  }
    /// <summary>
    /// Gets if the system is paused
    /// </summary>
    bool IsPaused { get; }
    /// <summary>
    /// Toggles the paused system
    /// </summary>
    Task Toggle();
    /// <summary>
    /// Pauses the system.
    /// </summary>
    Task Pause();

    /// <summary>
    /// Resumes the system.
    /// </summary>
    Task Resume();

    /// <summary>
    /// Occurs when the paused label changes.
    /// </summary>
    event OnPausedLabelChangedEventHandler OnPausedLabelChanged;

    /// <summary>
    /// Occurs when the system is paused.
    /// </summary>
    event OnPausedEventHandler OnPaused;

    /// <summary>
    /// Occurs when the system resumes from a paused state.
    /// </summary>
    event OnResumeEventHandler OnResume;
}

/// <summary>
/// Service that monitors the systems paused status
/// </summary>
public class PausedService : IPausedService
{
    private SystemInfo SystemInfo = new SystemInfo();
    private TimeSpan TimeDiff;
    private DateTime LastUpdated = DateTime.MinValue;
    private string lblPause, lblPaused, lblPausedWithTime;
    private bool Refreshing = false;
    
    /// <inheritdoc />
    public string PausedLabel { get; private set; }

    /// <inheritdoc />
    public bool IsPaused => SystemInfo?.IsPaused == true;

    private bool translated = false;

    /// <summary>
    /// Constructs an instance of the paused worker
    /// </summary>
    public PausedService(ClientService clientService)
    {
        var bkgTask = new BackgroundTask(TimeSpan.FromMilliseconds(1_000), () => _ = DoWork());
        bkgTask.Start();
        SystemInfo = new SystemInfo()
        {
            IsPaused = clientService.IsPaused
        };
        lblPause = "Pause Processing";
        lblPaused = "Resume Processing";
        lblPausedWithTime = "Pause for";
        PausedLabel = IsPaused ? "Resume Processing" : "Pause Processing";
    }
    
    /// <summary>
    /// Do the work to update the paused state
    /// </summary>
    private async Task DoWork()
    {
        if (translated == false && Translater.InitDone)
        {
            translated = true;
            lblPause = Translater.Instant("Labels.Pause");
            lblPaused = Translater.Instant("Labels.Paused");
            lblPausedWithTime = Translater.Instant("Labels.PausedWithTime");
        }
        if (LastUpdated < DateTime.UtcNow.AddSeconds(-5))
        {
            await Refresh();
        }

        string originalLabel = PausedLabel;
        UpdateTime();
        OnPausedLabelChanged?.Invoke(PausedLabel);
    }
    /// <summary>
    /// Update the Pause Label
    /// </summary>
    private void UpdateTime()
    {
        if (SystemInfo.IsPaused == false)
        {
            PausedLabel = lblPause;
            return;
        }

        if (SystemInfo.PausedUntil > SystemInfo.CurrentTime.AddYears(1))
        {
            PausedLabel = lblPaused;
            return;
        }
        
        var pausedToLocal = SystemInfo.PausedUntil.Add(TimeDiff);
        var time = pausedToLocal.Subtract(DateTime.UtcNow);
        PausedLabel = lblPausedWithTime + " " + time.ToString(@"h\:mm\:ss");
    }
    
    /// <summary>
    /// Refreshes the paused state
    /// </summary>
    async Task Refresh()
    {
        if (Refreshing)
            return;
        Refreshing = true;
        try
        {
            RequestResult<List<FlowExecutorInfo>> result = null;
            RequestResult<SystemInfo> systemInfoResult = await GetSystemInfo();
            
            if (systemInfoResult.Success)
            {
                TimeDiff = DateTime.UtcNow - systemInfoResult.Data.CurrentTime;
                this.SystemInfo = systemInfoResult.Data;
                UpdateTime();
            }
                
        }
        catch (Exception)
        {
        }
        finally
        {
            LastUpdated = DateTime.UtcNow;
            Refreshing = false;
        }
    }

    Task<RequestResult<SystemInfo>> GetSystemInfo() => HttpHelper.Get<SystemInfo>("/api/system/info");

    /// <inheritdoc />
    public async Task Toggle()
    {
        bool paused = SystemInfo.IsPaused;
        int duration = 0;
        if (paused == false)
        {
            duration = await PausePrompt.Show();
            if (duration < 1)
                return;

        }

        await SetPausedState(duration);
    }

    /// <inheritdoc />
    public async Task Pause()
    {
        int duration = await PausePrompt.Show();
        if (duration < 1)
            return;

        await SetPausedState(duration);
    }

    /// <inheritdoc />
    public Task Resume()
        => SetPausedState(0);

    /// <summary>
    /// Pauses the system for the given amount of seconds
    /// </summary>
    /// <param name="duration">the duration in seconds</param>
    private async Task SetPausedState(int duration)
    {
        if (duration == 0)
        {
            if (await Confirm.Show("Dialogs.ResumeDialog.Title", "Dialogs.ResumeDialog.Message") == false)
                return;
        }
        await HttpHelper.Post($"/api/system/pause?duration=" + duration);
        var systemInfoResult = await GetSystemInfo();
        if (systemInfoResult.Success)
        {
            TimeDiff = DateTime.UtcNow - systemInfoResult.Data.CurrentTime;
            SystemInfo = systemInfoResult.Data;
            this.UpdateTime();
            
            if(duration == 0)
                OnResume?.Invoke();
            else
                OnPaused?.Invoke();
            OnPausedLabelChanged?.Invoke(this.PausedLabel);

        }
    }

    public event OnPausedLabelChangedEventHandler OnPausedLabelChanged;
    public event OnPausedEventHandler OnPaused;
    public event OnResumeEventHandler OnResume;
}