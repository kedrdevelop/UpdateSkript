using System;
using System.Threading.Tasks;
using Spectre.Console;
using UpdateSkriptApp.Services;

namespace UpdateSkriptApp.Modules;

public interface IWinUpdateProvider
{
    Task<bool> RunWindowsUpdatesAsync();
}

public class WinUpdateProvider : IWinUpdateProvider
{
    private readonly IPowerShellRunner _powerShell;

    public WinUpdateProvider(IPowerShellRunner powerShell)
    {
        _powerShell = powerShell;
    }

    public async Task<bool> RunWindowsUpdatesAsync()
    {
        string script = @"
Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -ErrorAction SilentlyContinue | Out-Null
Install-Module -Name PSWindowsUpdate -Force -AllowClobber -SkipPublisherCheck -ErrorAction SilentlyContinue
Import-Module PSWindowsUpdate

$updates = Get-WindowsUpdate
$filtered = $updates | Where-Object { $_.Title -notmatch 'Security Intelligence Update|Malicious Software Removal Tool|Antimalware Platform|Security platform' }

$kbUpdates = @($filtered | Where-Object { $_.KB })
$noKbUpdates = @($filtered | Where-Object { -not $_.KB })
$installedCount = 0

if ($kbUpdates) {
    $kbList = $kbUpdates | ForEach-Object { $_.KB }
    $res = Install-WindowsUpdate -KBArticleID $kbList -AcceptAll -Install -AutoReboot:$false
    $installedCount += @($res | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
}

if ($noKbUpdates) {
    foreach ($update in $noKbUpdates) {
        $res2 = Install-WindowsUpdate -Title $update.Title -AcceptAll -Install -AutoReboot:$false
        $installedCount += @($res2 | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
    }
}
Write-Output ""INSTALLCOUNT=$installedCount""
";

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Applying Windows Updates (This can take a long time)...", async ctx =>
            {
                var (exitCode, output) = await _powerShell.ExecuteScriptAsync(script);
                
                if (output.Contains("INSTALLCOUNT=0"))
                {
                    AnsiConsole.MarkupLine("[green]No new Windows Updates to install.[/]");
                    return false; // Did not install any requiring reboot
                }
                
                if (output.Contains("INSTALLCOUNT="))
                {
                    AnsiConsole.MarkupLine("[green]Windows Updates installed successfully![/]");
                    return true; // Did install updates
                }

                AnsiConsole.MarkupLine("[yellow]Could not determine install count. See logs or raw output for details.[/]");
                return true; 
            });
    }
}
