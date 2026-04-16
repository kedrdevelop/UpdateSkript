using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using UpdateSkriptApp.Modules;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Setup DI
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, RealFileSystem>();
        services.AddSingleton<IRegistryWrapper, RealRegistryWrapper>();
        services.AddSingleton<IAppEnvironment, RealAppEnvironment>();
        services.AddTransient<IPowerShellRunner, PowerShellHost>();

        services.AddTransient<IRegistryService, RegistryService>();
        services.AddTransient<ISetupCompleteBuilder, SetupCompleteBuilder>();
        services.AddTransient<IProgressDownloader, ProgressDownloader>();
        services.AddTransient<ILogWatcher, LogWatcher>();

        services.AddTransient<IWinUpdateProvider, WinUpdateProvider>();
        services.AddTransient<IDellCatalogProvider, DellCatalogProvider>();
        services.AddTransient<IPnPInstaller, PnPInstaller>();
        services.AddTransient<IUpgradeManager, UpgradeManager>();

        var serviceProvider = services.BuildServiceProvider();

        // 2. Resolve Services
        var registrySvc = serviceProvider.GetRequiredService<IRegistryService>();
        var winUpdater = serviceProvider.GetRequiredService<IWinUpdateProvider>();
        var dellCatalog = serviceProvider.GetRequiredService<IDellCatalogProvider>();
        var pnpInstaller = serviceProvider.GetRequiredService<IPnPInstaller>();
        var upgradeManager = serviceProvider.GetRequiredService<IUpgradeManager>();
        var powerShell = serviceProvider.GetRequiredService<IPowerShellRunner>();
        var env = serviceProvider.GetRequiredService<IAppEnvironment>();

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
            registrySvc.ResetAllFlags();
        }

        try
        {
            // PHASE 1: WINDOWS UPDATES
            AnsiConsole.Write(new Rule("[magenta]PHASE 1: WINDOWS UPDATES[/]"));
            if (registrySvc.IsPhaseCompleted("WU"))
            {
                AnsiConsole.MarkupLine("[green]Windows Updates were already completed previously. Skipping.[/]");
            }
            else
            {
                bool wuResult = await winUpdater.RunWindowsUpdatesAsync();
                if (wuResult)
                {
                    AnsiConsole.MarkupLine("[yellow]System needs to reboot after Windows Updates.[/]");
                    await powerShell.ExecuteScriptAsync("Restart-Computer -Force");
                    return;
                }
                registrySvc.MarkPhaseCompleted("WU");
            }

            // PHASE 2: DELL DRIVERS
            AnsiConsole.Write(new Rule("[magenta]PHASE 2: DELL DRIVER PACK[/]"));
            if (registrySvc.IsPhaseCompleted("Dell"))
            {
                AnsiConsole.MarkupLine("[green]Dell Driver updates were already completed previously. Skipping.[/]");
            }
            else
            {
                string extractDir = await dellCatalog.DownloadAndExtractDriverPackAsync();
                if (extractDir != null)
                {
                    await pnpInstaller.InstallDriversAsync(extractDir);
                    try { Directory.Delete(extractDir, true); } catch { }
                }
                registrySvc.MarkPhaseCompleted("Dell");
            }

            // PHASE 3: WINDOWS 11 25H2 UPGRADE
            AnsiConsole.Write(new Rule("[magenta]PHASE 3: WINDOWS 11 25H2 UPGRADE[/]"));
            if (registrySvc.IsPhaseCompleted("Win11"))
            {
                AnsiConsole.MarkupLine("[green]Windows 11 25H2 upgrade was already completed. Skipping.[/]");
            }
            else
            {
                var (build, _) = registrySvc.GetCurrentOsVersion();
                if (build >= 26100)
                {
                    AnsiConsole.MarkupLine("[green]System is already on Windows 11 24H2/25H2 or newer.[/]");
                    registrySvc.MarkPhaseCompleted("Win11");
                }
                else
                {
                    bool upgraded = await upgradeManager.RunUpgradeAsync(env.BaseDirectory);
                    if (upgraded)
                    {
                        registrySvc.MarkPhaseCompleted("Win11");
                        AnsiConsole.MarkupLine("[yellow]Waiting 30 seconds for SetupComplete.cmd to fire (Sysprep will shut down automatically)...[/]");
                        await Task.Delay(30000);
                        await powerShell.ExecuteScriptAsync("Restart-Computer -Force");
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
