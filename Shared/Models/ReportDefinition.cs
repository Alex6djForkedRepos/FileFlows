namespace FileFlows.Shared.Models;

/// <summary>
/// Definition for a report
/// </summary>
public class ReportDefinition : IUniqueObject<Guid>
{
    /// <summary>
    /// Gets or 
    /// </summary>
    public Guid Uid { get; set; }
    
    /// <summary>
    /// Gets or sets the type of report
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Gets or sets the icon
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets the default report period for this report if it supports a period
    /// </summary>
    public ReportPeriod? DefaultReportPeriod { get; set; }

    /// <summary>
    /// Gets the flow selection for this report
    /// </summary>
    public ReportSelection FlowSelection { get; set; }
    
    /// <summary>
    /// Gets the library selection for this report
    /// </summary>
    public ReportSelection LibrarySelection { get; set; }
    
    /// <summary>
    /// Gets the node selection for this report
    /// </summary>
    public ReportSelection NodeSelection { get; set; }

    /// <summary>
    /// Gets the tag selection for this report
    /// </summary>
    public ReportSelection TagSelection { get; set; }
    
    /// <summary>
    /// Gets or set s if the resolution should be an option on this report
    /// </summary>
    public ReportSelection ResolutionSelection { get; set; }
    
    /// <summary>
    /// Gets or sets if the IO Direction is shown
    /// </summary>
    public bool Direction { get; set; }
    
    /// <summary>
    /// Gets or sets the fields in this template
    /// </summary>
    public List<TemplateField> Fields { get; set; }
}

/// <summary>
/// Report selection
/// </summary>
public enum ReportSelection
{
    /// <summary>
    /// None, do not show this selection on this report
    /// </summary>
    None,
    /// <summary>
    /// One of these must be selected
    /// </summary>
    One,
    /// <summary>
    /// Any of these can be selected
    /// </summary>
    Any,
    /// <summary>
    /// Any or all
    /// </summary>
    AnyOrAll 
}


/// <summary>
/// Enumeration representing different report periods.
/// </summary>
public enum ReportPeriod
{
    /// <summary>
    /// Any period.
    /// </summary>
    Any = 0,

    /// <summary>
    /// Last 24 hours period.
    /// </summary>
    Last24Hours,

    /// <summary>
    /// Today's period.
    /// </summary>
    Today,

    /// <summary>
    /// Yesterday's period.
    /// </summary>
    Yesterday,

    /// <summary>
    /// Last 7 days period.
    /// </summary>
    Last7Days,

    /// <summary>
    /// Last 31 days period.
    /// </summary>
    Last31Days,

    /// <summary>
    /// Last 3 months period.
    /// </summary>
    Last3Months,

    /// <summary>
    /// Last 6 months period.
    /// </summary>
    Last6Months,

    /// <summary>
    /// Last 12 months period.
    /// </summary>
    Last12Months
}
