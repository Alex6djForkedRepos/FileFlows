using Microsoft.AspNetCore.Components;
using FileFlows.Client.Components;
using FileFlows.Client.Helpers;
using ffPart = FileFlows.Shared.Models.FlowPart;
using ffElement = FileFlows.Shared.Models.FlowElement;
using ff = FileFlows.Shared.Models.Flow;
using xFlowConnection = FileFlows.Shared.Models.FlowConnection;
using Microsoft.JSInterop;
using FileFlows.Client.Components.Dialogs;
using System.Text.Json;
using FileFlows.Plugin;
using System.Text.RegularExpressions;
using BlazorContextMenu;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FileFlows.Client.Pages;

public partial class Flow : ComponentBase, IDisposable
{
    [CascadingParameter] public Editor Editor { get; set; }
    [Parameter] public System.Guid Uid { get; set; }
    [Inject] INavigationService NavigationService { get; set; }
    [Inject] IBlazorContextMenuService ContextMenuService { get; set; }
    [CascadingParameter] Blocker Blocker { get; set; }
    [Inject] IHotKeysService HotKeyService { get; set; }
    private ffElement[] Available { get; set; }
    private ffElement[] Filtered { get; set; }
    private List<ffPart> Parts { get; set; } = new List<ffPart>();

    private string lblObsoleteMessage, lblEdit, lblHelp, lblDelete, lblCopy, lblPaste, lblRedo, lblUndo, lblAdd;
    private string SelectedElement;

    private int _Zoom = 100;
    private int Zoom
    {
        get => _Zoom;
        set
        {
            if(_Zoom != value)
            {
                _Zoom = value;
                _ = ZoomChanged(value);
            }
        }
    }

    private ElementReference eleFilter { get; set; }

    [Inject]
    private IJSRuntime jsRuntime { get; set; }
    private bool IsSaving { get; set; }
    private bool IsExecuting { get; set; }

    private ff Model { get; set; }

    private string Title { get; set; }

    private bool EditorOpen = false;

    private string Name { get; set; }

    const string API_URL = "/api/flow";

    private string lblName, lblSave, lblSaving, lblClose;

    private bool _needsRendering = false;

    private bool IsDirty = false;

    private bool ElementsVisible = false;

    private string _txtFilter = string.Empty;
    private string lblFilter;

    private Func<Task<bool>> NavigationCheck;

    public string txtFilter
    {
        get => _txtFilter;
        set
        {
            _txtFilter = value ?? string.Empty;
            string filter = value.Trim().Replace(" ", "").ToLower();
            if (filter == string.Empty)
                Filtered = Available;
            else
            {
                Filtered = Available.Where(x => x.Name.ToLower().Replace(" ", "").Contains(filter) || x.Group.ToLower().Replace(" ", "").Contains(filter)).ToArray();
            }
        }
    }

    protected override void OnInitialized()
    {
        lblName = Translater.Instant("Labels.Name");
        lblSave = Translater.Instant("Labels.Save");
        lblClose = Translater.Instant("Labels.Close");
        lblSaving = Translater.Instant("Labels.Saving");
        lblFilter = Translater.Instant("Labels.FilterPlaceholder");
        lblAdd = Translater.Instant("Labels.Add");
        lblEdit = Translater.Instant("Labels.Edit");
        lblCopy = Translater.Instant("Labels.Copy");
        lblPaste = Translater.Instant("Labels.Paste");
        lblRedo = Translater.Instant("Labels.Redo");
        lblUndo = Translater.Instant("Labels.Undo");
        lblDelete = Translater.Instant("Labels.Delete");
        lblHelp = Translater.Instant("Labels.Help");
        lblObsoleteMessage = Translater.Instant("Labels.ObsoleteConfirm.Message");


        NavigationCheck = async () =>
        {
            if (IsDirty)
            {
                bool result = await Confirm.Show(lblClose, $"Pages.{nameof(Flow)}.Messages.Close");
                if (result == false)
                    return false;
            }
            return true;
        };

        NavigationService.RegisterNavigationCallback(NavigationCheck);

        HotKeyService.RegisterHotkey("FlowFilter", "/", callback: () =>
        {
            if (EditorOpen) return;
            Task.Run(async () =>
            {
                await Task.Delay(10);
                await eleFilter.FocusAsync();
            });
        });

        HotKeyService.RegisterHotkey("FlowUndo", "Z", ctrl: true, shift: false, callback: () => Undo());
        HotKeyService.RegisterHotkey("FlowUndo", "Z", ctrl: true, shift: true, callback: () => Redo());
        _ = Init();
    }


