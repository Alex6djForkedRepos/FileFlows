namespace FileFlows.Client.Components.Inputs
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using FileFlows.Client.Components.Dialogs;
    using Microsoft.AspNetCore.Components;

    public partial class InputFile : Input<string>
    {
        [Parameter]
        public string[] Extensions { get; set; }

        [Parameter]
        public bool Directory { get; set; }

        private Dictionary<string, object> _Variables = new Dictionary<string, object>();

        [Parameter]
        public Dictionary<string, object> Variables
        {
            get => _Variables;
            set { _Variables = value ?? new Dictionary<string, object>(); }
        }

        public override bool Focus() => FocusUid();
        async Task Browse()
        {
            string result = await FileBrowser.Show(this.Value, directory: Directory, extensions: Extensions);
            if (string.IsNullOrEmpty(result))
                return;
            this.ClearError();
            this.Value = result;
        }
    }
}