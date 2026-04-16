using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;

namespace UpdateSkriptApp.Modules;

public static class PnPInstaller
{
    public static async Task InstallDriversAsync(string driverExtractDir)
    {
        if (!Directory.Exists(driverExtractDir))
        {
            AnsiConsole.MarkupLine("[red]Driver extraction directory not found.[/]");
            return;
        }

        var infFiles = Directory.GetFiles(driverExtractDir, "*.inf", SearchOption.AllDirectories);
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

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/add-driver \"{inf}\" /install",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    
                    string output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

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