    public void Dispose()
    {
        HotKeyService.DeregisterHotkey("FlowFilter");
        NavigationService.UnRegisterNavigationCallback(NavigationCheck);
    }

    private void SelectPart(string uid)
    {
        if (App.Instance.IsMobile)
            SelectedElement = uid;
    }

    private async Task Init()
    {
        this.Blocker.Show();
        this.StateHasChanged();
        try
        {
            FileFlows.Shared.Models.Flow flow;
            if (Uid == Guid.Empty && App.Instance.NewFlowTemplate != null)
            {
                flow = App.Instance.NewFlowTemplate;
                App.Instance.NewFlowTemplate = null;
            }
            else
            {
                var modelResult = await GetModel(API_URL + "/" + Uid.ToString());
                flow = (modelResult.Success ? modelResult.Data : null) ?? new ff() { Parts = new List<ffPart>() };
            }

            var elementsResult = await GetElements(API_URL + "/elements?type=" + (int)flow.Type);
            if (elementsResult.Success)
            {
                Available = elementsResult.Data;
                this.txtFilter = string.Empty;
            }
            await InitModel(flow);

            var dotNetObjRef = DotNetObjectReference.Create(this);
            await jsRuntime.InvokeVoidAsync("ffFlow.init", new object[] { "flow-parts", dotNetObjRef, this.Parts, Available });

            await WaitForRender();
            await jsRuntime.InvokeVoidAsync("ffFlow.redrawLines");

        }
        finally
        {
            this.Blocker.Hide();
            this.StateHasChanged();
        }
    }

    private async Task<RequestResult<ff>> GetModel(string url)
    {
#if (DEMO)
        string json = "{\"Enabled\":true,\"Parts\":[{\"Uid\":\"10c99731-370d-41b6-b400-08d003e6e843\",\"Name\":\"\",\"FlowElementUid\":\"FileFlows.VideoNodes.VideoFile\",\"xPos\":411,\"yPos\":18,\"Icon\":\"fas fa-video\",\"Inputs\":0,\"Outputs\":1,\"OutputConnections\":[{\"Input\":1,\"Output\":1,\"InputNode\":\"38e28c04-4ce7-4bcf-90f3-79ed0796f347\"}],\"Type\":0,\"Model\":{}},{\"Uid\":\"3121dcae-bfb8-4c37-8871-27618b29beb4\",\"Name\":\"\",\"FlowElementUid\":\"FileFlows.VideoNodes.Video_H265_AC3\",\"xPos\":403,\"yPos\":310,\"Icon\":\"far fa-file-video\",\"Inputs\":1,\"Outputs\":2,\"OutputConnections\":[{\"Input\":1,\"Output\":1,\"InputNode\":\"7363e1d1-2cc3-444c-b970-a508e7ef3d42\"},{\"Input\":1,\"Output\":2,\"InputNode\":\"7363e1d1-2cc3-444c-b970-a508e7ef3d42\"}],\"Type\":2,\"Model\":{\"Language\":\"eng\",\"Crf\":21,\"NvidiaEncoding\":true,\"Threads\":0,\"Name\":\"\",\"NormalizeAudio\":false}},{\"Uid\":\"7363e1d1-2cc3-444c-b970-a508e7ef3d42\",\"Name\":\"\",\"FlowElementUid\":\"FileFlows.BasicNodes.File.MoveFile\",\"xPos\":404,\"yPos\":489,\"Icon\":\"fas fa-file-export\",\"Inputs\":1,\"Outputs\":1,\"OutputConnections\":[{\"Input\":1,\"Output\":1,\"InputNode\":\"bc8f30c0-a72e-47a4-94fc-7543206705b9\"}],\"Type\":2,\"Model\":{\"DestinationPath\":\"/media/downloads/converted/tv\",\"MoveFolder\":true,\"DeleteOriginal\":true}},{\"Uid\":\"38e28c04-4ce7-4bcf-90f3-79ed0796f347\",\"Name\":\"\",\"FlowElementUid\":\"FileFlows.VideoNodes.DetectBlackBars\",\"xPos\":411,\"yPos\":144,\"Icon\":\"fas fa-film\",\"Inputs\":1,\"Outputs\":2,\"OutputConnections\":[{\"Input\":1,\"Output\":1,\"InputNode\":\"3121dcae-bfb8-4c37-8871-27618b29beb4\"},{\"Input\":1,\"Output\":2,\"InputNode\":\"3121dcae-bfb8-4c37-8871-27618b29beb4\"}],\"Type\":3,\"Model\":{}},{\"Uid\":\"bc8f30c0-a72e-47a4-94fc-7543206705b9\",\"Name\":\"\",\"FlowElementUid\":\"FileFlows.BasicNodes.File.DeleteSourceDirectory\",\"xPos\":404,\"yPos\":638,\"Icon\":\"far fa-trash-alt\",\"Inputs\":1,\"Outputs\":2,\"OutputConnections\":null,\"Type\":2,\"Model\":{\"IfEmpty\":true,\"IncludePatterns\":[\"mkv\",\"mp4\",\"divx\",\"avi\"]}}]}";

        var result = System.Text.Json.JsonSerializer.Deserialize<ff>(json);
        return new RequestResult<ff> { Success = true, Data = result };
#else
        return await HttpHelper.Get<ff>(url);
#endif
    }

