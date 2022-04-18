namespace FileFlows.Client.Components.Inputs
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Web;
    using FileFlows.Shared.Helpers;
    using Microsoft.AspNetCore.Components;
    public partial class InputLogView : Input<string>, IDisposable
    {
        [Parameter] public string RefreshUrl { get; set; }

        [Parameter] public int RefreshSeconds { get; set; }

        private string Colorized { get; set; }

        private string PreviousValue { get; set; }

        private Timer RefreshTimer;
        private bool Refreshing = false;

        protected override void OnInitialized()
        {
            base.OnInitialized();

            this.Colorized = Colorize(this.Value);

            if (string.IsNullOrEmpty(RefreshUrl) == false)
            {
                this.RefreshTimer = new Timer();
                this.RefreshTimer.AutoReset = true;
                this.RefreshTimer.Interval = RefreshSeconds > 0 ? RefreshSeconds * 1000 : 10_000;
                this.RefreshTimer.Elapsed += RefreshTimerElapsed;
                this.RefreshTimer.Start();
            }
        }

        public void Dispose()
        {
            if (this.RefreshTimer != null)
            {
                this.RefreshTimer.Stop();
                this.RefreshTimer.Elapsed -= RefreshTimerElapsed;
            }
        }

        private void RefreshTimerElapsed(object sender, EventArgs e)
        {
            if (Refreshing)
                return;
            Refreshing = true;
            Task.Run(async () =>
            {
                try
                {
                    var refreshResult = await HttpHelper.Get<string>(this.RefreshUrl);
                    if (refreshResult.Success == false)
                        return;
                    this.Value = refreshResult.Data;
                    this.Colorized = Colorize(this.Value); 
                    this.StateHasChanged();
                }
                catch (Exception) { }
                finally
                {
                    Refreshing = false;
                }
            });
        }

        private string Colorize(string log)
        {
            if (log == null)
                return string.Empty;

            if (log.IndexOf("<div") >= 0)
                return log;

            StringBuilder colorized = new StringBuilder();
            if (string.IsNullOrWhiteSpace(PreviousValue) == false && log.StartsWith(PreviousValue))
            {
                // this avoid us from redoing stuff we've already done
                colorized.Append(this.Colorized);
                log = log.Substring(PreviousValue.Length).TrimStart();
            }
            PreviousValue = log;

            colorized.Append(LogToHtml.Convert(log));
            string result = colorized.ToString();
            return result;
        }

    }
}