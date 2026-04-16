using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp.Modules;

public interface IPnPInstaller
{
    Task InstallDriversAsync(string driverExtractDir);
}

public class PnPInstaller : IPnPInstaller
{
    private readonly IFileSystem _fileSystem;
    private readonly IPowerShellRunner _powerShell;

    public PnPInstaller(IFileSystem fileSystem, IPowerShellRunner powerShell)
    {
        _fileSystem = fileSystem;
        _powerShell = powerShell;
    }

    public async Task InstallDriversAsync(string driverExtractDir)
    {
        if (!_fileSystem.DirectoryExists(driverExtractDir))
        {
            AnsiConsole.MarkupLine("[red]Driver extraction directory not found.[/]");
            return;
        }

        var infFiles = _fileSystem.GetFiles(driverExtractDir, "*.inf", SearchOption.AllDirectories);
        int total = infFiles.Length;
        int installed = 0;
        int failed = 0;

        AnsiConsole.MarkupLine($"[cyan]Found {total} driver INF files. Installing...[/]");

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Installing Dell drivers[/]", maxValue: total);

                for (int i = 0; i < total; i++)
                {
                    string inf = infFiles[i];
                    string driverName = Path.Combine(new DirectoryInfo(Path.GetDirectoryName(inf)).Name, Path.GetFileName(inf));
                    
                    task.Description = $"[green]Installing:[/] {driverName}";
                    
                    var (exit, output) = await _powerShell.ExecuteScriptAsync($"pnputil.exe /add-driver \"{inf}\" /install", hidden: true);

                    if (output.Contains("Published") || output.Contains("successfully"))
                    {
                        installed++;
                    }
                    else if (output.Contains("Failed") || output.Contains("Error"))
                    {
                        failed++;
                    }

                    task.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]Driver installation complete: {installed} installed, {failed} failed out of {total} total.[/]");
    }
}