    private async Task<RequestResult<ffElement[]>> GetElements(string url)
    {
        return await HttpHelper.Get<ffElement[]>(url);
    }

    private async Task WaitForRender()
    {
        _needsRendering = true;
        StateHasChanged();
        while (_needsRendering)
        {
            await Task.Delay(50);
        }
    }

    async Task Close()
    {
        if (App.Instance.IsMobile && ElementsVisible)
        {
            ElementsVisible = false;
            return;
        }
        await NavigationService.NavigateTo("flows");
    }

    protected override void OnAfterRender(bool firstRender)
    {
        _needsRendering = false;
    }

    private void SetTitle()
    {
        this.Title = Translater.Instant("Pages.Flow.Title", Model) + ":";
    }

    private async Task InitModel(ff model)
    {
        this.Model = model;
        this.SetTitle();
        this.Model.Parts ??= new List<ffPart>(); // just incase its null
        this.Parts = this.Model.Parts;
        foreach (var p in this.Parts)
        {
            if (string.IsNullOrEmpty(p.Name) == false || string.IsNullOrEmpty(p?.FlowElementUid))
                continue;
            string type = p.FlowElementUid.Substring(p.FlowElementUid.LastIndexOf(".") + 1);
            string name = Translater.Instant($"Flow.Parts.{type}.Label", supressWarnings: true);
            if (name == "Label")
                name = FlowHelper.FormatLabel(type);
            p.Name = name;
        }

        this.Name = model.Name ?? "";

        var connections = new Dictionary<string, List<xFlowConnection>>();
        foreach (var part in this.Parts.Where(x => x.OutputConnections?.Any() == true))
        {
            connections.Add(part.Uid.ToString(), part.OutputConnections);
        }
        await jsRuntime.InvokeVoidAsync("ffFlow.ioInitConnections", connections);

    }

    [JSInvokable]
    public async Task<string> NewGuid() => Guid.NewGuid().ToString();

