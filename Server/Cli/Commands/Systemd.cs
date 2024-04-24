using ILogger = FileFlows.Plugin.ILogger;

namespace FileFlows.Server.Cli.Commands;

/// <summary>
/// Command that installs or uninstalls the systemd service
/// </summary>
public class Systemd : Command
{

    /// <inheritdoc />
    public override string Switch => "systemd";
    
    /// <inheritdoc />
    public override string Description => "Installs or uninstalls the Systemd service, only works on Linux";
    
    /// <summary>
    /// Gets or sets the mode
    /// </summary>
    [CommandLineArg("", "install to install the service on uninstall to remove it")]
    public string Mode { get; set; }
    
    /// <inheritdoc />
    public override bool Run(ILogger logger)
    {
        if (Globals.IsLinux == false)
        {
            logger.ELog("Only available on Linux systems");
            return true;
        }
            
        string mode = (Mode ?? string.Empty).TrimStart('-').ToLowerInvariant();
        if (mode == "install")
        {
            FileFlows.ServerShared.Helpers.SystemdService.Install(DirectoryHelper.BaseDirectory, isNode: false);
            return true;
        }

        if (mode == "uninstall")
        {
            FileFlows.ServerShared.Helpers.SystemdService.Uninstall(false);
            return true;
        }
        
        logger.ELog("Use install or uninstall to install the service");
        return true;
    }

    
}