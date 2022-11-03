﻿using FileFlows.Plugin;
using FileFlows.ServerShared.Services;
using FileFlows.Shared.Helpers;
using FileFlows.Shared.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Net;
using System.Net.Http;
using Azure;
using FileFlows.ServerShared;
using FileFlows.ServerShared.Models;
using Microsoft.Extensions.Logging;

namespace FileFlows.FlowRunner
{
    /// <summary>
    /// Flow Runner
    /// </summary>
    public class Program
    {
        public static Guid Uid { get; private set; }

        public static void Main(string[] args)
        {
            LogInfo("Flow Runner Version: " + Globals.Version);
            int exitCode = 0;
            ServicePointManager.DefaultConnectionLimit = 50;
            try
            {
                args ??= new string[] { };
                bool server = args.Any(x => x.ToLower() == "--server");

                string uid = GetArgument(args, "--uid");
                if (string.IsNullOrEmpty(uid))
                    throw new Exception("uid not set.");
                Uid = Guid.Parse(uid);

                string tempPath = GetArgument(args, "--tempPath");
                if (string.IsNullOrEmpty(tempPath) || Directory.Exists(tempPath) == false)
                    throw new Exception("Temp path doesnt exist: " + tempPath);
                
                string cfgPath = GetArgument(args, "--cfgPath");
                if (string.IsNullOrEmpty(cfgPath) || Directory.Exists(cfgPath) == false)
                    throw new Exception("Configuration Path doesnt exist: " + cfgPath);

                string cfgFile = Path.Combine(cfgPath, "config.json");
                if(File.Exists(cfgFile) == false)
                    throw new Exception("Configuration file doesnt exist: " + cfgFile);


                string cfgKey = GetArgument(args, "--cfgKey");
                if (string.IsNullOrEmpty(cfgKey))
                    throw new Exception("Configuration Key not set");
                bool noEnrypt = cfgKey == "NO_ENCRYPT";
                string cfgJson;
                if (noEnrypt)
                {
                    LogInfo("No Encryption for Node configuration");
                    cfgJson = File.ReadAllText(cfgFile);
                }
                else
                {
                    LogInfo("Using configuration encryption key: " + cfgKey);
                    cfgJson = ConfigDecrypter.DecryptConfig(cfgFile, cfgKey);
                }


                var config = JsonSerializer.Deserialize<ConfigurationRevision>(cfgJson);

                string baseUrl = GetArgument(args, "--baseUrl");
                if (string.IsNullOrEmpty(baseUrl))
                    throw new Exception("baseUrl not set");
                LogInfo("Base URL: " + baseUrl);
                Service.ServiceBaseUrl = baseUrl;

                string hostname = GetArgument(args, "--hostname");
                if(string.IsNullOrWhiteSpace(hostname))
                    hostname = Environment.MachineName;

                Globals.IsDocker = args.Contains("--docker");

                string workingDir = Path.Combine(tempPath, "Runner-" + uid);
                Directory.CreateDirectory(workingDir);

                var libfileUid = Guid.Parse(GetArgument(args, "--libfile"));
                Shared.Helpers.HttpHelper.Client = Shared.Helpers.HttpHelper.GetDefaultHttpHelper(ServerShared.Services.Service.ServiceBaseUrl);
                Execute(new()
                {
                    IsServer = server,
                    Config = config,
                    ConfigDirectory = cfgPath,
                    TempDirectory = tempPath,
                    LibraryFileUid = libfileUid,
                    WorkingDirectory = workingDir,
                    Hostname = hostname
                });

            }
            catch (Exception ex)
            {
                exitCode = 1;
                LogInfo("Error: " + ex.Message + Environment.NewLine + ex.StackTrace);
                while(ex.InnerException != null)
                {
                    LogInfo("Error: " + ex.Message + Environment.NewLine + ex.StackTrace);
                    ex = ex.InnerException;
                }
                return;
            }
            finally
            {
                LogInfo("Exit Code: " + exitCode);
                Environment.ExitCode = exitCode;
            }
        }

        static string GetArgument(string[] args, string name)
        {
            int index = args.Select(x => x.ToLower()).ToList().IndexOf(name.ToLower());
            if (index < 0)
                return string.Empty;
            if (index >= args.Length - 1)
                return string.Empty;
            return args[index + 1];
        }