    [JSInvokable]
    public async Task<object> AddElement(string uid)
    {
        var element = this.Available.FirstOrDefault(x => x.Uid == uid);
        string name;
        if (element.Type == FlowElementType.Script)
        {
            // special type
            name = element.Name;
        }
        else
        {
            string type = element.Uid.Substring(element.Uid.LastIndexOf(".") + 1);
            name = Translater.Instant($"Flow.Parts.{type}.Label", supressWarnings: true);
            if (name == "Label")
                name = FlowHelper.FormatLabel(type);
        }

        if (element.Obsolete)
        {
            string msg = element.ObsoleteMessage?.EmptyAsNull() ?? lblObsoleteMessage;
            string confirmMessage = Translater.Instant("Labels.ObsoleteConfirm.Question");
            string title = Translater.Instant("Labels.ObsoleteConfirm.Title");

            msg += "\n\n" + confirmMessage;
            var confirmed = await Confirm.Show(title, msg);
            if (confirmed == false)
                return null;
        }

        element.Name = name;
        return new { element, uid = Guid.NewGuid() };
    }

    [JSInvokable]
    public string Translate(string key, ExpandoObject model)
    {
        string prefix = string.Empty;
        if (key.Contains(".Outputs."))
        {
            prefix = Translater.Instant("Labels.Output") + " " + key.Substring(key.LastIndexOf(".") + 1) + ": ";
        }

        string translated = Translater.Instant(key, model);
        if (Regex.IsMatch(key, "^[\\d]+$"))
            return string.Empty;
        return prefix + translated;
    }

    private async Task ZoomChanged(int zoom)
    {
        await jsRuntime.InvokeVoidAsync("ffFlow.zoom", new object[] { zoom });
    }

    private async Task Save()
    {
#if (DEMO)
        return;
#else
        this.Blocker.Show(lblSaving);
        this.IsSaving = true;
        try
        {

            var parts = await jsRuntime.InvokeAsync<List<FileFlows.Shared.Models.FlowPart>>("ffFlow.getModel");

            Model ??= new ff();
            Model.Name = this.Name;
            // ensure there are no duplicates and no rogue connections
            Guid[] nodeUids = parts.Select(x => x.Uid).ToArray();
            foreach (var p in parts)
            {
                p.OutputConnections = p.OutputConnections
                                      ?.Where(x => nodeUids.Contains(x.InputNode))
                                      ?.GroupBy(x => x.Output).Select(x => x.First())
                                      ?.ToList();
            }
            Model.Parts = parts;
            var result = await HttpHelper.Put<ff>(API_URL, Model);
            if (result.Success)
            {
                if ((App.Instance.FileFlowsSystem.ConfigurationStatus & ConfigurationStatus.Flows) !=
                    ConfigurationStatus.Flows)
                {
                    // refresh the app configuration status
                    await App.Instance.LoadAppInfo();
                }

                Model = result.Data;
                IsDirty = false;
            }
            else
            {
                Toast.ShowError(
                    result.Success || string.IsNullOrEmpty(result.Body) ? Translater.Instant($"ErrorMessages.UnexpectedError") : Translater.TranslateIfNeeded(result.Body),
                    duration: 60_000
                );
            }
        }
        finally
        {
            this.IsSaving = false;
            this.Blocker.Hide();
        }
#endif
    }

    private async Task<RequestResult<Dictionary<string, object>>> GetVariables(string url, List<FileFlows.Shared.Models.FlowPart> parts)
    {
#if (DEMO)
        string json = "{\"ext\":\".mkv\",\"fileName\":\"Filename\",\"fileSize\":1000,\"fileOrigExt\":\".mkv\",\"fileOrigFileName\":\"OriginalFile\",\"folderName\":\"FolderName\",\"folderFullName\":\"/folder/subfolder\",\"folderOrigName\":\"FolderOriginalName\",\"folderOrigFullName\":\"/originalFolder/subfolder\",\"viVideoCodec\":\"hevc\",\"viAudioCodec\":\"ac3\",\"viAudioCodecs\":\"ac3,aac\",\"viAudioLanguage\":\"eng\",\"viAudioLanguages\":\"eng, mao\",\"viResolution\":\"1080p\"}";
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        return new RequestResult<Dictionary<string, object>> { Success = true, Data = dict };
#else
        return await HttpHelper.Post<Dictionary<string, object>>(url, parts);
#endif
    }


