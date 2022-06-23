namespace FileFlows.Server.Models;

/// <summary>
/// Time it took to process a library file
/// </summary>
public class LibraryFileProcessingTime
{
    /// <summary>
    /// Gets or sets the name of the library
    /// </summary>
    public string Library { get; set; }
    /// <summary>
    /// Gets or sets the original size of the file
    /// </summary>
    public long OriginalSize { get; set; }
    /// <summary>
    /// Gets or sets how many seconds it took to process
    /// </summary>
    public int Seconds { get; set; }
}