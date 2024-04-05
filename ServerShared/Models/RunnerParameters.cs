namespace FileFlows.ServerShared.Models;

/// <summary>
/// Parameters passed to flow runner
/// </summary>
public class RunnerParameters
{
    /// <summary>
    /// Gets or sets the UID of the runner
    /// </summary>
    public Guid Uid { get; set; }
    /// <summary>
    /// Gets or sets the UID of the node
    /// </summary>
    public Guid NodeUid { get; set; }
    /// <summary>
    /// Gets or sets the UID of the node to include in the remote calls
    /// </summary>
    public Guid RemoteNodeUid { get; set; }
    /// <summary>
    /// Gets or sets the UID of the library file
    /// </summary>
    public Guid LibraryFile { get; set; }
    /// <summary>
    /// Gets or sets the temporary path
    /// </summary>
    public string TempPath { get; set; }
    /// <summary>
    /// Gets or sets the configuration path
    /// </summary>
    public string ConfigPath { get; set; }
    /// <summary>
    /// Gets or sets the configuration encryption key
    /// </summary>
    public string ConfigKey { get; set; }
    /// <summary>
    /// Gets or sets the base URL for the FileFlows server
    /// </summary>
    public string BaseUrl { get; set; }
    /// <summary>
    /// Gets or sets the API token 
    /// </summary>
    public string ApiToken { get; set; }
    /// <summary>
    /// Gets or sets if running inside docker 
    /// </summary>
    public bool IsDocker { get; set; }
    /// <summary>
    /// Gets or sets the hostname
    /// </summary>
    public string? Hostname { get; set; }
    /// <summary>
    /// Gets or sets if is the internal processing node 
    /// </summary>
    public bool IsInternalServerNode { get; set; }
}