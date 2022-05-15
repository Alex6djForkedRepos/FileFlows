namespace FileFlows.FlowRunner;

using System;
using System.Linq;
using FileFlows.Plugin;
using FileFlows.Shared.Models;

/// <summary>
/// Logger specifically for the Flow execution
/// </summary>
public class FlowLogger : ILogger
{
    /// <summary>
    /// Gets or sets the log file used by this logger
    /// </summary>
    List<string> log = new List<string>();
    
    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="args">the arguments for the log message</param>
    public void DLog(params object[] args) => Log(LogType.Debug, args);
    /// <summary>
    /// Logs a error message
    /// </summary>
    /// <param name="args">the arguments for the log message</param>
    public void ELog(params object[] args) => Log(LogType.Error, args);
    /// <summary>
    /// Logs a information message
    /// </summary>
    /// <param name="args">the arguments for the log message</param>
    public void ILog(params object[] args) => Log(LogType.Info, args);
    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="args">the arguments for the log message</param>
    public void WLog(params object[] args) => Log(LogType.Warning, args);

    /// <summary>
    /// Gets or sets the library file this flow is executing
    /// </summary>
    public LibraryFile File { get; set; }
    
    /// <summary>
    /// The type of log message
    /// </summary>
    private enum LogType
    {
        /// <summary>
        /// A error message
        /// </summary>
        Error, 
        /// <summary>
        /// a warning message
        /// </summary>
        Warning,
        /// <summary>
        /// A informational message
        /// </summary>
        Info,
        /// <summary>
        /// A debug message
        /// </summary>
        Debug
    }

    IFlowRunnerCommunicator Communicator;
    
    /// <summary>
    /// Creates an instance of the a flow logger
    /// </summary>
    /// <param name="communicator">a communicator to report messages to the FileFlows server</param>
    public FlowLogger(IFlowRunnerCommunicator communicator)
    {
        this.Communicator = communicator;
    }

    /// <summary>
    /// Logs a message
    /// </summary>
    /// <param name="type">the type of message to log</param>
    /// <param name="args">the log message arguments</param>
    private void Log(LogType type, params object[] args)
    {
        if (args == null || args.Length == 0)
            return;
        string prefix = type switch
        {
            LogType.Info => "INFO",
            LogType.Error => "ERRR",
            LogType.Warning => "WARN",
            LogType.Debug => "DBUG",
            _ => ""
        };

        string message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " - " + prefix + " -> " +
            string.Join(", ", args.Select(x =>
            x == null ? "null" :
            x.GetType().IsPrimitive || x is string ? x.ToString() :
            System.Text.Json.JsonSerializer.Serialize(x)));
        log.Add(message);
        if(type != LogType.Debug)
            Console.WriteLine(message);
        try
        {
            Communicator.LogMessage(Program.Uid, message).Wait();
        }
        catch (Exception) { }
    }

    /// <summary>
    /// Gets the full log as a string
    /// </summary>
    /// <returns>the full log as a string</returns>
    public override string ToString() => String.Join(Environment.NewLine, log);

    /// <summary>
    /// Gets the last number of log lines
    /// </summary>
    /// <param name="length">The maximum number of lines to grab</param>
    /// <returns>The last number of log lines</returns>
    public string GetTail(int length = 50)
    {
        if (length <= 0)
            length = 50;

        var noLines = log.Where(x => x.Contains("======================================================================") == false);
        if (noLines.Count() <= length)
            return String.Join(Environment.NewLine, noLines);
        return String.Join(Environment.NewLine, noLines.Skip(noLines.Count() - length));
    }
}
