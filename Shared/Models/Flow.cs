using FileFlows.Plugin;

namespace FileFlows.Shared.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// A flow 
/// </summary>
public class Flow : FileFlowObject
{
    /// <summary>
    /// Gets or sets if the flow is enabled
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Gets or sets the revision of this flow
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets the type of flow
    /// </summary>
    public FlowType Type { get; set; }

    /// <summary>
    /// Gets or sets the template this flow is based on
    /// </summary>
    public string Template { get; set; }
    
    /// <summary>
    /// Gets or sets the parts of this flow
    /// </summary>
    public List<FlowPart> Parts { get; set; }

    /// <summary>
    /// Gets or sets if this is the default failure flow
    /// </summary>
    public bool Default { get; set; }

    public FlowProperties _Properties = new();

    /// <summary>
    /// Gets or sets the advanced properties of this flow
    /// </summary>
    public FlowProperties Properties
    {
        get => _Properties;
        set => _Properties = value ?? new();
    }
}
