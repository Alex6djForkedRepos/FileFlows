using System.Text.Json;
using FileFlows.ScriptExecution;
using Logger = FileFlows.Shared.Logger;
using FileFlows.Plugin;
using System.Dynamic;

namespace FileFlows.Server;

/// <summary>
/// A special node that is not shown in the UI and only created
/// by the Flow Runner to execute a script.
/// This Node exists in this plugin to make use of the Javascript executor
/// </summary>
public class ScriptNode:Node
{
    /// <summary>
    /// Gets the number of inputs of this node
    /// </summary>
    public override int Inputs => 1;

    /// <summary>
    /// Gets or sets the model to pass to the node
    /// </summary>
    public ExpandoObject Model { get; set; }

    /// <summary>
    /// Gets or sets the code to execute
    /// </summary>
    public string Code { get; set; }


    /// <summary>
    /// Executes the script node
    /// </summary>
    /// <param name="args">the NodeParameters passed into this from the flow runner</param>
    /// <returns>the output node to call next</returns>
    public override int Execute(NodeParameters args)
    {
        // will throw exception if invalid
        var scriptParseResult = new ScriptParser().Parse("ScriptNode", Code);
        if (scriptParseResult.Success == false)
            throw new Exception(scriptParseResult.Error);
        
        var script = scriptParseResult.Model;

        // build up the entry point
        string epParams = string.Join(", ", script.Parameters?.Select(x => x.Name).ToArray());
        // all scripts must contain the "Script" method we then add this to call that 
        //string entryPoint = $"Script({epParams});";
        string entryPoint = $"var scriptResult = Script({epParams});\nexport const result = scriptResult;";

        var execArgs = new Plugin.Models.ScriptExecutionArgs
        {
            Args = args,
            ScriptType = ScriptType.Flow,
            Code = (Code + "\n\n" + entryPoint).Replace("\t", "   ").Trim(),
            AdditionalArguments = new ()
        };

        if (script.Parameters?.Any() == true)
        {
            var dictModel = Model as IDictionary<string, object>;
            foreach (var p in script.Parameters) 
            {
                try
                {
                    var value = dictModel?.ContainsKey(p.Name) == true ? dictModel[p.Name] : null;
                    if (value is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.String)
                        {
                            var str = je.GetString();
                            if (string.IsNullOrWhiteSpace(str) == false)
                            {
                                Logger.Instance.ILog("Parameter is string replacing variables: " + str);
                                string replaced = args.ReplaceVariables(str);
                                if (replaced != str)
                                {
                                    Logger.Instance.ILog("Variables replaced: " + replaced);
                                    value = replaced;
                                }
                            }
                        }
                        else if (je.ValueKind == JsonValueKind.True)
                            value = true;
                        else if (je.ValueKind == JsonValueKind.False)
                            value = false;
                        else if (je.ValueKind == JsonValueKind.Number)
                            value = double.Parse(je.ToString());
                        else if (je.ValueKind == JsonValueKind.Null)
                            value = null;
                    }

                    execArgs.AdditionalArguments.Add(p.Name, value);
                }
                catch (Exception ex)
                {
                    args?.Logger?.WLog("Failed to set parameter: " + p.Name + " => " + ex.Message +
                                       Environment.NewLine + ex.StackTrace);
                }
            }
        }

        return args.ScriptExecutor.Execute(execArgs);
    }
}
