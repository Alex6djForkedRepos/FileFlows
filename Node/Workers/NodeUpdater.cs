using FileFlows.ServerShared;
using FileFlows.ServerShared.Helpers;
using FileFlows.ServerShared.Services;
using FileFlows.ServerShared.Workers;

namespace FileFlows.Node.Workers;

/// <summary>
/// Worker to automatically download node updates from the FileFlows server
/// </summary>
public class NodeUpdater:UpdaterWorker
{
    private static NodeUpdater? Instance;
    
    /// <summary>
    /// Constructs an instance of the node updater worker
    /// </summary>
    public NodeUpdater() : base("node-upgrade", ScheduleType.Daily, 3)
    {
        Instance = this;
    }

    /// <summary>
    /// Checks for an update now
    /// </summary>
    internal static void CheckForUpdate()
    {
        if (Instance == null)
            return;
        Instance.Trigger();
    }

    
    /// <summary>
    /// Gets if an update can currently run
    /// </summary>
    /// <returns>true if the update can run, otherwise false</returns>
    protected override bool CanUpdate() => FlowWorker.HasActiveRunners == false;

    /// <summary>
    /// Quits the application so the update can be applied
    /// </summary>
    protected override void QuitApplication()
    {
        Logger.Instance?.ILog($"{UpdaterName}: Quiting Application");
        // systemd needs an OK status not to auto restart, we dont want to auto restart that when upgrading
        Program.Quit(Globals.IsSystemd ? 0 : 99);
    }

    /// <summary>
    /// Downloads the binary update from the FileFlows server
    /// </summary>
    /// <returns>the downloaded binary filename</returns>
    protected override string DownloadUpdateBinary()
    {   
        Logger.Instance.DLog("Checking for auto update");
        var systemService = SystemService.Load();
        var serverVersion = systemService.GetNodeUpdateVersion().Result;
        Logger.Instance.DLog("Checking for auto update: " + serverVersion);
        if (serverVersion <= CurrentVersion)
            return string.Empty;

        Logger.Instance.ILog($"New Node version {serverVersion} detected, starting download");

        var data = systemService.GetNodeUpdater().Result;
        if (data?.Any() != true)
        {
            Logger.Instance.WLog("Failed to download Node updater.");
            return string.Empty;
        }

        var updateDir = Path.Combine(DirectoryHelper.BaseDirectory, "NodeUpdate");
        if (Directory.Exists(updateDir)) // delete the update dir so we get a full fresh update
            Directory.Delete(updateDir, true);
        Directory.CreateDirectory(updateDir);

        string update = Path.Combine(updateDir, "update.zip");
        File.WriteAllBytes(update, data);
        return update;
    }

    /// <summary>
    /// Gets if automatic updates should be downloaded
    /// </summary>
    /// <returns>true if automatic updates are enabled</returns>
    protected override bool GetAutoUpdatesEnabled()
    {
        var settingsService = SettingsService.Load();
        var settings = settingsService.Get().Result;
        return settings?.AutoUpdateNodes == true;
    }

    protected override bool GetUpdateAvailable()
    {
        var systemService = SystemService.Load();
        var serverVersion = systemService.GetNodeUpdateVersion().Result;
        Logger.Instance.DLog("Checking for auto update: " + serverVersion);
        return serverVersion > CurrentVersion;
    }
}