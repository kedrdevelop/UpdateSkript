using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp.Modules;

public static class UpgradeManager
{
    public static async Task<bool> RunUpgradeAsync(string baseDir)
    {
        var isoFile = Directory.GetFiles(baseDir, "*.iso")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();

        if (isoFile == null)
        {
            AnsiConsole.MarkupLine("[yellow]WARNING: No Windows 11 ISO file found next to the executable.[/]");
            return false;
        }

        AnsiConsole.MarkupLine($"[cyan]Found ISO: {Path.GetFileName(isoFile)}[/]");
        
        var (mountCode, mountOut) = await PowerShellHost.ExecuteScriptAsync($@"(Mount-DiskImage -ImagePath ""{isoFile}"" -PassThru | Get-Volume).DriveLetter");
        
        string driveLetter = mountOut.Trim();
        if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length > 1)
        {
            AnsiConsole.MarkupLine("[red]ERROR: Failed to mount ISO.[/]");
            return false;
        }

        string setupPath = $@"{driveLetter}:\setup.exe";
        if (!File.Exists(setupPath))
        {
            AnsiConsole.MarkupLine("[red]ERROR: setup.exe not found on mounted ISO.[/]");
            await PowerShellHost.ExecuteScriptAsync($@"Dismount-DiskImage -ImagePath ""{isoFile}""");
            return false;
        }

        AnsiConsole.MarkupLine("[cyan]Configuring SetupComplete.cmd...[/]");
        SetupCompleteBuilder.InjectSetupCompleteCmd();

        string logPath = @"C:\$WINDOWS.~BT\Sources\Panther\setupact.log";
        if (File.Exists(logPath)) File.Delete(logPath); // clear old log

        AnsiConsole.MarkupLine("[yellow]Starting Windows 11 Upgrade... DO NOT TURN OFF THE COMPUTER[/]");
        
        var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = "/auto upgrade /noreboot /DynamicUpdate disable /eula accept /compat ignorewarning",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.Start();

        await LogWatcher.MonitorSetupLogAsync(logPath, () => !proc.HasExited);
        
        bool success = LogWatcher.CheckIfUpgradeSucceeded(logPath);
        
        await PowerShellHost.ExecuteScriptAsync($@"Dismount-DiskImage -ImagePath ""{isoFile}""");

        if (success || proc.ExitCode == 0 || proc.ExitCode == 3)
        {
            AnsiConsole.MarkupLine("[green]Upgrade Succeeded! PC will restart shortly...[/]");
            return true;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Upgrade finished with unexpected state. Raw Exit Code: {proc.ExitCode}[/]");
            return false;
        }
    }
}
