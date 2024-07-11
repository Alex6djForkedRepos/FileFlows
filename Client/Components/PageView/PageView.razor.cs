using Microsoft.AspNetCore.Components;
using FileFlows.Client.Shared;

namespace FileFlows.Client.Components;

/// <summary>
/// Page view 
/// </summary>
public partial class PageView
{
    /// <summary>
    /// Gets or sets the nav menu component
    /// </summary>
    [CascadingParameter] public NavMenu Menu { get; set; }

    /// <summary>
    /// Gets or sets any head fragment to render
    /// </summary>
    [Parameter] public RenderFragment Head { get; set; }
    
    /// <summary>
    /// Gets or sets the left head fragment to render
    /// </summary>
    [Parameter] public RenderFragment HeadLeft { get; set; }

    /// <summary>
    /// Gets or sets the body fragment to render
    /// </summary>
    [Parameter] public RenderFragment Body { get; set; }

    /// <summary>
    /// Gets or sets if the page view is full width
    /// </summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>
    /// Gets or sets the title of the page view
    /// </summary>
    [Parameter] public string Title { get; set; }

    /// <summary>
    /// Gets or sets if this page view should add the flex class
    /// </summary>
    [Parameter] public bool Flex { get; set; }

    /// <summary>
    /// Gets or sets additional class names to add to the ViContainer
    /// </summary>
    [Parameter] public string ClassName { get; set; }
    
    /// <summary>
    /// Gets or sets the icon to use, if not set the icon from the nav menu will be used
    /// </summary>
    [Parameter] public string? Icon { get; set; }

}