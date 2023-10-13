﻿using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Popstation;
using Popstation.Database;
using PSXPackager.Common;

namespace PSXPackager
{
    public static class Results
    {
        public const int OK = 0;
        public const int ERROR = 1;
        public const int CANCELLED = 2;
        public const int INVALID_INPUT = 3;
    }

    class Program
    {
        private static CancellationTokenSource _cancellationTokenSource;

        static void CancelEventHandler(object sender, ConsoleCancelEventArgs args)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                Console.WriteLine("Stopping...");
                _cancellationTokenSource.Cancel();
            }
            args.Cancel = true;
        }

        static int Main(string[] args)
        {
            var result = 0;

            _cancellationTokenSource = new CancellationTokenSource();

            Console.CancelKeyPress += CancelEventHandler;

            var tempPath = Path.Combine(Path.GetTempPath(), "PSXPackager");

            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            Parser.Default.ParseArguments<Options>(args)
                 .WithParsed<Options>(o =>
                 {
                     var version = Assembly.GetExecutingAssembly().GetName().Version;

                     var intro = $"PSXPackager v{version.Major}.{version.Minor}.{version.Build} by RupertAvery\r\n";

                     Console.WriteLine(intro);

                     if (o.CompressionLevel < 0 || o.CompressionLevel > 9)
                     {
                         Console.WriteLine($"Invalid compression level, please enter a value from 0 to 9");
                         return;
                     }

                     if (!string.IsNullOrEmpty(o.Discs))
                     {
                         if (!Regex.IsMatch(o.Discs, "\\d(,\\d)*"))
                         {
                             Console.WriteLine($"Invalid discs specification, please enter a comma separated list of values from 1-5");
                             return;
                         }
                     }

                     if (!string.IsNullOrEmpty(o.InputPath))
                     {
                         Console.WriteLine($"Input : {o.InputPath}");
                     }

                     if (string.IsNullOrEmpty(o.OutputPath))
                     {
                         if (!string.IsNullOrEmpty(o.InputPath))
                         {
                             o.OutputPath = Path.GetDirectoryName(o.InputPath);
                         }
                     }

                     Console.WriteLine($"Output: {o.OutputPath}");
                     Console.WriteLine($"Compression Level: {o.CompressionLevel}");

                     if (o.OverwriteIfExists)
                     {
                         Console.WriteLine("WARNING: You have chosen to overwrite all files in the output directory!");
                     }

                     var resourceOptionsCount = (o.ExtractResources ? 1 : 0) + (o.ImportResources ? 1 : 0) + (o.GenerateResourceFolders ? 1 : 0);

                     if (resourceOptionsCount > 1)
                     {
                         Console.WriteLine($"Invalid option, please select only one of extract, import, or generate");
                         return;
                     }

                     if (string.IsNullOrEmpty(o.ResourceFormat))
                     {
                         if (o.ExtractResources)
                         {
                             o.ResourceFormat = "%FILENAME%\\%RESOURCE%.%EXT%";
                         }
                         else if (o.ImportResources)
                         {
                             o.ResourceFormat = "%FILENAME%\\%RESOURCE%.%EXT%";
                         }
                         else if (o.GenerateResourceFolders)
                         {
                             o.ResourceFormat = "%FILENAME%";
                         }
                     }

                     Console.WriteLine();

                     var files = new List<string>();

                     if (!string.IsNullOrEmpty(o.InputPath))
                     {
                         if (PathIsDirectory(o.InputPath))
                         {
                             files.AddRange(GetFilesFromDirectory(o.InputPath, null, o.Recursive));
                         }
                         else
                         {
                             var filename = Path.GetFileName(o.InputPath);
                             var path = Path.GetDirectoryName(o.InputPath);
                             if (!string.IsNullOrEmpty(path) && PathIsDirectory(path) && ContainsWildCards(filename))
                             {
                                 files.AddRange(GetFilesFromDirectory(path, filename, o.Recursive));
                             }
                             else
                             {
                                 if (ContainsWildCards(filename))
                                 {
                                     files.AddRange(GetFilesFromDirectory(".", filename, o.Recursive));
                                 }
                                 else
                                 {
                                     files.Add(o.InputPath);
                                 }
                             }
                         }

                     }

                     var discs = string.IsNullOrEmpty(o.Discs)
                         ? Enumerable.Range(1, 5).ToList()
                         : o.Discs.Split(new char[] { ',' }).Select(int.Parse).ToList();

                     var options = new ProcessOptions()
                     {
                         Files = files,
                         OutputPath = o.OutputPath,
                         TempPath = tempPath,
                         Discs = discs,
                         CheckIfFileExists = !o.OverwriteIfExists,
                         SkipIfFileExists = o.SkipIfExists,
                         FileNameFormat = o.FileNameFormat,
                         CompressionLevel = o.CompressionLevel,
                         Verbosity = o.Verbosity,
                         Log = o.Log,
                         ExtractResources = o.ExtractResources,
                         ImportResources = o.ImportResources,
                         GenerateResourceFolders = o.GenerateResourceFolders,
                         ResourceFormat = o.ResourceFormat,
                         ResourceRoot = o.ResourceRoot,
                     };

                     result = ProcessFiles(options);
                 });

            return result;
        }

        private static IEnumerable<string> GetFilesFromDirectory(string path, string filterExpression, bool recursive)
        {
            // https://github.com/adamhathcock/sharpcompress/blob/master/FORMATS.md
            var supportedFiles = new List<string>() {
                ".rar", ".zip", ".tar", ".gz", ".7z",
                ".bin", ".cue", ".img", ".iso", ".pbp"
            };

            if (string.IsNullOrEmpty(filterExpression))
            {
                filterExpression = string.Join(";", supportedFiles);
            }

            var filters = filterExpression.Split(new char[] { ';', '|' });

            foreach (var filter in filters)
            {
                string tempFilter = filter;

                if (filter.StartsWith("."))
                {
                    tempFilter = $"*{filter}";
                }

                var files = Directory.GetFiles(path, tempFilter);

                foreach (var file in files)
                {
                    if (supportedFiles.Contains(Path.GetExtension(file).ToLower()))
                        yield return file;
                }

                if (recursive)
                {
                    var dirs = Directory.GetDirectories(path);
                    foreach (var dir in dirs)
                    {
                        if (dir != "." && dir != "..")
                        {
                            foreach (var file in GetFilesFromDirectory(dir, filterExpression, recursive))
                            {
                                if (supportedFiles.Contains(Path.GetExtension(file).ToLower()))
                                    yield return file;
                            }
                        }
                    }
                }
            }
        }

        private static bool ContainsWildCards(string filename)
        {
            return filename.Contains("?") || filename.Contains("*");
        }

        private static bool PathIsDirectory(string path)
        {
            if (ContainsWildCards(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;
        }

        private static int ProcessFiles(ProcessOptions options)
        {
            var result = 0;

            var eventHandler = new EventHandler();
            var notifier = new AggregateNotifier();

            var consoleNotifer = new ConsoleNotifier(options.Verbosity);
            notifier.Add(consoleNotifer);

            if (options.Log)
            {
                var logNotifier = new LogNotifier(DateTime.Now.ToString("yyyyMMdd-hhmmss") + ".log");
                notifier.Add(logNotifier);
            }

            var gameDb = new GameDB(Path.Combine(ApplicationInfo.AppPath, "Resources", "gameInfo.db"));

            var processing = new Processing(notifier, eventHandler, gameDb);

            notifier.Notify(PopstationEventEnum.ProcessingStart, null);

            if (options.Files.Count == 0)
            {
                notifier.Notify(PopstationEventEnum.Error, "No files matched!");
                result = Results.ERROR;
            }
            else if (options.Files.Count > 1)
            {
                notifier.Notify(PopstationEventEnum.Info, $"Matched {options.Files.Count} files");

                var i = 1;
                var processed = 0;

                try
                {
                    foreach (var file in options.Files)
                    {
                        if (!File.Exists(file))
                        {
                            notifier.Notify(PopstationEventEnum.Error, $"Could not find file '{file}'");
                            continue;
                        }

                        notifier.Notify(PopstationEventEnum.Info, $"Processing {i} of {options.Files.Count}");
                        notifier.Notify(PopstationEventEnum.FileName, $"Processing {file}");

                        var processResult = processing.ProcessFile(file,
                            options,
                            _cancellationTokenSource.Token);

                        if (_cancellationTokenSource.Token.IsCancellationRequested || eventHandler.Cancelled)
                        {
                            result = Results.CANCELLED;
                            break;
                        }

                        processed += processResult ? 1 : 0;

                        i++;
                    }

                }
                finally
                {
                    notifier.Notify(PopstationEventEnum.Info, $"{i - 1} files processed");
                }

            }
            else
            {
                var file = options.Files[0];

                if (!File.Exists(file))
                {
                    notifier.Notify(PopstationEventEnum.Error, $"Could not find file '{file}'");
                    return Results.INVALID_INPUT;
                }

                notifier.Notify(PopstationEventEnum.FileName, $"Processing {file}");

                var processResult = processing.ProcessFile(file, options, _cancellationTokenSource.Token);

                result = processResult ? 0 : (_cancellationTokenSource.Token.IsCancellationRequested ? Results.CANCELLED : Results.ERROR);
            }

            notifier.Notify(PopstationEventEnum.ProcessingComplete, null);

            return result;
        }


    }
}
