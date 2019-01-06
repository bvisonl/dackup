﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using Serilog;

using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;

namespace dackup
{
    class Program
    {
        public static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
            .CreateLogger();

            var app = new CommandLineApplication
            {
                Name = "dackup",
                Description = "A backup app for your server or database or desktop",
            };

            app.HelpOption(inherited: true);

            app.Command("gen", genCmd =>
            {
                genCmd.Description = "Generate a config file";

                var modelName = genCmd.Argument("model", "Name of the model").IsRequired();

                genCmd.OnExecute(() =>
                {
                    Console.WriteLine("modleName = " + modelName.Value);
                    return 1;
                });
            });

            app.Command("perform", performCmd =>
            {
                performCmd.Description = "Performing your backup by config";

                var configFile = performCmd.Option("--config-file  <FILE>", "Required. The File name of the config.", CommandOptionType.SingleValue)
                                            .IsRequired()
                                            .Accepts(v => v.ExistingFile());
                var logPath = performCmd.Option("--log-path  <PATH>", "op. The File path of the log.", CommandOptionType.SingleValue);
                var tmpPath = performCmd.Option("--tmp-path  <PATH>", "op. The tmp path.", CommandOptionType.SingleValue);

                BackupContext.Create(Path.Join(logPath.Value(), "dackup.log"), tmpPath.Value());

                performCmd.OnExecute(() =>
                {
                    var configFilePath = configFile.Value();
                    var configFileInfo = new FileInfo(configFilePath);
                    var configurationBuilder = new ConfigurationBuilder();
                    configurationBuilder.SetBasePath(configFileInfo.Directory.FullName);
                    configurationBuilder.AddXmlFile(configFileInfo.Name);                    
                    var configRoot = configurationBuilder.Build();
                    
                    Log.Information("======== Dackup start ========");

                    // run backup
                    var backupTaskList = ParseBackupTaskFromConfig(configRoot);
                    var backupTaskResult = new List<Task<BackupTaskResult>>();
                    backupTaskList.ForEach(task =>
                    {
                        var result = task.BackupAsync();
                        backupTaskResult.Add(result);
                    });
                    var backupTasks = Task.WhenAll(backupTaskResult.ToArray());
                    try
                    {
                        backupTasks.Wait();
                    }
                    catch (AggregateException)
                    { 
                    }
                    
                    Log.Information("======== Dackup start storage task ========");

                    // run store
                    var storageList = ParseStorageFromConfig(configRoot);
                    storageList.ForEach(storage =>
                    {
                        BackupContext.Current.GenerateFilesList.ForEach(file =>
                        {
                            storage.UploadAsync(file);
                        });
                        storage.PurgeAsync();
                    });

                    Log.Information("======== Dackup start notify task ========");

                    // run notify
                    var notifyList = ParseNotifyFromConfig(configRoot);
                    notifyList.ForEach(notify =>
                    {
                        notify.NotifyAsync();
                    });

                    Log.CloseAndFlush();

                    Log.Information("======== Dackup done ========");

                    return 1;
                });

            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            return app.Execute(args);
        }

        private static List<IBackupTask> ParseBackupTaskFromConfig(IConfigurationRoot configRoot)
        {
            return null;
        }
        private static List<IStorage> ParseStorageFromConfig(IConfigurationRoot configRoot)
        {
            return null;
        }
        private static List<INotify> ParseNotifyFromConfig(IConfigurationRoot configRoot)
        {
            return null;
        }

    }
}