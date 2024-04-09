
using FileFlows.Managers.InitializationManagers;
using FileFlows.Server.Services;
using FileFlows.Shared.Models;

namespace FileFlows.Server.Upgrade;

        
/// <summary>
/// Run upgrade from 24.04.1
/// </summary>
public class Upgrade_24_04_1 : UpgradeBase
{
    /// <summary>
    /// Initializes the upgrade for 24.04.1
    /// </summary>
    /// <param name="logger">the logger</param>
    /// <param name="settingsService">the settings service</param>
    /// <param name="upgradeManager">the upgrade manager</param>
    public Upgrade_24_04_1(FileFlows.Plugin.ILogger logger, AppSettingsService settingsService, UpgradeManager upgradeManager)
        : base(logger, settingsService, upgradeManager)
    {
    }
    
    /// <summary>
    /// Runs the upgrade
    /// </summary>
    public void Run()
    {
        UpgradeManager.Run_Upgrade_24_04_1(Logger, DbType, ConnectionString);
    }
}