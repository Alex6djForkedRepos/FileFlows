﻿using FileFlows.Plugin;
using FileFlows.Shared;

namespace FileFlows.ServerShared;

/// <summary>
/// A logger that outputs to the console
/// </summary>
public class ConsoleLogger : ILogWriter
{
    /// <summary>
    /// Creates an instance of a console logger
    /// </summary>
    public ConsoleLogger()
    {
        Shared.Logger.Instance.RegisterWriter(this);
    }
    
    /// <summary>
    /// Logs a message
    /// </summary>
    /// <param name="type">the type of log message</param>
    /// <param name="args">the arguments for the log message</param>
    public Task Log(LogType type, params object[] args)
    {
        string message = type + " -> " + string.Join(", ", args.Select(x =>
            x == null ? "null" :
            x.GetType().IsPrimitive ? x.ToString() :
            x is string ? x.ToString() :
            System.Text.Json.JsonSerializer.Serialize(x)));
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
}
