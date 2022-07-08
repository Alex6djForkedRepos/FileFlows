using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Threading.Tasks;
using System.Transactions;
using FileFlows.Client.Pages;
using Humanizer;

namespace FileFlows.Client.Components.Inputs;


public partial class InputExecutedNodes: Input<IEnumerable<ExecutedNode>>
{
    /// <summary>
    /// Gets or sets the log for this item
    /// </summary>
    [Parameter]
    public string Log { get; set; }

    private string PartialLog;
    private ExecutedNode PartialLogNode;
    private string lblClose, lblLogPartialNotAvailable, lblViewLog;

    private List<string> _LogLines;
    private List<string> LogLines
    {
        get
        {
            if (_LogLines == null)
            {
                _LogLines = (Log ?? string.Empty).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return _LogLines;
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.lblClose = Translater.Instant("Labels.Close");
        this.lblLogPartialNotAvailable = Translater.Instant("Labels.LogPartialNotAvailable");
        this.lblViewLog = Translater.Instant("Labels.ViewLog");
    }
    

    private string FormatNodeUid(string name)
    {
        //FlowElement.FormatName(name);
        return name.Substring(name.LastIndexOf(".") + 1).Humanize(LetterCasing.Title)
            .Replace("File Flows", "FileFlows")
            .Replace("MKV", "MKV")
            .Replace("Mp4", "MP4")
            .Replace("Ffmpeg Builder", "FFMPEG Builder:");
    }
    
    private string FormatNodeName(ExecutedNode node)
    {
        if (string.IsNullOrEmpty(node.NodeName))
            return FormatNodeUid(node.NodeUid);
        
        string nodeUid = Regex.Match(node.NodeUid.Substring(node.NodeUid.LastIndexOf(".") + 1), "[a-zA-Z0-9]+").Value.ToLower();
        string nodeName = Regex.Match(node.NodeName ?? string.Empty, "[a-zA-Z0-9]+").Value.ToLower();

        if (string.IsNullOrEmpty(node.NodeName) || nodeUid == nodeName)
            return FormatNodeUid(node.NodeUid);
        
        return node.NodeName;
    }

    private void ClosePartialLog()
    {
        PartialLogNode = null;
        PartialLog = null;
    }

    private async Task OpenLog(ExecutedNode node)
    {
        int index = Value.ToList().IndexOf(node);
        if (index < 0)
        {
            Toast.ShowWarning(lblLogPartialNotAvailable);
            return;
        }

        ++index;
        var lines  = LogLines;
        int startIndex = lines.FindIndex(x => x.IndexOf($"Executing Node {index}:") > 0);
        if (startIndex < 1)
        {
            Toast.ShowWarning(lblLogPartialNotAvailable);
            return;
        }

        --startIndex;

        var remainingLindex = lines.Skip(startIndex + 3).ToList();

        int endIndex = remainingLindex.FindIndex(x => x.IndexOf("======================================================================") > 0);

        string sublog;
        if (endIndex > -1)
        {
            endIndex += startIndex + 4;
            sublog = string.Join("\n", lines.ToArray()[startIndex..endIndex]);
        }
        else
        {
            sublog = string.Join("\n", lines.ToArray()[startIndex..]);
        }

        PartialLog = sublog;
        PartialLogNode = node;
    }
}
