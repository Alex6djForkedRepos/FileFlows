﻿using FileFlows.Server.Database;
using FileFlows.Server.Database.Managers;
using FileFlows.Server.Helpers;
using FileFlows.Shared.Models;
using MySql.Data.MySqlClient;

namespace FileFlows.Server.Upgrade;

public class Upgrader
{
    public void Run(Settings settings)
    {
        var currentVersion = string.IsNullOrWhiteSpace(settings.Version) ? new Version() : Version.Parse(settings.Version);
        Logger.Instance.ILog("Current version: " + currentVersion);
        // check if current version is even set, and only then do we run the upgrades
        // so on a clean install these do not run
        if (currentVersion > new Version(0, 4, 0))
        {
            if (new Version(settings.Version).ToString() != new Version(Globals.Version).ToString())
            {
                // first backup the database
                if (DbHelper.UseMemoryCache)
                {
                    try
                    {
                        Logger.Instance.ILog("Backing up database");
                        string source = SqliteDbManager.SqliteDbFile;
                        string dbBackup = source.Replace(".sqlite",
                            "-" + currentVersion.Major + "." + currentVersion.Minor + "." + currentVersion.Build +
                            ".sqlite.backup");
                        File.Copy(source, dbBackup);
                        Logger.Instance.ILog("Backed up database to: " + dbBackup);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    // backup a MySQL db using the migrator
                    try
                    {
                        Logger.Instance.ILog("Backing up database, please wait this may take a while");
                        string dbBackup = DatabaseBackupManager.CreateBackup(AppSettings.Instance.DatabaseConnection,
                            DirectoryHelper.DatabaseDirectory, currentVersion);
                        Logger.Instance.ILog("Backed up database to: " + dbBackup);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.ELog("Failed creating database backup: " + ex.Message);
                    }
                }
            }
            
            if (currentVersion < new Version(0, 8, 3))
                new Upgrade_0_8_3().Run(settings);
            if (currentVersion < new Version(0, 8, 4))
                new Upgrade_0_8_4().Run(settings);
            if (currentVersion < new Version(0, 9, 0))
                new Upgrade_0_9_0().Run(settings);
            if (currentVersion < new Version(0, 9, 1))
                new Upgrade_0_9_1().Run(settings);
            if (currentVersion < new Version(0, 9, 2, 1792))
                new Upgrade_0_9_2().Run(settings);
            if (currentVersion < new Version(0, 9, 4)) // 0.9.4 because 1.0.0 was originally 0.9.4 
                new Upgrade_1_0_0().Run(settings);
            if (currentVersion < new Version(1, 0, 2))  
                new Upgrade_1_0_2().Run(settings);
            if (currentVersion < new Version(1, 0, 5))  
                new Upgrade_1_0_5().Run(settings);
            if (currentVersion < new Version(1, 0, 5, 2060))  
                new Upgrade_1_0_5().Run2ndUpgrade(settings);
            if (currentVersion < new Version(1, 0, 9, 2190))  
                new Upgrade_1_0_9().Run(settings);
            if (currentVersion < new Version(1, 0, 10))  
                new Upgrade_1_0_10().Run(settings);
            if (currentVersion < new Version(1, 1, 0, 2246))  
                new Upgrade_1_1_0().Run(settings);
        }

        // save the settings
        if (settings.Version != Globals.Version.ToString())
        {
            Logger.Instance.ILog("Saving version to database");
            settings.Version = Globals.Version.ToString();
            // always increase the revision when the version changes
            settings.Revision += 1;
            DbHelper.Update(settings).Wait();
        }
        Logger.Instance.ILog("Finished checking upgrade scripts");
    }
}
