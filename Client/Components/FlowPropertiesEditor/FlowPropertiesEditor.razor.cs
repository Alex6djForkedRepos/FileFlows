﻿using System.Text;
using System.Text.Json;
using FileFlows.Client.Components.Dialogs;
using FileFlows.Client.Pages;
using FileFlows.Plugin;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Flow = FileFlows.Shared.Models.Flow;

namespace FileFlows.Client.Components;

/// <summary>
/// Flow Properties editor
/// </summary>
public partial class FlowPropertiesEditor
{
    private List<FlowField> Fields => Flow.Properties.Fields;
    /// <summary>
    /// Gets or sets the JavaScript runtime
    /// </summary>
    [Inject] IJSRuntime jsRuntime { get; set; }
    
    /// <summary>
    /// Gets or sets the flow editor currently opened
    /// </summary>
    [Parameter] public FlowEditor FlowEditor { get; set; }

    /// <summary>
    /// Gets or sets the flow
    /// </summary>
    public Flow Flow => FlowEditor?.Flow;

    private FlowField Editing;
    protected string lblClose, lblHelp, lblTitle, lblSubFlowHelp;
    
    /// <summary>
    /// Gets or sets if this is visible
    /// </summary>
    public bool Visible { get; private set; }

    private List<KeyValuePair<string, string>> _FlowVariables;
    private List<KeyValuePair<string, string>> FlowVariables
    {
        get => _FlowVariables;
        set
        {
            _FlowVariables = value ?? new ();
            Flow.Properties.Variables = _FlowVariables.ToDictionary<KeyValuePair<string, string>, string, object>(x => x.Key, x =>
            {
                if (int.TryParse(x.Value, out int iValue))
                    return iValue;
                if (bool.TryParse(x.Value, out bool bValue))
                    return bValue;
                return x.Value;
            });
        }
    }

    protected override void OnInitialized()
    {
        lblTitle = Translater.Instant("Pages.Flow.Labels.FlowProperties");
        lblClose = Translater.Instant("Labels.Close");
        lblHelp = Translater.Instant("Labels.Help");
        lblSubFlowHelp  = Translater.Instant("Pages.Flow.Labels.SubFlowHelp");
        _FlowVariables = Flow.Properties.Variables?.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString()))
            ?.ToList() ?? new ();
        foreach (var field in Flow.Properties.Fields)
        {
            if (field.DefaultValue is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                    field.DefaultValue = jsonElement.GetInt32();
                else if (jsonElement.ValueKind == JsonValueKind.False)
                    field.DefaultValue = false;
                else if (jsonElement.ValueKind == JsonValueKind.True)
                    field.DefaultValue = true;
                else
                    field.DefaultValue = jsonElement.GetString();
            }
        }
    }

    /// <summary>
    /// Closes the properties editor
    /// </summary>
    public void Close()
    {
        Visible = false;
        StateHasChanged();
    }

    /// <summary>
    /// Shows the the editor
    /// </summary>
    public void Show()
    {
        Visible = true;
        StateHasChanged();
    }

    // Opens the helper
    void OpenHelp()
        => _ = jsRuntime.InvokeVoidAsync("open", "https://fileflows.com/docs/pages/flows/properties", "_blank");

    /// <summary>
    /// Adds a new property variable
    /// </summary>
    void Add()
    {
        Fields.Add(new());
        MarkDirty();
    }

    /// <summary>
    /// Edits a field
    /// </summary>
    /// <param name="item">the field to edit</param>
    void Edit(FlowField item)
        => Editing = item;

    /// <summary>
    /// Deletes a field
    /// </summary>
    /// <param name="item">the field to delete</param>
    async Task Delete(FlowField item)
    {
        if (await Confirm.Show("Labels.Delete", "Are you sure you want to delete this field?") == false)
            return;
        Fields.Remove(item);
        MarkDirty();
        StateHasChanged();
    }

    /// <summary>
    /// Moves an item up or down in the list
    /// </summary>
    /// <param name="item">the item to move</param>
    /// <param name="up">true to move up, false to move down</param>
    async Task Move(FlowField item, bool up)
    {
        // Fields == List<>
        var index = Fields.IndexOf(item);
        if (index < 0)
        {
            // Item not found in the list, do nothing
            return;
        }

        // Calculate the new index after moving up or down
        var newIndex = up ? index - 1 : index + 1;

        if (newIndex < 0 || newIndex >= Fields.Count)
        {
            // If the new index is out of range, do nothing
            return;
        }

        // Remove the item from its current position
        Fields.RemoveAt(index);

        // Insert the item at the new position
        Fields.Insert(newIndex, item);
        MarkDirty();

        await Task.Delay(50);
        Editing = item;
        StateHasChanged();
    }

    /// <summary>
    /// Gets or sets the the default string value
    /// </summary>
    public string DefaultValueString
    {
        get => Editing?.DefaultValue as string ?? string.Empty;
        set
        {
            if (Editing?.Type is FlowFieldType.String or FlowFieldType.Directory or FlowFieldType.Select)
            {
                Editing.DefaultValue = value;
                MarkDirty();
            }
        } 
    }

    /// <summary>
    /// Gets or sets the default boolean value
    /// </summary>
    public bool DefaultValueBoolean
    {
        get => Editing?.DefaultValue as bool? == true;
        set
        {
            if (Editing?.Type == FlowFieldType.Boolean)
            {
                Editing.DefaultValue = value;
                MarkDirty();
            }
        } 
    }
    /// <summary>
    /// Gets or sets the default number value
    /// </summary>
    public int DefaultValueNumber
    {
        get => Editing?.DefaultValue as int? ?? 0;
        set
        {
            if (Editing?.Type is FlowFieldType.Number or FlowFieldType.Slider)
            {
                Editing.DefaultValue = value;
                MarkDirty();
            }
        } 
    }
    /// <summary>
    /// Gets or sets the int minimum
    /// </summary>
    public int IntMinValue
    {
        get => Editing.IntMinimum;
        set
        {
            if (Editing?.Type is FlowFieldType.Number or FlowFieldType.Slider)
            {
                Editing.IntMinimum = value;
                MarkDirty();
            }
        } 
    }
    /// <summary>
    /// Gets or sets the int maximum
    /// </summary>
    public int IntMaxValue
    {
        get => Editing.IntMaximum;
        set
        {
            if (Editing?.Type is FlowFieldType.Number or FlowFieldType.Slider)
            {
                Editing.IntMaximum = value;
                MarkDirty();
            }
        } 
    }

    /// <summary>
    /// Gets or sets if the value input should be inversed
    /// </summary>
    public bool Inverse
    {
        get => Editing.Inverse;
        set { Editing.Inverse = value; MarkDirty(); }
    }

    /// <summary>
    /// Marks the current flow editor as dirty and needed to be saved
    /// </summary>
    private void MarkDirty()
    {
        FlowEditor?.MarkDirty();
    }
}
