using System.Reflection;
using FileFlows.ServerShared;

namespace FileFlows.Node;

class CommandLineOptions
{
    /// <summary>
    /// Gets or sets the FileFlows server
    /// </summary>
    [CommandLineArg("The FileFlows server", "http://tower:5000/")]
    public string Server { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of this Node
    /// </summary>
    [CommandLineArg("The name of this Node", "WindowsNode")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the temporary working directory this node will use
    /// </summary>
    [CommandLineArg("The temporary working directory this node will use", "DIR")]
    public string Temp { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the base directory where the application's data will be saved.
    /// You can override the default location using the command-line parameter.
    /// Example usage:
    ///     --base-dir "/my/custom/data/path"
    /// </summary>
    [CommandLineArg("Specifies the base directory for saving application data.", "/my/custom/path", "base-dir")]
    public string BaseDir { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets if the GUI should be shown
    /// </summary>
    [CommandLineArg("If a GUI should be shown", "true", "gui")]
    public bool Gui { get; set; }
    
    /// <summary>
    /// Gets or sets if the GUI should be shown
    /// </summary>
    [CommandLineArg("The entry point of the application", "/home/user/FileFlows", "entry-point", hidden: true)]
    public string? EntryPoint { get; set; }
    
    /// <summary>
    /// Gets or sets the por the REST API will be exposed on
    /// </summary>
    [CommandLineArg("The port the REST API will be exposed on", "5000", "api-port")]
    public int ApiPort { get; set; }

    /// <summary>
    /// Gets or sets if running inside a docker container
    /// </summary>
    [CommandLineArg("If running inside a docker container", "true", "docker", true)]
    public bool Docker { get; set; }
    
    /// <summary>
    /// Gets or sets if running inside as a systemd service
    /// </summary>
    [CommandLineArg("If running inside as a systemd service", "true", "systemd-service", true)]
    public bool IsSystemd { get; set; }
    
    /// <summary>
    /// Gets or sets if should install sysetmd service
    /// </summary>
    [CommandLineArg("Installs FileFlows Node as a systemd service", "", command: "systemd")]
    public bool InstallService { get; set; }
    
    /// <summary>
    /// Gets or sets if the service is installed as root
    /// </summary>
    [CommandLineArg("If the systemd service is root", "", command: "root")]
    public bool Root { get; set; }
    
    /// <summary>
    /// Gets or sets if should uninstall the sysetmd service
    /// </summary>
    [CommandLineArg("Uninstall the systemd service", "", command: "uninstall")]
    public bool Uninstall { get; set; }
    
    /// <summary>
    /// Gets or sets if running inside a docker container
    /// </summary>
    [CommandLineArg("The location of dotnet, used to install the systemd service", "/usr/john/.dotnet", command: "dotnet")]
    public string DotNet { get; set; } = string.Empty;

    /// <summary>
    /// Parses the command line arguments
    /// </summary>
    /// <param name="args">the command line arguments</param>
    /// <returns>the command line options</returns>
    public static CommandLineOptions Parse(string[] args)
    {
        CommandLineOptions options = new();
        if (args?.Any() != true)
            return options;

        var props = typeof(CommandLineOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var ca = prop.GetCustomAttribute<CommandLineArgAttribute>();
            if (ca == null)
                continue;
            string key = ca.Command?.EmptyAsNull() ?? prop.Name.ToLower();

            if (prop.PropertyType == typeof(bool))
            {
                bool value = GetArgBooleanValue(args, "--" + key);
                prop.SetValue(options, value);
            }
            else
            {
                string strValue = GetArgValue(args, "--" + key);
                if (string.IsNullOrEmpty(strValue))
                    continue;
                prop.SetValue(options, strValue);
            }
        }

        return options;
    }

    /// <summary>
    /// Gets a string argument value
    /// </summary>
    /// <param name="key">the argument key</param>
    /// <returns>the string value or an empty string if not found</returns>
    static string GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].ToLower() == key)
                return args[i + 1];
        }

        return string.Empty;
    }
        
    /// <summary>
    /// Gets a boolean argument value
    /// </summary>
    /// <param name="key">the argument key</param>
    /// <returns>the boolean value</returns>
    static bool GetArgBooleanValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].ToLower() == key)
                return args[i + 1].ToLowerInvariant() != "false" && args[i + 1] != "0";
        }

        return string.Equals(args.Last(), key, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Prints the help to the command line
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine("FileFlows Node Version:" + Globals.Version);
        Console.WriteLine("");
        var props = typeof(CommandLineOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var ca = prop.GetCustomAttribute<CommandLineArgAttribute>();
            if (ca == null || ca.Hidden)
                continue;
            
            var key = ca.Command?.EmptyAsNull() ?? prop.Name.ToLower();
            var example = ca.Example;
            if (example == "DIR")
                example = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\FileFlows\Temp" : "/mnt/temp";
            string value = prop.PropertyType == typeof(bool) ? "true|false" : "value";
            Console.WriteLine($"--{key} [{value}]");
            Console.WriteLine($"\t{ca.Description}");
            if(string.IsNullOrWhiteSpace(example) == false)
                Console.WriteLine($"\teg --{key} {example}");
        }
    }
}

class CommandLineArgAttribute:Attribute 
{
    public string Description { get; set; }
    public string Command { get; set; }
    public string Example { get; set; }
    public bool Hidden { get; set; }

    public CommandLineArgAttribute(string description, string example, string? command = null, bool hidden = false)
    {
        this.Description = description;
        this.Example = example;
        this.Command = command ?? string.Empty;
        this.Hidden = hidden;
    }
}