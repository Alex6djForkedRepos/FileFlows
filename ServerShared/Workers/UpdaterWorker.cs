using System.Diagnostics;
using System.IO.Compression;

namespace FileFlows.ServerShared.Workers;

/// <summary>
/// Worker that will automatically update the system
/// </summary>
public abstract class UpdaterWorker : Worker
{
    /// <summary>
    /// Gets if there is an updated pending installation
    /// </summary>
    public static bool UpdatePending { get; private set; }

    protected Version CurrentVersion { get; init; }

    protected readonly string UpdaterName;

    private readonly string UpgradeScriptPrefix;

    /// <summary>
    /// Constructs an instance of a Update Worker
    /// </summary>
    /// <param name="upgradeScriptPrefix">The script to execute in the upgrade zip file</param>
    /// <param name="schedule">the type of schedule this worker runs at</param>
    /// <param name="interval">the interval of this worker</param>
    public UpdaterWorker(string upgradeScriptPrefix, ScheduleType schedule, int interval) : base(schedule, interval)
    {
        CurrentVersion = Version.Parse(Globals.Version);
        this.UpgradeScriptPrefix = upgradeScriptPrefix;
        UpdaterName = this.GetType().Name;
        RunCheck();
    }

    protected override void Execute()
    {
        RunCheck();
    }

    /// <summary>
    /// Gets if the update can run
    /// </summary>
    protected abstract bool CanUpdate();

    /// <summary>
    /// Quits the current application
    /// </summary>
    protected abstract void QuitApplication();

    /// <summary>
    /// Pre-check to run before executing
    /// </summary>
    /// <returns>if false, no update will be checked for</returns>
    protected virtual bool PreCheck() => true;
    
    /// <summary>
    /// Runs a check for update and if found will download it 
    /// </summary>
    /// <returns>A update has been downloaded</returns>
    public bool RunCheck()
    {
        if (PreCheck() == false)
            return false;

        Logger.Instance?.ILog($"{UpdaterName}: Checking for update");
        try
        {
#if(DEBUG)
            return false; // disable during debugging
#else
            string updateScript = DownloadUpdate();
            if (string.IsNullOrEmpty(updateScript))
                return false;

            UpdatePending = true;
            PrepareApplicationShutdown();
            Logger.Instance.ILog($"{UpdaterName}: Update pending installation");
            do
            {
                Logger.Instance.ILog($"{UpdaterName}: Waiting to run update");
                // sleep just in case something has just started
                Thread.Sleep(10_000);
            } while (CanUpdate() == false);

            Logger.Instance.ILog($"{UpdaterName} - Update about to be installed");
            RunUpdateScript(updateScript);
            return true;
#endif
        }
        catch (Exception ex)
        {
            Logger.Instance?.ELog($"{UpdaterName} Error: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Prepares the application to be shutdown
    /// Called after the update has been downloaded, but before it has run
    /// </summary>
    protected virtual void PrepareApplicationShutdown()
    {
    }

    private void RunUpdateScript(string updateScript)
    {
        try
        {
            // if inside docker or systemd we just restart, the restart policy should automatically kick in then run the upgrade script when it starts
            if (Globals.IsDocker == false && Globals.IsSystemd == false)
            {
                Logger.Instance?.ILog($"{UpdaterName}About to execute upgrade script: " + updateScript);
                var fi = new FileInfo(updateScript);

                var psi = new ProcessStartInfo(updateScript);
                psi.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
                psi.WorkingDirectory = fi.DirectoryName;
                psi.UseShellExecute = true;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            
            QuitApplication();
        }
        catch (Exception ex)
        {
            Logger.Instance?.ELog($"{UpdaterName}: Failed running update script: " + ex.Message);
        }
    }

    /// <summary>
    /// Downloads an update
    /// </summary>
    /// <returns>The update file</returns>
    protected abstract string DownloadUpdateBinary();

    /// <summary>
    /// Gets if auto updates are enabled
    /// </summary>
    /// <returns>if auto updates are enabled</returns>
    protected abstract bool GetAutoUpdatesEnabled();

    private string DownloadUpdate()
    {
        try
        {
            if (GetAutoUpdatesEnabled() == false)
                return string.Empty;

            Logger.Instance?.DLog($"{UpdaterName}: Checking for new update binary");
            string update = DownloadUpdateBinary();
            if (string.IsNullOrEmpty(update))
            {
                Logger.Instance?.DLog($"{UpdaterName}: No update available");
                return string.Empty;
            }

            Logger.Instance?.DLog($"{UpdaterName}: Downloaded update: " + update);

            var updateDir = new FileInfo(update).DirectoryName;

            Logger.Instance?.ILog($"{UpdaterName}: Extracting update to: " + updateDir);
            try
            {
                ZipFile.ExtractToDirectory(update, updateDir, true);
            }
            catch (Exception)
            {
                Logger.Instance?.ELog($"{UpdaterName}: Failed extract update zip, file likely corrupt during download, deleting update");
                File.Delete(update);
                return string.Empty;
            }

            Logger.Instance?.ILog($"{UpdaterName}: Extracted update to: " + updateDir);
            // delete the upgrade file after extraction
            File.Delete(update);
            Logger.Instance?.ILog($"{UpdaterName}: Deleted update file: " + update);

            var updateFile = Path.Combine(updateDir, UpgradeScriptPrefix + (Globals.IsWindows ? ".bat" : ".sh"));
            if (File.Exists(updateFile) == false)
            {
                Logger.Instance?.WLog($"{UpdaterName}: No update script found: " + updateFile);
                return string.Empty;
            }

            Logger.Instance?.ILog($"{UpdaterName}: Update script found: " + updateFile);

            if (Globals.IsLinux && FileHelper.MakeExecutable(updateFile) == false)
            {
                Logger.Instance?.WLog($"{UpdaterName}: Failed to make update script executable");
                return string.Empty;
            }

            Logger.Instance?.ILog($"{UpdaterName}: Upgrade directory ready: " + updateDir);
            Logger.Instance?.ILog($"{UpdaterName}: Upgrade script ready: " + updateFile);

            return updateFile;
        }
        catch (Exception ex)
        {
            //if (ex.Message == "Object reference not set to an instance of an object")
            //    return string.Empty; // just ignore this error, likely due ot it not being configured yet.
            Logger.Instance?.ELog($"{UpdaterName}: Failed checking for update: " + ex.Message);
            return string.Empty;
        }
    }

}