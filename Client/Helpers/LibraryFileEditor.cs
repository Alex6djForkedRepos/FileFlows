namespace FileFlows.Client.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using FileFlows.Client.Components;
    using FileFlows.Client.Components.Common;
    using FileFlows.Client.Components.Inputs;
    using FileFlows.Plugin;
    using FileFlows.Shared;
    using FileFlows.Shared.Helpers;
    using FileFlows.Shared.Models;

    public class LibraryFileEditor
    {
        static string ApIUrl => "/api/library-file";

        private static async Task<RequestResult<LibraryFileModel>> GetLibraryFile(string url)
        {
#if (DEMO)
            var file = new LibraryFileModel
            {
                Name = "Demo Library File.mkv"
            };
            return new RequestResult<LibraryFileModel> { Success = true, Data = file };
#else
            return await HttpHelper.Get<LibraryFileModel>(url);
#endif
        }
        private static async Task<RequestResult<string>> GetLibraryFileLog(string url)
        {
#if (DEMO)
            return new RequestResult<string> { Success = true, Data = "This is a sample log file." };
#else
            return await HttpHelper.Get<string>(url);
#endif
        }

        public static async Task Open(Blocker blocker, Editor editor, Guid libraryItemUid)
        {
            LibraryFileModel model = null;
            string logUrl = ApIUrl + "/" + libraryItemUid+ "/log";
            blocker.Show();
            try
            {
                var result = await GetLibraryFile(ApIUrl + "/" + libraryItemUid);
                if (result.Success == false)
                {
                    Toast.ShowError(
                        result.Success || string.IsNullOrEmpty(result.Body) ? Translater.Instant("ErrorMessage.NotFound") : Translater.TranslateIfNeeded(result.Body),
                        duration: 60_000
                    );
                    return;
                }


                model = result.Data;
                if (model.Status == FileStatus.Processing)
                    logUrl += "?lines=5000";

                var logResult = await GetLibraryFileLog(logUrl);
                model.Log = (logResult.Success ? logResult.Data : string.Empty) ?? string.Empty;

            }
            finally
            {

                blocker.Hide();
            }


            if(new[] { FileStatus.Unprocessed, FileStatus.Disabled, FileStatus.Duplicate, FileStatus.OutOfSchedule }.Contains(model.Status) == false)
            {
                // show tabs
                var tabs = new Dictionary<string, List<ElementField>>();

                tabs.Add("Info", GetInfoTab(model));

                tabs.Add("Log", new List<ElementField>
                {
                    new ElementField
                    {
                        InputType = FormInputType.LogView,
                        Name = "Log",
                        Parameters = model.Status == FileStatus.Processing ? new Dictionary<string, object> {
                            { nameof(InputLogView.RefreshUrl), logUrl },
                            { nameof(InputLogView.RefreshSeconds), 5 },
                        } : null
                    }
                });

                await editor.Open("Pages.LibraryFile", model.RelativePath, null, model, tabs: tabs, large: true, readOnly: true, noTranslateTitle: true);
            }
            else
            {
                // just show basic info
                await editor.Open("Pages.LibraryFile", model.RelativePath, GetInfoTab(model), model, large: true, readOnly: true, noTranslateTitle: true);
            }
        }

        private static List<ElementField> GetInfoTab(LibraryFileModel item)
        {
            List<ElementField> fields = new List<ElementField>();

            fields.Add(new ElementField
            {
                InputType = FormInputType.TextLabel,
                Name = nameof(item.Name)
            });

            if (item.Name != item.OutputPath && string.IsNullOrEmpty(item.OutputPath) == false)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.OutputPath)
                });
            }

            fields.Add(new ElementField
            {
                InputType = FormInputType.TextLabel,
                Name = nameof(item.OriginalSize),
                Parameters = new Dictionary<string, object>
                {
                    { nameof(InputTextLabel.Formatter), nameof(FileSizeFormatter)  }
                }
            });

            if (item.Status == FileStatus.Processed)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.FinalSize),
                    Parameters = new Dictionary<string, object>
                    {
                        { nameof(InputTextLabel.Formatter), nameof(FileSizeFormatter) }
                    }
                });
            }

            if (string.IsNullOrEmpty(item.Fingerprint) == false)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.Fingerprint)
                });
            }

            if (item.Status != FileStatus.Disabled && item.Status != FileStatus.Unprocessed &&
                item.Status != FileStatus.OutOfSchedule)
            {
                if(item.Node?.Name == "FileFlowsServer")
                    item.Node.Name = "Internal Processing Node";
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.Node)
                });
            }
            
            if (string.IsNullOrEmpty(item.Flow?.Name) == false)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.Flow)
                });
            }

            if (string.IsNullOrEmpty(item.Library?.Name) == false)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.Library)
                });
            }

            if (item.ProcessingTime.TotalMilliseconds > 0)
            {
                fields.Add(new ElementField
                {
                    InputType = FormInputType.TextLabel,
                    Name = nameof(item.ProcessingTime)
                });
            }

            fields.Add(new ElementField
            {
                InputType = FormInputType.TextLabel,
                Name = nameof(item.Status)
            });

            if(item.ExecutedNodes?.Any() == true)
            {
                var flowParts = new ElementField
                {
                    InputType = FormInputType.ExecutedNodes,
                    Name = nameof(item.ExecutedNodes),
                    Parameters = new Dictionary<string, object>
                    {
                        { nameof(InputExecutedNodes.HideLabel), true },
                    }
                };
                if(item.Status != FileStatus.Processing)
                    flowParts.Parameters.Add(nameof(InputExecutedNodes.Log), item.Log);
                fields.Add(flowParts);
            }

            return fields;
        }
    }


    public class LibraryFileModel : LibraryFile
    {
        public string Log { get; set; }
    }
}