using System.Text.Json;
using FileFlows.Client.Components.Inputs;
using FileFlows.Plugin;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FileFlows.Client.Components;

/// <summary>
/// Editor for adding a new flow and showing the flow templates
/// </summary>
public partial class NewFlowEditor : Editor
{
    [Inject] NavigationManager NavigationManager { get; set; }
    [CascadingParameter] public Blocker Blocker { get; set; }
    TaskCompletionSource<Flow> ShowTask;
    
    /// <summary>
    /// Gets or sets the available templates
    /// </summary>
    [Parameter] public Dictionary<string, List<FlowTemplateModel>> Templates { get; set; }

    private const string FIELD_NAME = "Name";
    private const string FIELD_TEMPLATE = "Template";

    private List<ListOption> TemplateOptions;
    private string lblDescription;
    
    private ElementField efTemplate;
    private readonly Dictionary<string, TemplateFieldModel> TemplateFields = new ();
    private bool InitializingTemplate = false;
    private FlowTemplateModel CurrentTemplate;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.TypeName = "Flow";
        this.Title = Translater.Instant("Pages.Flows.Template.Title");
        this.lblDescription = Translater.Instant("Pages.Flows.Template.Fields.PageDescription");
        
        base.SaveCallback = SaveCallback;
    }

    private async Task InitTemplate(FlowTemplateModel template)
    {

        if (Fields?.Any() == true && template == this.CurrentTemplate)
            return;
        
        this.InitializingTemplate = true;
        try
        {
            DisposeCurrentTemplate();
            this.CurrentTemplate = template;
            
            if (this.Model is IDictionary<string, object> oldDict && oldDict.TryGetValue("Name", out object oName) &&
                oName is string sName && string.IsNullOrWhiteSpace(sName) == false)
            {
                this.Model = new ExpandoObject();
                ((IDictionary<string, object>)this.Model).Add("Name", sName);
            }
            else
            {
                this.Model = new ExpandoObject();
            }

            var fields = new List<ElementField>();
            fields.Add(new ElementField
            {
                InputType = FormInputType.Text,
                Name = FIELD_NAME,
                Validators = new List<FileFlows.Shared.Validators.Validator>
                {
                    new FileFlows.Shared.Validators.Required()
                }
            });
            this.efTemplate = new ElementField
            {
                InputType = FormInputType.Select,
                Name = FIELD_TEMPLATE,
                Parameters = new Dictionary<string, object>
                {
                    { "Options", TemplateOptions }
                },
                Validators = new List<FileFlows.Shared.Validators.Validator>
                {
                    new FileFlows.Shared.Validators.Required()
                }
            };
            this.efTemplate.ValueChanged += EfTemplateOnValueChanged;
            fields.Add(this.efTemplate);

            if (template?.Fields?.Any() == true)
            {
                foreach (var field in template.Fields)
                {
                    string efName = field.Label.Dehumanize();
                    var ef = new ElementField()
                    {
                        Name = efName,
                        Label = field.Label,
                        HelpText = field.Help,
                        Parameters = new(),
                        InputType = field.Type switch
                        {
                            "Directory" => FormInputType.Folder,
                            "Switch" => FormInputType.Switch,
                            "Select" => FormInputType.Select,
                            "Int" => FormInputType.Int,
                            _ => FormInputType.Text
                        }
                    };
                    if (ef.InputType == FormInputType.Select)
                    {
                        var parameters = JsonSerializer.Deserialize<SelectParameters>(
                            JsonSerializer.Serialize(field.Parameters), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        ef.Parameters.Add("Options", parameters.Options);
                    }
                    else if (ef.InputType == FormInputType.Int && field.Parameters != null)
                    {
                        var parameters = JsonSerializer.Deserialize<IntParameters>(
                            JsonSerializer.Serialize(field.Parameters), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        if(parameters.Minimum != 0)
                            ef.Parameters.Add("Min", parameters.Minimum);
                        if(parameters.Maximum != 0)
                            ef.Parameters.Add("Max", parameters.Maximum);
                    }
                    TemplateFields.Add(efName, new (field, ef));
                }

                foreach (var field in template.Fields)
                {
                    var tfm = TemplateFields[field.Label.Dehumanize()];
                    if (field.Conditions?.Any() == true)
                    {
                        foreach (var condition in field.Conditions)
                        {
                            if (TemplateFields.TryGetValue(condition.Property.Dehumanize(),
                                    out TemplateFieldModel efOther) == false)
                                continue;
                            tfm.ElementField.Conditions ??= new();
                            var newCon = new Condition(efOther.ElementField, null, condition.Value, condition.IsNot);
                            newCon.Owner = tfm.ElementField;
                            tfm.ElementField.Conditions.Add(newCon);
                        }
                    }

                    fields.Add(tfm.ElementField);
                    if (tfm.TemplateField.Default != null)
                        UpdateValue(tfm.ElementField, tfm.TemplateField.Default);
                    else if(tfm.ElementField.InputType == FormInputType.Switch)
                        UpdateValue(tfm.ElementField, false);
                }
            }
            this.Fields = fields;
            this.StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.Instance.ELog("Error initializing template: " + ex.Message + "\n" + ex.StackTrace);
        } 
        finally
        {
            InitializingTemplate = false;
        }
    }

    private void EfTemplateOnValueChanged(object sender, object value)
    {
        // if (InitializingTemplate)
        //     return;
        if (value is FlowTemplateModel template)
        {
            if (template == CurrentTemplate)
                return;
            
            InitTemplate(template);
        }
    }

    /// <summary>
    /// Shows the new flow editor
    /// </summary>
    public Task<Flow> Show()
    {
        ShowTask = new TaskCompletionSource<Flow>();
        _ = Task.Run(async () =>
        {
            if (this.Templates == null)
            {
                this.Blocker.Show("Pages.Flows.Messages.LoadingTemplates");
                this.StateHasChanged();
                try
                {
                    var flowResult =
                        await HttpHelper.Get<Dictionary<string, List<FlowTemplateModel>>>("/api/flow/templates");
                    if (flowResult.Success)
                        Templates = flowResult.Data ?? new();
                    else
                        Templates = new();
                    TemplateOptions ??= new();

                    foreach (var group in Templates)
                    {
                        if (string.IsNullOrEmpty(group.Key) == false)
                        {
                            TemplateOptions.Add(new Plugin.ListOption
                            {
                                Value = Globals.LIST_OPTION_GROUP,
                                Label = group.Key
                            });
                        }
                        TemplateOptions.AddRange(group.Value.Select(x => new Plugin.ListOption
                        {
                            Label = x.Flow.Name,
                            Value = x
                        }));
                    }
                }
                finally
                {
                    this.Blocker.Hide();
                }
            }

            if (efTemplate != null)
                efTemplate.ValueChanged -= EfTemplateOnValueChanged;
            if (this.TemplateOptions.Any() == false)
            {
                // no templates, give them a blank
                NavigationManager.NavigateTo("flows/" + Guid.Empty);
                ShowTask.TrySetResult(default(Flow));
                return;
            }
            this.Model = null;
            this.CurrentTemplate = null;
            this.Visible = true;
            await this.InitTemplate(null);
            this.StateHasChanged();
        });
        return ShowTask.Task;
    }

    private void DisposeCurrentTemplate()
    {
        var keys = RegisteredInputs.Keys.ToArray();
        for(int i=keys.Length -1;i >= 0;i--)
        {
            var key = keys[i];
            var input = RegisteredInputs[key];
            
            if (input == null)
                continue;
            if (input?.Field?.Name == "Name")
                continue;
            if (input?.Field?.Name == "Template")
                continue;
            input.Dispose();
            this.RegisteredInputs.Remove(key);
        }
        Fields?.Clear();
        TemplateFields?.Clear();
        Fields ??= new();
    }
    

    private async Task<bool> SaveCallback(ExpandoObject model)
    {
        var dict = model as IDictionary<string, object>;
        var flow = CurrentTemplate.Flow;
        if (dict.TryGetValue("Name", out object oName) && oName is string sName)
            flow.Name = sName;

        foreach (var tfm in TemplateFields.Values)
        {
            var part = flow.Parts.FirstOrDefault(x => x.Uid == tfm.TemplateField.Uid);
            if (part == null)
                continue;

            if (dict.ContainsKey(tfm.ElementField.Name) == false)
            {
                if (tfm.ElementField.InputType == FormInputType.Switch)
                    dict.Add(tfm.ElementField.Name, false); // special case, switches sometimes dont have their false values set
                else
                    continue;
            }

            var value = dict[tfm.ElementField.Name];
            if (value == null || value.Equals(string.Empty))
                continue;

            if (value?.ToString().StartsWith("OUTPUT:") == true)
            {
                // special case, removing this node and wiring up a different output
                var parts = value.ToString().Split(':');
                var outputNode = flow.Parts.FirstOrDefault(x => x.Uid.ToString() == parts[1]);
                int outputIndex = int.Parse(parts[2]);
                // remove any existing output connections
                outputNode.OutputConnections.RemoveAll(x => x.Output == outputIndex);
                outputNode.OutputConnections.Add(new ()
                {
                    Input = 1,
                    Output = outputIndex,
                    InputNode = Guid.Parse(parts[3])
                });
                flow.Parts.Remove(part);
                continue;
            }
            
            part.Model ??= new ExpandoObject();
            var dictPartModel = (IDictionary<string, object>)part.Model;
            string key = tfm.TemplateField.Name;
            if (string.IsNullOrEmpty(key))
            {
                // no name, means we are setting the entire model
                if (value is JsonElement jEle)
                    part.Model = jEle.Deserialize<ExpandoObject>();
                else if (value != null)
                    part.Model = JsonSerializer.Deserialize<ExpandoObject>(JsonSerializer.Serialize(value));
                else
                    part.Model = null;
                continue;
            }


            if (key == "Node")
            {
                // replace the node type
                part.FlowElementUid = value.ToString();
                continue;
            }

            if(tfm.TemplateField.Value is JsonElement jElement)
            {
                if(jElement.ValueKind == JsonValueKind.Object)
                {
                    var tv = jElement.Deserialize<Dictionary<string, object>>();
                    if (tv.ContainsKey("true") && value?.ToString()?.ToLower() == "true")
                        value = tv["true"];
                    else if(tv.ContainsKey("false") && value?.ToString()?.ToLower() == "false")
                        value = tv["false"];
                    else
                    {
                        // complex object, meaning we are setting properties
                        if (dictPartModel.ContainsKey(key) == false)
                        {
                            dictPartModel.Add(key, new ExpandoObject());
                        }
                        else if (dictPartModel[key] is IDictionary<string, object> == false)
                        {
                            dictPartModel[key] = new ExpandoObject();
                        }
                        dict = dictPartModel[key] as IDictionary<string, object>;
                    
                        foreach (var kv in tv.Keys)
                        {
                            if (dict.ContainsKey(kv))
                                dict[kv] = tv[kv];
                            else
                                dict.Add(kv, tv[kv]);
                        }
                        continue;
                    }
                }
                else if(jElement.ValueKind == JsonValueKind.String)
                {
                    value = jElement.GetString();
                }
                else if (jElement.ValueKind == JsonValueKind.Number)
                {
                    value = jElement.GetDouble();
                }
            }

            // do this after the JsonElement stuff so Value can be converted to true/false etc values
            if (key.StartsWith("Output-"))
            {
                if (Guid.TryParse(value?.ToString() ?? string.Empty, out Guid inputNode) == false)
                {
                    // if the input value means no connection,
                    // eg a switch where true is connecting Delete Original and false connects to nothing
                    continue;
                }

                int output = int.Parse(key.Substring("Output-".Length));
                // the value is then the UID of the node to connect this one too
                part.OutputConnections.Add(new ()
                {
                    Input = 1,
                    Output = output,
                    InputNode = inputNode
                });
                continue;
            }

            if (dictPartModel.ContainsKey(key))
                dictPartModel[key] = value;
            else
                dictPartModel.Add(key, value);
        }

        // shake lose any nodes that have no connections
        // stop at one to skip the input node
        int count;
        do
        {
            var inputNodes = flow.Parts.SelectMany(x => x.OutputConnections.Select(x => x.InputNode)).ToList();
            count = flow.Parts.Count;
            for (int i = flow.Parts.Count - 1; i >= 1; i--)
            {
                if (inputNodes.Contains(flow.Parts[i].Uid) == false)
                {
                    flow.Parts.RemoveAt(i);
                }
            }
        } while (count != flow.Parts.Count); // loop over as we may have removed a connection that now makes other nodes disconnected/redundant

        Logger.Instance.ILog("Flow", flow);

        if (CurrentTemplate.Save)
        {
            var newFlowResult = await HttpHelper.Put<Flow>("/api/flow", flow);
            if (newFlowResult.Success == false)
            {
                Toast.ShowError(newFlowResult.Body?.EmptyAsNull() ?? "Failed to create new flow");
                return false;
            }
            ShowTask.TrySetResult(newFlowResult.Data);
        }
        else
        {
            ShowTask.TrySetResult(flow);
        }
        return true;
    }

    private class SelectParameters
    {
        public List<ListOption> Options { get; set; }
    }

    private class IntParameters
    {
        public int Minimum { get; set; }
        public int Maximum { get; set; }
    }
    
    private record TemplateFieldModel(TemplateField TemplateField, ElementField ElementField);
}