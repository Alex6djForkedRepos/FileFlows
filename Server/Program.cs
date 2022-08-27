using System.Collections;
using System.Net;
using Avalonia;
using FileFlows.Plugin;
using FileFlows.Server.Database;
using FileFlows.Server.Database.Managers;
using FileFlows.Server.Helpers;
using FileFlows.Server.Ui;
using FileFlows.Shared.Helpers;
using FileFlows.Shared.Models;

namespace FileFlows.Server;

public class Program
{
    /// <summary>
    /// Gets if this is running inside a docker container
    /// </summary>
    public static bool Docker => ServerShared.Globals.IsDocker;
    private static Mutex appMutex = null;
    const string appName = "FileFlowsServer";
    /// <summary>
    /// General cache used by the server
    /// </summary>
    internal static CacheStore GeneralCache = new ();

    public static void Main(string[] args)
    {
        #if(DEBUG)
        if (Globals.IsWindows)
        {
            new ProcessHelper(null, false).ExecuteShellCommand(new () {
                Command = "Taskkill",
                Arguments = "/IM ffmpeg.exe /F"
            });
        }
        #endif
        try
        {
            if (args.Any(x =>
                    x.ToLower() == "--help" || x.ToLower() == "-?" || x.ToLower() == "/?" || x.ToLower() == "/help" ||
                    x.ToLower() == "-help"))
            {
                Console.WriteLine("FileFlows v" + Globals.Version);
                Console.WriteLine("--no-gui: To hide the GUI");
                return;
            }
            
            
            if (Globals.IsLinux && args?.Any(x => x == "--systemd") == true)
            {
                if(args?.Any(x => x == "--uninstall") == true)
                    SystemdService.Uninstall(false);
                else
                    SystemdService.Install(DirectoryHelper.BaseDirectory, isNode: false);
                return;
            }
            
            ServerShared.Globals.IsDocker = args?.Any(x => x == "--docker") == true;
            ServerShared.Globals.IsSystemd = args?.Any(x => x == "--systemd-service") == true;
            var noGui = args?.Any((x => x.ToLower() == "--no-gui")) == true || Docker;
            DirectoryHelper.Init(Docker, false);
            
            
            if(File.Exists(Path.Combine(DirectoryHelper.BaseDirectory, "server-upgrade.bat")))
                File.Delete(Path.Combine(DirectoryHelper.BaseDirectory, "server-upgrade.bat"));
            if(File.Exists(Path.Combine(DirectoryHelper.BaseDirectory, "server-upgrade.sh")))
                File.Delete(Path.Combine(DirectoryHelper.BaseDirectory, "server-upgrade.sh"));

            ServicePointManager.DefaultConnectionLimit = 50;

            InitializeLoggers();

            WriteLogHeader(args);

            CleanDefaultTempDirectory();

            if (Docker == false)
            {
                appMutex = new Mutex(true, appName, out bool createdNew);
                if (createdNew == false)
                {
                    // app is already running;
                    if (noGui)
                    {
                        Console.WriteLine("An instance of FileFlows is already running");
                    }
                    else
                    {
                        try
                        {
                            var appBuilder = BuildAvaloniaApp(true);
                            appBuilder.StartWithClassicDesktopLifetime(args);
                        }
                        catch (Exception) { }
                    }
            
                    return;
                }
            }
            
            // create new client, this can be used by upgrade scripts, so do this before preparing database
            Shared.Helpers.HttpHelper.Client = new HttpClient();

            CheckLicense();

            if (PrepareDatabase() == false)
                return;

            if (Docker || noGui)
            {
                Console.WriteLine("Starting FileFlows Server...");
                WebServer.Start(args);
            }
            else
            {
                if(Globals.IsWindows)
                    Utils.WindowsConsoleManager.Hide();
                
                Task.Run(() =>
                {
                    Console.WriteLine("Starting FileFlows Server...");
                    WebServer.Start(args);
                });

                try
                {
                    var appBuilder = BuildAvaloniaApp();
                    appBuilder.StartWithClassicDesktopLifetime(args);
                }
                catch (Exception) { }
            }

            _ = WebServer.Stop();
            Console.WriteLine("Exiting FileFlows Server...");
        }
        catch (Exception ex)
        {
            try
            {
                Logger.Instance.ELog("Error: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
            catch (Exception) { }
            Console.WriteLine("Error: " + ex.Message + Environment.NewLine + ex.StackTrace);
        }
    }

    private static void WriteLogHeader(string[] args)
    {
        Logger.Instance.ILog(new string('=', 50));
        Thread.Sleep(1); // so log message can be written
        Logger.Instance.ILog("Starting FileFlows " + Globals.Version);
        Thread.Sleep(1); // so log message can be written
        if(Docker)
            Logger.Instance.ILog("Running inside docker container");
        Thread.Sleep(1); // so log message can be written
        Logger.Instance.DLog("Arguments: " + (args?.Any() == true ? string.Join(" ", args) : "No arguments"));
        Thread.Sleep(1); // so log message can be written
        foreach (DictionaryEntry var in Environment.GetEnvironmentVariables())
        {
            Logger.Instance.DLog($"ENV.{var.Key} = {var.Value}");
            Thread.Sleep(1); // so log message can be written
        }
        Thread.Sleep(1); // so log message can be written
        Logger.Instance.ILog(new string('=', 50));
        Thread.Sleep(1); // so log message can be written
    }

    private static void CheckLicense()
    {
        LicenseHelper.Update().Wait();
    }

    private static void InitializeLoggers()
    {
        new ServerShared.FileLogger(DirectoryHelper.LoggingDirectory, "FileFlows");
        new ConsoleLogger();
    }

    private static bool PrepareDatabase()
    {
        if (string.IsNullOrEmpty(AppSettings.Instance.DatabaseConnection) == false &&
            AppSettings.Instance.DatabaseConnection.Contains(".sqlite") == false)
        {
            // check if licensed for external db, if not force migrate to sqlite
            if (LicenseHelper.IsLicensed(LicenseFlags.ExternalDatabase) == false)
            {
                #if(DEBUG)
                // twice for debugging so we can step into it and see why
                if (LicenseHelper.IsLicensed(LicenseFlags.ExternalDatabase) == false)
                {
                }
                #endif

                Logger.Instance.WLog("No longer licensed for external database, migrating to SQLite database.");
                AppSettings.Instance.DatabaseMigrateConnection = SqliteDbManager.GetDefaultConnectionString();
            }
        }
        
        if (string.IsNullOrEmpty(AppSettings.Instance.DatabaseMigrateConnection) == false)
        {
            if (AppSettings.Instance.DatabaseConnection == AppSettings.Instance.DatabaseMigrateConnection)
            {
                AppSettings.Instance.DatabaseMigrateConnection = null;
                AppSettings.Instance.Save();
            }
            else if (AppSettings.Instance.RecreateDatabase == false &&
                     DbMigrater.ExternalDatabaseExists(AppSettings.Instance.DatabaseMigrateConnection))
            {
                Logger.Instance.ILog("Switching to existing database");
                AppSettings.Instance.DatabaseConnection = AppSettings.Instance.DatabaseMigrateConnection;
                AppSettings.Instance.DatabaseMigrateConnection = null;
                AppSettings.Instance.RecreateDatabase = false;
                AppSettings.Instance.Save();
            }
            else
            {
                Logger.Instance.ILog("Database migration starting");
                bool migrated = DbMigrater.Migrate(AppSettings.Instance.DatabaseConnection,
                    AppSettings.Instance.DatabaseMigrateConnection);
                if (migrated)
                    AppSettings.Instance.DatabaseConnection = AppSettings.Instance.DatabaseMigrateConnection;
                else
                {
                    Logger.Instance.ELog("Database migration failed, reverting to previous database settings");
                    #if(DEBUG)
                    throw new Exception("Migration failed");
                    #endif
                }

                AppSettings.Instance.DatabaseMigrateConnection = null;
                AppSettings.Instance.RecreateDatabase = false;
                AppSettings.Instance.Save();
            }
        }
            
        // initialize the database
        if (DbHelper.Initialize().Result == false)
        {
            Logger.Instance.ELog("Failed initializing database");
            return false;
        }
        Logger.Instance.ILog("Database initialized");
            
        // run any upgrade code that may need to be run
        var settings = DbHelper.Single<Settings>().Result;
        new Upgrade.Upgrader().Run(settings);
        DbHelper.RestoreDefaults();

        new DatabaseLogger();
        
        return true;
    }

    /// <summary>
    /// Clean the default temp directory on startup
    /// </summary>
    private static void CleanDefaultTempDirectory()
    {
        string tempDir = Docker ? Path.Combine(DirectoryHelper.DataDirectory, "temp") : Path.Combine(DirectoryHelper.BaseDirectory, "Temp");
        DirectoryHelper.CleanDirectory(tempDir);
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(bool messagebox = false)
        => (messagebox ? AppBuilder.Configure<MessageApp>() : AppBuilder.Configure<App>())
            .UsePlatformDetect();
}
