﻿using System.Text.Json;
using FileFlows.Client.Components;
using FileFlows.Plugin;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;

namespace FileFlows.Client.Services;

/// <summary>
/// Service for connecting to the SignalR server and handling incoming messages and commands.
/// </summary>
public partial class ClientService
{
    /// <summary>
    /// The SignalR hub connection.
    /// </summary>
    private HubConnection _hubConnection;

    /// <summary>
    /// Indicates whether the client is connected to the SignalR server.
    /// </summary>
    private bool _isConnected;

    /// <summary>
    /// Event raised when the client is connected to the SignalR server.
    /// </summary>
    public event Action Connected;

    /// <summary>
    /// Event raised when the client is disconnected from the SignalR server.
    /// </summary>
    public event Action Disconnected;

    /// <summary>
    /// Event raised when the executors have bene updated
    /// </summary>
    public event Action<List<FlowExecutorInfo>> ExecutorsUpdated;

    /// <summary>
    /// Event raised when the system is paused/unpaused
    /// </summary>
    public event Action<bool> SystemPausedUpdated;
    
    /// <summary>
    /// Event raised when the file status have bene updated
    /// </summary>
    public event Action<List<LibraryStatus>> FileStatusUpdated;

    /// <summary>
    /// Starts the client service asynchronously.
    /// </summary>
    public async Task StartAsync()
    {
        await ConnectAsync();
    }

    /// <summary>
    /// Connects to the SignalR server.
    /// </summary>
    private async Task ConnectAsync()
    {
        while (true) // Retry indefinitely
        {
            // string url = ServerUri.Replace("https", "wss").Replace("http", "ws");
            string url = ServerUri; //ServerUri.Replace("wss", "https").Replace("ws", "http");
            Logger.Instance.ILog("ServerUri: " + url);
            try
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(url)
                    .Build();

                _hubConnection.Closed += async (exception) =>
                {
                    _isConnected = false;
                    Disconnected?.Invoke();
                    await Task.Delay(TimeSpan.FromSeconds(5)); // Delay before reconnecting
                    await ConnectAsync();
                };

                _hubConnection.On<ToastData>("Toast", HandleToast);
                _hubConnection.On<Dictionary<Guid, FlowExecutorInfo>>("UpdateExecutors", UpdateExecutors);
                _hubConnection.On<List<LibraryStatus>>("UpdateFileStatus", UpdateFileStatus);
                _hubConnection.On<LibraryFile>("StartProcessing", StartProcessing);
                _hubConnection.On<LibraryFile>("FinishProcessing", FinishProcessing);
                _hubConnection.On<int>("SystemPaused", UpdateSystemPaused);

                await _hubConnection.StartAsync();

                _isConnected = true;
                Connected?.Invoke();

                return; // Connected successfully, exit the method
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to the SignalR server: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5)); // Delay before reconnecting
            }
        }
    }

    /// <summary>
    /// Handles the toast data received from the SignalR server.
    /// </summary>
    /// <param name="data">The toast data.</param>
    private void HandleToast(ToastData data)
    {
        switch (data.Type)
        {
            case LogType.Info:
                Toast.ShowInfo(data.Message);
                break;
            case LogType.Debug:
                Toast.ShowSuccess(data.Message);
                break;
            case LogType.Warning:
                Toast.ShowWarning(data.Message);
                break;
            case LogType.Error:
                Toast.ShowError(data.Message);
                break;
        }
    }

    /// <summary>
    /// Called when the executors have changed
    /// </summary>
    /// <param name="executors">the executors</param>
    private void UpdateExecutors(Dictionary<Guid, FlowExecutorInfo> executors)
    {   
        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSize(1);
        cacheEntryOptions.SetPriority(CacheItemPriority.High);
        cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        _cache.Set("FlowExecutorInfo", executors.Values.ToList(), cacheEntryOptions);

        var list = executors.Values.ToList();
        ExecutorsUpdated?.Invoke(list);
        FireJsEvent("UpdateExecutors", list);
    }

    private void UpdateFileStatus(List<LibraryStatus> data)
    {
        FileStatusUpdated?.Invoke(data);
    }
    /// <summary>
    /// Called when the system is paused/unpaused
    /// </summary>
    /// <param name="minutes">the how many minutes to pause the system for</param>
    private void UpdateSystemPaused(int minutes)
    {
        SetPausedFor(minutes);
    }
    
    /// <summary>
    /// Called when a file starts processing
    /// </summary>
    /// <param name="file">the file</param>
    private void StartProcessing(LibraryFile file)
        => FireJsEvent("StartProcessing", file);
    
    /// <summary>
    /// Called when a file is finished processing
    /// </summary>
    /// <param name="file">the file</param>
    private void FinishProcessing(LibraryFile file)
        => FireJsEvent("FinishProcessing", file);
        
    
    /// <summary>
    /// Represents the toast data received from the SignalR server.
    /// </summary>
    private class ToastData
    {
        /// <summary>
        /// Gets or sets the type of the toast.
        /// </summary>
        public LogType Type { get; set; }

        /// <summary>
        /// Gets or sets the toast message.
        /// </summary>
        public string Message { get; set; }
    }
}
