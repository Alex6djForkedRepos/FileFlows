using System.Data;
using FileFlows.Client.Helpers;
using Microsoft.AspNetCore.Components;

namespace FileFlows.Client.Components.Widgets;

/// <summary>
/// File Overview Widget
/// </summary>
public partial class FileOverviewWidget : ComponentBase, IDisposable
{
    /// <summary>
    /// Gets or sets the client service
    /// </summary>
    [Inject] public ClientService ClientService { get; set; }
    
    /// <summary>
    /// Gets if this is for the files processed, or the storage saved
    /// </summary>
    [Parameter] public bool IsFilesProcessed { get; set; }
    
    private int _Mode = 1;
    private string Color = "green";
    private string Label = string.Empty;
    private string Icon = string.Empty;
    private string Total;
    private string Average;
    private double[] Data = [];//[10, 20, 30, 20, 15, 16, 27, 45.34, 41.2, 38.2];
    private FileOverviewData? CurrentData;
    /// <summary>
    /// Gets or sets the selected mode
    /// </summary>
    private int Mode
    {
        get => _Mode;
        set
        {
            _Mode = value;
            SetValues();

            StateHasChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Color = IsFilesProcessed ? "green" : "blue";
        Label = IsFilesProcessed ? "Files Processed" : "Storage Saved";
        Icon = IsFilesProcessed ? "far fa-checked-circle" : "fas fa-hdd";
        
        ClientService.FileOverviewUpdated += OnFileOverviewUpdated;
        if (ClientService.CurrentFileOverData != null)
        {
            CurrentData = ClientService.CurrentFileOverData;
            SetValues();
        }
    }

    /// <summary>
    /// Called when the file overview is updated
    /// </summary>
    /// <param name="data">the updated data</param>
    private void OnFileOverviewUpdated(FileOverviewData data)
    {
        CurrentData = data;
        SetValues();
        StateHasChanged();
    }

    /// <summary>
    /// Sets the value based on the data
    /// </summary>
    private void SetValues()
    {
        var dataset = Mode switch
        {
            1 => CurrentData.Last7Days,
            2 => CurrentData.Last31Days,
            _ => CurrentData.Last24Hours
        };

        if (dataset.Count == 0)
            return;
        
        if (IsFilesProcessed)
        {
            int total = dataset.Sum(x => x.Value.FileCount);
            Total = total.ToString("N0");
            // average
            switch (Mode)
            {
                case 1:
                    Average = $"{Math.Round(total / 7d)} per day";
                    break;
                case 2:
                    Average = $"{Math.Round(total / 31d)} per day";
                    break;
                default:
                    Average = $"{Math.Round(total / 24d)} per hour";
                    break;
            }
            Data = dataset.Select(x => x.Value.FileCount).Select(x => (double)x).ToArray();
        }
        else
        {
            long total = dataset.Sum(x => x.Value.StorageSaved);
            Total = FileSizeFormatter.FormatSize(total, 1);
            // average
            switch (Mode)
            {
                case 1:
                    Average = $"{FileSizeFormatter.FormatSize((long)Math.Round(total / 7d), 1)} per day";
                    break;
                case 2:
                    Average = $"{FileSizeFormatter.FormatSize((long)Math.Round(total / 31d), 1)} per day";
                    break;
                default:
                    Average = $"{FileSizeFormatter.FormatSize((long)Math.Round(total / 24d), 1)} per hour";
                    break;
            }
            Data = dataset.Select(x => x.Value.StorageSaved).Select(x => (double)x).ToArray();
        }
    }

    /// <summary>
    /// Disposes of the object
    /// </summary>
    public void Dispose()
    {
        ClientService.FileOverviewUpdated -= OnFileOverviewUpdated;
    }
}