using FileFlows.ServerShared.Services;

namespace FileFlows.ServerShared.Workers;

/// <summary>
/// Worker to clean up temporary files
/// </summary>
public class TempFileCleaner:Worker
{
    private string nodeAddress;
    
    /// <summary>
    /// Constructs a temp file cleaner
    /// <param name="nodeAddress">The name of the node</param>
    /// </summary>
    public TempFileCleaner(string nodeAddress) : base(ScheduleType.Daily, 5)
    {
        this.nodeAddress = nodeAddress;
        Trigger();
    }

    /// <summary>
    /// Executes the cleaner
    /// </summary>
    protected sealed override void Execute()
    {
        var service = NodeService.Load();
        var node = string.IsNullOrWhiteSpace(nodeAddress)
            ? service.GetServerNode().Result
            : service.GetByAddress(nodeAddress).Result;
        if (string.IsNullOrWhiteSpace(node?.TempPath))
            return;
        var tempDir = new DirectoryInfo(node.TempPath);
        if (tempDir.Exists == false)
            return;
        
        Logger.Instance?.ILog("About to clean temporary directory: " + tempDir.FullName);
        foreach (var dir in tempDir.GetDirectories())
        {
            if (dir.CreationTime < DateTime.Now.AddDays(-1))
            {
                try
                {
                    dir.Delete(recursive: true);
                    Logger.Instance.ILog($"Deleted directory '{dir.Name}' from temp directory");
                }
                catch (Exception ex)
                {
                    Logger.Instance.WLog($"Failed to delete directory '{dir.Name}' from temp directory: " + ex.Message);
                }
            }
        }
    }
}