        static void Execute(ExecuteArgs args)
        {
            ProcessingNode node;
            var nodeService = NodeService.Load();
            try
            {
                string address = args.IsServer ? "INTERNAL_NODE" : args.Hostname;
                LogInfo("Address: "+ address);
                var nodeTask = nodeService.GetByAddress(address);
                LogInfo("Waiting on node task");
                nodeTask.Wait();
                LogInfo("Completed node task");
                node = nodeTask.Result;
                if (node == null)
                    throw new Exception("Failed to load node!!!!");
                LogInfo("Node SignalrUrl: " + node.SignalrUrl);
            }
            catch (Exception ex)
            {
                LogInfo("Failed to register node: " + ex.Message + Environment.NewLine + ex.StackTrace);
                throw;
            }

            FlowRunnerCommunicator.SignalrUrl = node.SignalrUrl;

            var libFileService = LibraryFileService.Load();
            var libFile = libFileService.Get(args.LibraryFileUid).Result;
            if (libFile == null)
            {
                LogInfo("Library file not found, must have been deleted from the library files.  Nothing to process");
                return; // nothing to process
            }

            string workingFile = node.Map(libFile.Name);

            var libfileService = LibraryFileService.Load();
            var lib = args.Config.Libraries.FirstOrDefault(x => x.Uid == libFile.Library.Uid);
            if (lib == null)
            {
                LogInfo("Library was not found, deleting library file");
                libfileService.Delete(libFile.Uid).Wait();
                return;
            }

            FileSystemInfo file = lib.Folders ? new DirectoryInfo(workingFile) : new FileInfo(workingFile);
            bool fileExists = file.Exists; // set to variable so we can set this to false in debugging easily
            if (fileExists == false)
            {
                if(args.IsServer == false)
                {
                    // check if the library file exists on the server, if it does its a mapping issue
                    bool exists = libfileService.ExistsOnServer(libFile.Uid).Result;
                    if (exists == true)
                    {
                        LogError("Library file exists but is not accessible from node: " + file.FullName);
                        libFile.Status = FileStatus.MappingIssue;
                        libFile.ExecutedNodes = new List<ExecutedNode>();                        
                        libfileService.Update(libFile).Wait();
                        return;
                    }                    
                }
                LogInfo("Library file does not exist, deleting from library files: " + file.FullName);
                libfileService.Delete(libFile.Uid).Wait();
                return;
            }

            var flow = args.Config.Flows.FirstOrDefault(x => x.Uid == (lib.Flow?.Uid ?? Guid.Empty));
            if (flow == null || flow.Uid == Guid.Empty)
            {
                LogInfo("Flow not found, cannot process file: " + file.FullName);
                libFile.Status = FileStatus.FlowNotFound;
                libfileService.Update(libFile).Wait();
                return;
            }

            libFile.Status = FileStatus.Processing;
            
            // update the library file to reference the updated flow (if changed)
            if (libFile.Flow?.Name != flow.Name || libFile.Flow?.Uid != flow.Uid)
            {
                libFile.Flow = new ObjectReference
                {
                    Uid = flow.Uid,
                    Name = flow.Name,
                    Type = typeof(Flow)?.FullName ?? String.Empty
                };
                libfileService.Update(libFile).Wait();
            }

            libFile.ProcessingStarted = DateTime.Now;
            libfileService.Update(libFile).Wait();

            var info = new FlowExecutorInfo
            {
                Uid = Program.Uid,
                Config = args.Config,
                ConfigDirectory = args.ConfigDirectory,
                LibraryFile = libFile,
                Log = String.Empty,
                NodeUid = node.Uid,
                NodeName = node.Name,
                RelativeFile = libFile.RelativePath,
                Library = libFile.Library,
                TotalParts = flow.Parts.Count,
                CurrentPart = 0,
                CurrentPartPercent = 0,
                CurrentPartName = string.Empty,
                StartedAt = DateTime.Now,
                WorkingFile = workingFile,
                IsDirectory = lib.Folders,
                LibraryPath = lib.Path, 
                Fingerprint = lib.UseFingerprinting,
                InitialSize = lib.Folders ? GetDirectorySize(workingFile) : new FileInfo(workingFile).Length
            };
            info.LibraryFile.OriginalSize = info.InitialSize;
            LogInfo("Initial Size: " + info.InitialSize);
            

            var runner = new Runner(info, flow, node, args.WorkingDirectory);
            runner.Run();
        }


        private static long GetDirectorySize(string path)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);
                return dir.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(x => x.Length);
            }
            catch (Exception ex)
            {
                LogInfo("Failed retrieving directory size: " + ex.Message);
                return 0;
            }
        }


        internal static void LogInfo(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [INFO] -> " + message);
        }
        internal static void LogWarning(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [WARN] -> " + message);
        }
        internal static void LogError(string message)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [ERRR] -> " + message);
        }
    }
}