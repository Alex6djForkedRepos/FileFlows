﻿using Microsoft.AspNetCore.SignalR.Client;

namespace FileFlows.FlowRunner;

/// <summary>
/// Interface used for the flow runner to communicate with the FileFlows server
/// </summary>
public interface IFlowRunnerCommunicator
{
    /// <summary>
    /// Logs a message to the FileFlows server
    /// </summary>
    /// <param name="runnerUid">the UID of the flow runner</param>
    /// <param name="message">the message to log</param>
    /// <returns>a completed task</returns>
    Task LogMessage(Guid runnerUid, string message);
}

/// <summary>
/// A communicator by the flow runner to communicate with the FileFlows server
/// </summary>
public class FlowRunnerCommunicator : IFlowRunnerCommunicator
{
    /// <summary>
    /// Gets ors sets the URL to the signalr endpoint on the FileFlows server
    /// </summary>
    public static string SignalrUrl { get; set; }
    
    /// <summary>
    /// The signalr hub connection
    /// </summary>
    HubConnection connection;
    
    /// <summary>
    /// The UID of the executing library file
    /// </summary>
    private Guid LibraryFileUid;
    
    /// <summary>
    /// Delegate used when the flow is being canceled
    /// </summary>
    public delegate void Cancel();
    
    /// <summary>
    /// Event used when the flow is being canceled
    /// </summary>
    public event Cancel OnCancel;


    /// <summary>
    /// Creates an instance of the flow runner communicator
    /// </summary>
    /// <param name="libraryFileUid">the UID of the library file being executed</param>
    /// <exception cref="Exception">throws an exception if cannot connect to the server</exception>
    public FlowRunnerCommunicator(Guid libraryFileUid)
    {
        this.LibraryFileUid = libraryFileUid;
        Program.LogInfo("SignalrUrl: " + SignalrUrl);
        connection = new HubConnectionBuilder()
                            .WithUrl(new Uri(SignalrUrl))
                            .WithAutomaticReconnect()
                            .Build();
        connection.Closed += Connection_Closed;
        connection.On<Guid>("AbortFlow", (uid) =>
        {
            if (uid != LibraryFileUid)
                return;
            OnCancel?.Invoke();
        });
        connection.StartAsync().Wait();
        if (connection.State == HubConnectionState.Disconnected)
            throw new Exception("Failed to connect to signalr");
    }

    /// <summary>
    /// Closes the Signalr connection to the server
    /// </summary>
    public void Close()
    {
        try
        {
            connection?.DisposeAsync();
        }
        catch (Exception) { } // not sure if this can throw, but just in case
    }

    /// <summary>
    /// Called when the Signalr connection is closed
    /// </summary>
    /// <param name="obj">the connection object</param>
    /// <returns>a completed task</returns>
    private Task Connection_Closed(Exception? arg)
    {
        Program.LogInfo("Connection_Closed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the Signalr connection is received
    /// </summary>
    /// <param name="obj">the connection object</param>
    private void Connection_Received(string obj)
    {
        Program.LogInfo("Connection_Received");
    }

    /// <summary>
    /// Logs a message to the FileFlows server
    /// </summary>
    /// <param name="runnerUid">the UID of the flow runner</param>
    /// <param name="message">the message to log</param>
    /// <returns>a completed task</returns>
    public async Task LogMessage(Guid runnerUid, string message)
    {
        try
        {
            await connection.InvokeAsync("LogMessage", runnerUid, LibraryFileUid, message);
        } 
        catch (Exception)
        {
            // silently fail here, we store the log in memory if one message fails its not a biggie
            // once the flow is complete we send the entire log to the server to update
        }
    }

    /// <summary>
    /// Sends a hello to the server saying this runner is still executing
    /// </summary>
    /// <param name="runnerUid">the UID of the flow runner</param>
    public async Task<bool> Hello(Guid runnerUid)
    {
        try
        {
            return await connection.InvokeAsync<bool>("Hello", runnerUid, LibraryFileUid);
        }
        catch(Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Loads an instance of the FlowRunnerCommunicator
    /// </summary>
    /// <param name="libraryFileUid">the UID of the library file being processed</param>
    /// <returns>an instance of the FlowRunnerCommunicator</returns>
    public static FlowRunnerCommunicator Load(Guid libraryFileUid)
    {
        return new FlowRunnerCommunicator(libraryFileUid);

    }
}