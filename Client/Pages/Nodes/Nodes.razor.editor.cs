﻿using System.ComponentModel;
using FileFlows.Client.Components.Inputs;
using FileFlows.Plugin;

namespace FileFlows.Client.Pages;

public partial class Nodes : ListPage<Guid, ProcessingNode>
{

    public override async Task<bool> Edit(ProcessingNode node)
    {
#if (!DEMO)

        bool isServerProcessingNode = node.Address == FileFlowsServer;
        node.Mappings ??= new();
        this.EditingItem = node;

        var scripts = (await HttpHelper.Get<List<Script>>("/api/script")).Data ?? new List<Script>();

        var tabs = new Dictionary<string, List<ElementField>>();
        tabs.Add("General", TabGeneral(node, isServerProcessingNode, scripts));
        tabs.Add("Schedule", TabSchedule(node, isServerProcessingNode));
        if(isServerProcessingNode == false)
            tabs.Add("Mappings", TabMappings(node));
        tabs.Add("Processing", await TabProcessing(node));
        if (node.OperatingSystem == OperatingSystemType.Linux || node.OperatingSystem == OperatingSystemType.Unknown)
            tabs.Add("Advanced", TabAdvanced(node));

        string helpUrl = isServerProcessingNode
            ? string.Empty
            : "http://docs.fileflows.com/guides/external-processing-node";

        var result = await Editor.Open(new()
        {
            TypeName = "Pages.ProcessingNode", Title = "Pages.ProcessingNode.Title", Model = node, Tabs = tabs,
            Large = true,
            SaveCallback = Save, HelpUrl = helpUrl
        });
#endif
        return false;
    }

    private List<ElementField> TabGeneral(ProcessingNode node, bool isServerProcessingNode, List<Script> scripts)
    {
        List<ElementField> fields = new List<ElementField>();

        if (isServerProcessingNode)
        {
            fields.Add(new ElementField
            {
                InputType = FormInputType.Label,
                Name = "InternalProcessingNodeDescription"
            });
        }
        else
        {
            fields.Add(new ElementField
            {
                InputType = FormInputType.Text,
                Name = nameof(node.Name),
                Validators = new List<FileFlows.Shared.Validators.Validator> {
                    new FileFlows.Shared.Validators.Required()
                }
            });
            fields.Add(new ElementField
            {
                InputType = FormInputType.Text,
                Name = nameof(node.Address),
                Validators = new List<FileFlows.Shared.Validators.Validator> {
                    new FileFlows.Shared.Validators.Required()
                }
            });
        }

        fields.Add(new ElementField
        {
            InputType = FormInputType.Switch,
            Name = nameof(node.Enabled)
        });
        fields.Add(new ElementField
        {
            InputType = FormInputType.Int,
            Name = nameof(node.FlowRunners),
            Validators = new List<FileFlows.Shared.Validators.Validator> {
                new FileFlows.Shared.Validators.Range() { Minimum = 0, Maximum = 100 } // 100 is insane but meh, let them be insane 
            },
            Parameters = new()
            {
                { "Min", 0 },
                { "Max", 100 }
            }
        });
        fields.Add(new ElementField
        {
            InputType = isServerProcessingNode ? FormInputType.Folder : FormInputType.Text,
            Name = nameof(node.TempPath),
            Validators = new List<FileFlows.Shared.Validators.Validator> {
                new FileFlows.Shared.Validators.Required()
            }
        });

        if (App.Instance.FileFlowsSystem.Licensed)
        {
            var scriptOptions = scripts.Select(x => new ListOption
            {
                Value = x.Name, Label = x.Name
            }).ToList();
            scriptOptions.Insert(0, new ListOption() { Label = "Labels.None", Value = string.Empty});
            fields.Add(new ElementField
            {
                InputType = FormInputType.Select,
                Name = nameof(node.PreExecuteScript),
                Parameters = new Dictionary<string, object>
                {
                    { "AllowClear", false},
                    { "Options", scriptOptions }
                }
            });
        }

        return fields;
    }

