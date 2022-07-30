﻿using FileFlows.Server.Helpers;
using FileFlows.Shared.Formatters;

namespace FileFlows.Server.Workers;

using FileFlows.Server.Controllers;
using FileFlows.ServerShared.Workers;
using FileFlows.Shared;
using FileFlows.Shared.Helpers;

/// <summary>
/// A worker that automatically updates FileFlows
/// </summary>
public class ServerUpdater : UpdaterWorker
{
    private static string UpdateUrl = "https://fileflows.com/auto-update";

    internal static ServerUpdater Instance;
    private Version? NotifiedUpdateVersion;
    private Version? DownloadedVersion;
    
    /// <summary>
    /// Creates an instance of a worker to automatically update FileFlows Server
    /// </summary>
    public ServerUpdater() : base("server-upgrade", ScheduleType.Daily, 4) 
    {
        Instance = this;
    }

    /// <summary>
    /// Pre-check to run before executing
    /// </summary>
    /// <returns>if false, no update will be checked for</returns>
    protected override bool PreCheck() => LicenseHelper.IsLicensed(LicenseFlags.AutoUpdates);
    
    protected override void Initialize(ScheduleType schedule, int interval)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("AutoUpdateInterval") ?? string.Empty, out int minutes) &&
            minutes > 0)
        {
            Logger.Instance?.DLog($"{nameof(ServerUpdater)}: Using Auto Update Interval: " + minutes + " minute" + (minutes == 1 ? "" : "s"));
            interval = minutes;
            schedule = ScheduleType.Minute;
        }

        var updateUrl = Environment.GetEnvironmentVariable("AutoUpdateUrl");
        if (string.IsNullOrEmpty(updateUrl) == false)
        {
            if (updateUrl.EndsWith("/"))
                updateUrl = updateUrl[..^1];
            Logger.Instance?.DLog($"{nameof(ServerUpdater)}: Using Auto Update URL: " + updateUrl);
            UpdateUrl = updateUrl;
        }
        base.Initialize(schedule, interval);
    }

    protected override void QuitApplication()
    {
        Logger.Instance.ILog($"{nameof(ServerUpdater)} - Exiting Application to run update");
        WorkerManager.StopWorkers();
        // systemd needs an OK status not to auto restart, we dont want to auto restart that when upgrading
        Environment.Exit(Globals.IsSystemd ? 0 : 99);  
    }
    
    protected override bool GetAutoUpdatesEnabled()
    {
        var settings = new SettingsController().Get().Result;
        return settings?.AutoUpdate == true;
    }

    protected override bool CanUpdate()
    {
        var workers = new WorkerController(null).GetAll();
        return workers?.Any() != true;
    }

    protected override void PrepareApplicationShutdown()
    {
        WorkerManager.StopWorkers();
    }

    protected override void PreRunUpdateScript(string updateScript)
    {
        if(DownloadedVersion != null)
            SystemEvents.TriggerServerUpdating(DownloadedVersion.ToString());
    }

    protected override bool GetUpdateAvailable()
    {
        var result = GetLatestOnlineVersion();
        if (result.updateAvailable)
        {
            if (NotifiedUpdateVersion != result.onlineVersion)
            {
                SystemEvents.TriggerServerUpdateAvailable(result.onlineVersion.ToString());
                NotifiedUpdateVersion = result.onlineVersion;
            }
        }
        return result.updateAvailable;
    }

    protected override string DownloadUpdateBinary()
    {
        var result = GetLatestOnlineVersion();
        if (result.updateAvailable == false)
            return string.Empty;
        
        Version onlineVersion = result.onlineVersion;

        string updateDirectory = Path.Combine(DirectoryHelper.BaseDirectory, "Update");

        string file = Path.Combine(updateDirectory, $"FileFlows-{onlineVersion}.zip");
        if (File.Exists(file))
        {
            string size = FileSizeFormatter.Format(new FileInfo(file).Length);
            Logger.Instance.ILog($"{UpdaterName}: Update already downloaded: {file} ({size}");
            return file;
        }

        if (Directory.Exists(updateDirectory))
            Directory.Delete(updateDirectory, true);
        Directory.CreateDirectory(updateDirectory);

        Logger.Instance.ILog($"{UpdaterName}: Downloading update: " + onlineVersion);
        
        
        string url = $"{UpdateUrl}/download/{onlineVersion}?ts={DateTime.Now.Ticks}";
        HttpHelper.DownloadFile(url, file).Wait();
        if (File.Exists(file) == false)
        {
            Logger.Instance.WLog($"{UpdaterName}: Download failed");
            return string.Empty;
        }

        string dlSize = FileSizeFormatter.Format(new FileInfo(file).Length);
        Logger.Instance.ILog($"{UpdaterName}: Download complete: {file} ({dlSize})");
        DownloadedVersion = result.onlineVersion;
        return file;
    }

    /// <summary>
    /// Gets the latest version available online
    /// </summary>
    /// <returns>The latest version available online</returns>
    public static (bool updateAvailable, Version onlineVersion) GetLatestOnlineVersion()
    {
        try
        {
            string url = UpdateUrl + "/latest-version";
            var result = HttpHelper.Get<string>(url, noLog: true).Result;
            if (result.Success == false)
            {
                Logger.Instance.ILog($"{nameof(ServerUpdater)}: Failed to retrieve online version");
                return (false, new Version(0, 0, 0, 0));
            }

            Version current = Globals.Version;
            Version? onlineVersion;
            if (Version.TryParse(result.Data, out onlineVersion) == false)
            {
                Logger.Instance.ILog($"{nameof(ServerUpdater)}: Failed to parse online version: " + result.Data);
                return (false, new Version(0, 0, 0, 0));
            }

            if (current >= onlineVersion)
            {
                Logger.Instance.ILog(
                    $"{nameof(ServerUpdater)}: Current version '{current}' newer or same as online version '{onlineVersion}'");
                return (false, onlineVersion);
            }

            return (true, onlineVersion);
        }
        catch (Exception ex)
        {
            Logger.Instance.ELog($"{nameof(ServerUpdater)}: Failed checking online version: " + ex.Message);
            return (false, new Version(0, 0, 0, 0));
        }
    }
}
