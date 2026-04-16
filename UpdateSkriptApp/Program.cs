using System;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;
using UpdateSkriptApp.Modules;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp;

class Program
{
    static async Task Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Servier DE OOBE Preparation Tool")
                .Centered()
                .Color(Color.Cyan1));

        AnsiConsole.Write(new Rule("[yellow]Windows 11 Corporate Automation Tool[/]").Centered());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold cyan]Type 'reinstall' to RESET everything, or press Enter to CONTINUE[/]");
        string input = Console.ReadLine();
        if (input != null && input.Trim().Equals("reinstall", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]RESETTING ALL FLAGS: WU, Dell, and Win11...[/]");
            RegistryService.ResetAllFlags();
        }

        try
        {
            // PHASE 1: WINDOWS UPDATES
            AnsiConsole.Write(new Rule("[magenta]PHASE 1: WINDOWS UPDATES[/]"));
            if (RegistryService.IsPhaseCompleted("WU"))
            {
                AnsiConsole.MarkupLine("[green]Windows Updates were already completed previously. Skipping.[/]");
            }
            else
            {
                bool wuResult = await WinUpdateProvider.RunWindowsUpdatesAsync();
                if (wuResult)
                {
                    AnsiConsole.MarkupLine("[yellow]System needs to reboot after Windows Updates.[/]");
                    await PowerShellHost.ExecuteScriptAsync("Restart-Computer -Force");
                    return;
                }
                RegistryService.MarkPhaseCompleted("WU");
            }

            // PHASE 2: DELL DRIVERS
            AnsiConsole.Write(new Rule("[magenta]PHASE 2: DELL DRIVER PACK[/]"));
            if (RegistryService.IsPhaseCompleted("Dell"))
            {
                AnsiConsole.MarkupLine("[green]Dell Driver updates were already completed previously. Skipping.[/]");
            }
            else
            {
                string extractDir = await DellCatalogProvider.DownloadAndExtractDriverPackAsync();
                if (extractDir != null)
                {
                    await PnPInstaller.InstallDriversAsync(extractDir);
                    try { Directory.Delete(extractDir, true); } catch { }
                }
                RegistryService.MarkPhaseCompleted("Dell");
            }

            // PHASE 3: WINDOWS 11 25H2 UPGRADE
            AnsiConsole.Write(new Rule("[magenta]PHASE 3: WINDOWS 11 25H2 UPGRADE[/]"));
            if (RegistryService.IsPhaseCompleted("Win11"))
            {
                AnsiConsole.MarkupLine("[green]Windows 11 25H2 upgrade was already completed. Skipping.[/]");
            }
            else
            {
                var (build, _) = RegistryService.GetCurrentOsVersion();
                if (build >= 26100)
                {
                    AnsiConsole.MarkupLine("[green]System is already on Windows 11 24H2/25H2 or newer.[/]");
                    RegistryService.MarkPhaseCompleted("Win11");
                }
                else
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    bool upgraded = await UpgradeManager.RunUpgradeAsync(baseDir);
                    if (upgraded)
                    {
                        RegistryService.MarkPhaseCompleted("Win11");
                        AnsiConsole.MarkupLine("[yellow]Waiting 30 seconds for SetupComplete.cmd to fire (Sysprep will shut down automatically)...[/]");
                        await Task.Delay(30000);
                        await PowerShellHost.ExecuteScriptAsync("Restart-Computer -Force");
                        return;
                    }
                }
            }

            AnsiConsole.MarkupLine("[bold lime]All phases completed successfully. System is OOBE ready.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
}