    private List<FlowPart> SelectedParts;
    [JSInvokable]
    public async Task OpenContextMenu(OpenContextMenuArgs args)
    {
        SelectedParts = args.Parts ?? new();
        await ContextMenuService.ShowMenu(SelectedParts.Count == 1 ? "FlowContextMenu-Single" :
            SelectedParts.Count > 1 ? "FlowContextMenu-Multiple" : "FlowContextMenu-Basic", args.X, args.Y);
    }

    private async Task FilterKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            this.txtFilter = String.Empty;
            return;
        }
        if (e.Key != "Enter")
            return;
        if (this.Filtered.Length != 1)
            return;
        var item = this.Filtered[0];
        await jsRuntime.InvokeVoidAsync("ffFlow.insertElement", item.Uid);
        this.txtFilter = String.Empty;
    }

    private Dictionary<string, object> EditorVariables;
    [JSInvokable]
    public async Task<object> Edit(ffPart part, bool isNew = false)
    {
        // get flow variables that we can pass onto the form
        Blocker.Show();
        Dictionary<string, object> variables = new Dictionary<string, object>();
        try
        {
            var parts = await jsRuntime.InvokeAsync<List<FlowPart>>("ffFlow.getModel");
            var variablesResult = await GetVariables(API_URL + "/" + part.Uid + "/variables?isNew=" + isNew, parts);
            if (variablesResult.Success)
                variables = variablesResult.Data;
        }
        finally { Blocker.Hide(); }


        var flowElement = this.Available.FirstOrDefault(x => x.Uid == part.FlowElementUid);
        if (flowElement == null)
        {
            // cant find it, cant edit
            Logger.Instance.DLog("Failed to locate flow element: " + part.FlowElementUid);
            return null;
        }

        string typeName;
        string typeDisplayName;
        if (part.Type == FlowElementType.Script)
        {
            typeName = "Script";
            typeDisplayName = part.FlowElementUid[7..]; // 7 to remove Script:
        }
        else
        {
            typeName = part.FlowElementUid[(part.FlowElementUid.LastIndexOf(".") + 1)..];
            typeDisplayName = Translater.TranslateIfHasTranslation($"Flow.Parts.{typeName}.Label", FlowHelper.FormatLabel(typeName));
        }

        var fields = ObjectCloner.Clone(flowElement.Fields);
        // add the name to the fields, so a node can be renamed
        fields.Insert(0, new ElementField
        {
            Name = "Name",
            Placeholder = typeDisplayName,
            InputType = FormInputType.Text
        });

        bool isFunctionNode = flowElement.Uid == "FileFlows.BasicNodes.Functions.Function";

        if (isFunctionNode)
        {
            // special case
            await FunctionNode(fields);
        }


        var model = part.Model ?? new ExpandoObject();
        // add the name to the model, since this is actually bound on the part not model, but we need this 
        // so the user can update the name
        if (model is IDictionary<string, object> dict)
        {
            dict["Name"] = part.Name ?? string.Empty;
        }

        List<ListOption> flowOptions = null;

        foreach (var field in fields)
        {
            field.Variables = variables;
            if(field.Conditions?.Any() == true)
            {
                foreach(var condition in field.Conditions)
                {
                    if (condition.Owner == null)
                        condition.Owner = field;
                    if (condition.Field == null && string.IsNullOrWhiteSpace(condition.Property) == false)
                    {
                        var other = fields.Where(x => x.Name == condition.Property).FirstOrDefault();
                        if (other != null && model is IDictionary<string, object> mdict) 
                        {
                            object otherValue = mdict.ContainsKey(other.Name) ? mdict[other.Name] : null;
                            condition.SetField(other, otherValue);
                        }
                    }
                }
            }
            // special case, load "Flow" into FLOW_LIST
            // this lets a plugin request the list of flows to be shown
            if (field.Parameters?.Any() == true)
            {
                if (field.Parameters.ContainsKey("OptionsProperty") && field.Parameters["OptionsProperty"] is JsonElement optProperty)
                {
                    if (optProperty.ValueKind == JsonValueKind.String)
                    {
                        string optp = optProperty.GetString();
                        Logger.Instance.DLog("OptionsProperty = " + optp);
                        if (optp == "FLOW_LIST")
                        {
                            if (flowOptions == null)
                            {
                                flowOptions = new List<ListOption>();
                                var flowsResult = await HttpHelper.Get<ff[]>($"/api/flow");
                                if (flowsResult.Success)
                                {
                                    flowOptions = flowsResult.Data?.Where(x => x.Uid != Model?.Uid)?.OrderBy(x => x.Name)?.Select(x => new ListOption
                                    {
                                        Label = x.Name,
                                        Value = new ObjectReference
                                        {
                                            Name = x.Name,
                                            Uid = x.Uid,
                                            Type = x.GetType().FullName
                                        }
                                    })?.ToList() ?? new List<ListOption>();
                                }

                            }
                            if (field.Parameters.ContainsKey("Options"))
                                field.Parameters["Options"] = flowOptions;
                            else
                                field.Parameters.Add("Options", flowOptions);
                        }
                    }
                }
            }
        }



        string title = typeDisplayName;
        EditorOpen = true;
        EditorVariables = variables;
        var newModelTask = Editor.Open(new()
        {
            TypeName = "Flow.Parts." + typeName, Title = title, Fields = fields, Model = model,
            Large = fields.Count > 1, HelpUrl = flowElement.HelpUrl,
            SaveCallback = isFunctionNode ? FunctionSaveCallback : null
        });           
        try
        {
            await newModelTask;
        }
        catch (Exception)
        {
            // can throw if canceled
            return null;
        }
        finally
        {
            EditorOpen = false;
            await jsRuntime.InvokeVoidAsync("ffFlowPart.focusElement", part.Uid.ToString());
        }
        if (newModelTask.IsCanceled == false)
        {
            IsDirty = true;
            var newModel = newModelTask.Result;
            int outputs = -1;
            if (part.Model is IDictionary<string, object> dictNew)
            {
                if (dictNew?.ContainsKey("Outputs") == true && int.TryParse(dictNew["Outputs"]?.ToString(), out outputs)) { }
            }
            return new { outputs, model = newModel };
        }
        else
        {
            return null;
        }
    }
    
    private void ShowElementsOnClick()
    {
        ElementsVisible = !ElementsVisible;
        if (ElementsVisible)
            SelectedElement = null; // clear the selected item
    }

    private async Task<bool> FunctionSaveCallback(ExpandoObject model)
    {
        // need to test code
        var dict = model as IDictionary<string, object>;
        string code  = (dict?.ContainsKey("Code") == true ? dict["Code"] as string : null) ?? string.Empty;
        var codeResult = await HttpHelper.Post<string>("/api/script/validate", new { Code = code, Variables = EditorVariables, IsFunction = true });
        string error = null;
        if (codeResult.Success)
        {
            if (string.IsNullOrEmpty(codeResult.Data))
                return true;
            error = codeResult.Data;
        }
        Toast.ShowError(error?.EmptyAsNull() ?? codeResult.Body, duration: 20_000);
        return false;
    }

    private async Task FunctionNode(List<ElementField> fields)
    {
        var templates = await GetCodeTemplates();
        var templatesOptions = templates.Select(x => new ListOption()
        {
            Label = x.Name,
            Value = x
        }).ToList();
        templatesOptions.Insert(0, new ListOption
        {
            Label = Translater.Instant("Labels.None"),
            Value = null
        });
        var efTemplate = new ElementField
        {
            Name = "Template",
            InputType = FormInputType.Select,                
            UiOnly = true,
            Parameters = new Dictionary<string, object>
            {
                { nameof(Components.Inputs.InputSelect.AllowClear), false },
                { nameof(Components.Inputs.InputSelect.Options), templatesOptions }
            }
        };
        var efCode = fields.FirstOrDefault(x => x.InputType == FormInputType.Code);
        efTemplate.ValueChanged += (object sender, object value) =>
        {
            if (value == null)
                return;
            CodeTemplate template = value as CodeTemplate;
            if (template == null || string.IsNullOrEmpty(template.Code))
                return;
            Editor editor = sender as Editor;
            if (editor == null)
                return;
            if (editor.Model == null)
                editor.Model = new ExpandoObject();
            IDictionary<string, object> model = editor.Model;

            SetModelProperty(nameof(template.Outputs), template.Outputs);
            SetModelProperty(nameof(template.Code), template.Code);
            if (efCode != null)
                efCode.InvokeValueChanged(this, template.Code);

            void SetModelProperty(string property, object value)
            {
                if (model.ContainsKey(property))
                    model[property] = value;
                else
                    model.Add(property, value);
            }
        };
        fields.Insert(2, efTemplate);
    }

    private async Task<List<CodeTemplate>> GetCodeTemplates()
    {
        var templateResponse = await HttpHelper.Get<List<Script>>("/api/script/templates");
        if (templateResponse.Success == false || templateResponse.Data?.Any() != true)
            return new List<CodeTemplate>();

        List<CodeTemplate> templates = new();
        var rgxComments = new Regex(@"\/\*(\*)?(.*?)\*\/", RegexOptions.Singleline);
        var rgxOutputs = new Regex(@"@outputs ([\d]+)");
        foreach (var template in templateResponse.Data)
        {
            var commentBlock = rgxComments.Match(template.Code);
            if (commentBlock.Success == false)
                continue;
            var outputs = rgxOutputs.Match(commentBlock.Value);
            if (outputs.Success == false)
                continue;

            string code = template.Code;
            if (code.StartsWith("// path:"))
                code = string.Join("\n", code.Split('\n').Skip(1));
            code = code.Replace(commentBlock.Value, string.Empty).Trim();

            var ct = new CodeTemplate
            {
                Name = template.Name,
                Code = code,
                Outputs = int.Parse(outputs.Groups[1].Value),
            };
            templates.Add(ct);
        }
        
        return templates.OrderBy(x => x.Name).ToList();
    }

    private async Task EditItem()
    {
        var item = this.SelectedParts?.FirstOrDefault();
        if (item == null)
            return;
        await jsRuntime.InvokeVoidAsync("ffFlow.contextMenu_Edit", item);
    }

    private void Copy() => jsRuntime.InvokeVoidAsync("ffFlow.contextMenu_Copy", SelectedParts);
    private void Paste() => jsRuntime.InvokeVoidAsync("ffFlow.contextMenu_Paste");
    private void Add() => jsRuntime.InvokeVoidAsync("ffFlow.contextMenu_Add");
    private void Undo() => jsRuntime.InvokeVoidAsync("ffFlow.History.undo");
    private void Redo() => jsRuntime.InvokeVoidAsync("ffFlow.History.redo");
    private void DeleteItems() => jsRuntime.InvokeVoidAsync("ffFlow.contextMenu_Delete", SelectedParts);

    private void AddSelectedElement()
    {
        jsRuntime.InvokeVoidAsync("ffFlow.addElementActual", new object[] { this.SelectedElement, 100, 100});
        this.ElementsVisible = false;
    } 
    
    private async Task OpenHelp()
    {
        await jsRuntime.InvokeVoidAsync("open", "https://docs.fileflows.com/flow-editor", "_blank");
    }

    private class CodeTemplate
    {
        public string Name { get; init; }
        public string Code { get; init; }
        public int Outputs { get; init; }
    }

    /// <summary>
    /// Arguments passed from JavaScript when opening the context menu
    /// </summary>
    public class OpenContextMenuArgs
    {
        /// <summary>
        /// Gets the X coordinate of the mouse
        /// </summary>
        public int X { get; init; }
        /// <summary>
        /// Gets the Y coordinate of the mouse
        /// </summary>
        public int Y { get; init; }
        /// <summary>
        /// Gets the selected parts
        /// </summary>
        public List<FlowPart> Parts { get; init; }
    }

} 