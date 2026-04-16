using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace UpdateSkriptApp.Services;

public class LogWatcher : ILogWatcher
{
    private readonly IFileSystem _fileSystem;

    public LogWatcher(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task MonitorSetupLogAsync(string logPath, Func<bool> isProcessActive)
    {
        long lastLogPos = 0;
        int lastPct = 1;
        string lastLabel = "Initializing...";

        var phases = new (string Pattern, int Pct, string Label)[]
        {
            ("Initializing", 5, "Initializing..."),
            ("PreDownload", 10, "Preparing for download..."),
            ("PreInstall", 20, "Preparing for installation..."),
            ("Install", 30, "Installing Windows 11 25H2..."),
            ("Finalize", 60, "Finalizing installation..."),
            (@"Post\s*Install", 80, "Post-installation cleanup..."),
            ("Success|complete", 95, "Almost done...")
        };

        var cts = new CancellationTokenSource();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Phase 3: Windows 11 Upgrade[/]", maxValue: 100);
                task.Value = lastPct;

                while (isProcessActive() || _fileSystem.FileExists(logPath))
                {
                    if (_fileSystem.FileExists(logPath))
                    {
                        try
                        {
                            using var fs = _fileSystem.OpenRead(logPath);
                            if (fs.Length > lastLogPos)
                            {
                                fs.Seek(lastLogPos, SeekOrigin.Begin);
                                using var reader = new StreamReader(fs);
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (Regex.IsMatch(line, "Progress|Error|Warning|Phase|Operation|Migrating|Installing|Setting up|Percent", RegexOptions.IgnoreCase))
                                    {
                                        int rBracket = line.IndexOf(']');
                                        string cleanLine = rBracket >= 0 ? line.Substring(rBracket + 1).Trim() : line.Trim();

                                        foreach (var phase in phases)
                                        {
                                            if (Regex.IsMatch(line, phase.Pattern, RegexOptions.IgnoreCase) && phase.Pct > lastPct)
                                            {
                                                lastPct = phase.Pct;
                                                lastLabel = phase.Label;
                                                task.Value = lastPct;
                                            }
                                        }
                                        
                                        task.Description = $"[green]{lastLabel} | {cleanLine}[/]";
                                    }
                                }
                                lastLogPos = fs.Position;
                            }
                        }
                        catch
                        {
                            // Ignore read errors that can happen temporarily
                        }
                    }

                    if (!isProcessActive() && task.Value >= 95)
                        break;
                    if (!isProcessActive() && !_fileSystem.FileExists(logPath))
                        break; // Setup exited and log is gone or never created?

                    await Task.Delay(2000, cts.Token);
                }
                
                task.Value = 100;
                task.Description = "[green]Upgrade process finished watching logs.[/]";
            });
    }

    public bool CheckIfUpgradeSucceeded(string logPath)
    {
        if (!_fileSystem.FileExists(logPath)) return false;

        try
        {
            using var fs = _fileSystem.OpenRead(logPath);
            if (fs.Length > 8192)
                fs.Seek(-8192, SeekOrigin.End); // last ~8kb
            
            using var reader = new StreamReader(fs);
            string tail = reader.ReadToEnd();

            return tail.Contains("Finalize succeeded") || tail.Contains("Upgrade completed successfully") || tail.Contains("Operation completed successfully");
        }
        catch
        {
            return false;
        }
    }
}