    private List<ElementField> TabSchedule(ProcessingNode node, bool isServerProcessingNode)
    {
        List<ElementField> fields = new List<ElementField>();
        fields.Add(new ElementField
        {
            InputType = FormInputType.Label,
            Name = "ScheduleDescription"
        });

        fields.Add(new ElementField
        {
            InputType = FormInputType.Schedule,
            Name = nameof(node.Schedule),
            Parameters = new Dictionary<string, object>
            {
                { "HideLabel", true }
            }
        });
        return fields;

    }
    private List<ElementField> TabMappings(ProcessingNode node)
    {
        List<ElementField> fields = new List<ElementField>();
        fields.Add(new ElementField
        {
            InputType = FormInputType.Label,
            Name = "MappingsDescription"
        });
        fields.Add(new ElementField
        {
            InputType = FormInputType.KeyValue,
            Name = nameof(node.Mappings),
            Parameters = new()
            {
                { "HideLabel", true }
            }
        });
        return fields;
    }
    
    
    private async Task<List<ElementField>> TabProcessing(ProcessingNode node)
    {
        var librariesResult = await HttpHelper.Get<Library[]>("/api/library");
        var libraries = librariesResult?.Data?.Select(x => new ListOption
        {
            Label = x.Name,
            Value = new ObjectReference
            {
                Uid = x.Uid,
                Name = x.Name,
                Type = typeof(Library)?.FullName ?? string.Empty
            }
        })?.OrderBy(x => x.Label)?.ToList() ?? new List<ListOption>();
        List<ElementField> fields = new List<ElementField>();
        fields.Add(new ElementField
        {
            InputType = FormInputType.Label,
            Name = "ProcessingDescription"
        });
        var efAllLibraries = new ElementField
        {
            InputType = FormInputType.Select,
            Name = nameof(node.AllLibraries),
            Parameters = new Dictionary<string, object>
            {
                { 
                    nameof(InputChecklist.Options), 
                    new List<ListOption>
                    {
                        new() { Label = "All", Value = ProcessingLibraries.All },
                        new() { Label = "Only", Value = ProcessingLibraries.Only },
                        new() { Label = "All Except", Value = ProcessingLibraries.AllExcept },
                    } 
                }
            }
        };
        fields.Add(efAllLibraries);
        fields.Add(new ElementField
        {
            InputType = FormInputType.Checklist,
            Name = nameof(node.Libraries),
            Parameters = new()
            {
                { nameof(InputChecklist.Options), libraries }
            },
            Conditions = new List<Condition>
            {
                new Condition(efAllLibraries, node.AllLibraries, value: ProcessingLibraries.All, isNot: true)
            }
        });
        
        fields.Add(new ElementField
        {
            InputType = FormInputType.Int,
            Name = nameof(node.MaxFileSizeMb)
        });
        return fields;
    }

    private List<ElementField> TabAdvanced(ProcessingNode node)
    {
        List<ElementField> fields = new List<ElementField>();
        fields.Add(new ElementField
        {
            InputType = FormInputType.Switch,
            Name = nameof(node.DontChangeOwner),
            Parameters = new()
            {
                { "Inverse", true }
            }
        });
        var efDontSetPermissions = new ElementField
        {
            InputType = FormInputType.Switch,
            Name = nameof(node.DontSetPermissions),
            Parameters = new()
            {
                { "Inverse", true }
            }
        };
        fields.Add(efDontSetPermissions);

        var condition = new Condition()
        {
            Value = false
        };
        condition.SetField(efDontSetPermissions, node.DontSetPermissions);

        fields.Add(new ElementField
        {
            InputType = FormInputType.Text,
            Name = nameof(node.Permissions),
            DisabledConditions = new List<Condition> { condition }
        });
        return fields;
    }
}