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
    /// Gets or sets the description of the flow
    /// </summary>
    public string Description { get; set; }

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
}

/// <summary>
/// A model for a flow templates
/// </summary>
public class FlowTemplateModel
{
    /// <summary>
    /// Gets or sets the flow
    /// </summary>
    public Flow Flow { get; set; }
    
    /// <summary>
    /// Gets or sets fields used in the template
    /// </summary>
    public List<TemplateField> Fields { get; set; }

    /// <summary>
    /// Gets or sets the order this template appears
    /// </summary>
    public int? Order { get; set; }
    
    /// <summary>
    /// Gets or sets if this flow template should be saved after creation
    /// </summary>
    public bool Save { get; set; }

    /// <summary>
    /// Gets or sets the type of flow
    /// </summary>
    public FlowType Type { get; set; }
}

/// <summary>
/// A field used in templates
/// </summary>
public class TemplateField
{
    /// <summary>
    /// Gets or sets the UID of the template field
    /// </summary>
    public Guid Uid { get; set; }
    
    /// <summary>
    /// Gets or sets the type of the field
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Gets or sets if this field is required
    /// </summary>
    public bool Required { get; set; }
    
    /// <summary>
    /// Gets or sets the name of this field
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the label for this field
    /// </summary>
    public string Label { get; set; }
    
    /// <summary>
    /// Gets or sets the help text for this field
    /// </summary>
    public string Help { get; set; }
    
    /// <summary>
    /// Gets or sets the default value for this field
    /// </summary>
    public object Default { get; set; }
    
    /// <summary>
    /// Gets or sets the value of this field
    /// </summary>
    public object Value { get; set; }
    
    /// <summary>
    /// Gets or sets the parameters for this field
    /// </summary>
    public object Parameters { get; set; }

    /// <summary>
    /// Gets or sets teh conditions of the field
    /// </summary>
    public List<Condition> Conditions { get; set; }
}

/// <summary>
/// Model used in the flow list page
/// </summary>
public class FlowListModel: IInUse, IUniqueObject<Guid>
{
    /// <summary>
    /// Gets or sets the UID
    /// </summary>
    public Guid Uid { get; set; }
    
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type of flow
    /// </summary>
    public FlowType Type { get; set; }

    /// <summary>
    /// Gets or sets if this is the default failure flow
    /// </summary>
    public bool Default { get; set; }
    
    /// <summary>
    /// Gets or sets what is using this model
    /// </summary>
    public List<ObjectReference> UsedBy { get; set; }
}