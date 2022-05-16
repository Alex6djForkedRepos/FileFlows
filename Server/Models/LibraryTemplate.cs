﻿using FileFlows.Shared.Models;
using System.Dynamic;

namespace FileFlows.Server.Models;

/// <summary>
/// A library template
/// </summary>
class LibraryTemplate
{
    /// <summary>
    /// Gets or sets the name of the library template
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the group this template belongs to
    /// </summary>
    public string Group { get; set; }
    
    /// <summary>
    /// Gets or sets the description of this template
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Gets or sets the filter for this template
    /// </summary>
    public string Filter { get; set; }
    
    /// <summary>
    /// Gets or sets the path for this template
    /// </summary>
    public string Path { get; set; }
    
    /// <summary>
    /// Gets or sets the scan interval for this template
    /// </summary>
    public int ScanInterval { get; set; }

    /// <summary>
    /// Gets or sets the priority of this template
    /// </summary>
    public ProcessingPriority Priority { get; set; }
    
    /// <summary>
    /// Gets or sets the period to detect file size changes for this template
    /// </summary>
    public int FileSizeDetectionInterval { get; set; }
    
    /// <summary>
    /// Gets or sets if recreated files (files with a different creation time) should be automatically reprocessed
    /// This is helpful if you download the same file multiple times and want to reprocess it again
    /// </summary>
    public bool ReprocessRecreatedFiles { get; set; }
}
