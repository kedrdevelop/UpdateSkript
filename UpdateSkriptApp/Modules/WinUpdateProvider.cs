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
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
Write-Output ""Enabling TLS 1.2 support...""

Write-Output ""Preparing NuGet provider...""
Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Confirm:$false -ErrorAction SilentlyContinue | Out-Null

Write-Output ""Loading PSWindowsUpdate module...""
Install-Module -Name PSWindowsUpdate -Force -AllowClobber -Confirm:$false -SkipPublisherCheck -ErrorAction SilentlyContinue
Import-Module PSWindowsUpdate

Write-Output ""Scanning for Windows updates (this may take a minute)...""
$updates = Get-WindowsUpdate

Write-Output ""Filtering results...""
$filtered = $updates | Where-Object { $_.Title -notmatch 'Security Intelligence Update|Malicious Software Removal Tool|Antimalware Platform|Security platform' }

$kbUpdates = @($filtered | Where-Object { $_.KB })
$noKbUpdates = @($filtered | Where-Object { -not $_.KB })
$installedCount = 0

Write-Output ""Found $(@($updates).Count) total updates. $(@($filtered).Count) eligible for installation after filtering.""

if ($kbUpdates) {
    $kbList = $kbUpdates | ForEach-Object { $_.KB }
    Write-Output ""Installing $($kbList.Count) KB updates in batch...""
    $res = Install-WindowsUpdate -KBArticleID $kbList -AcceptAll -Confirm:$false -Install -AutoReboot:$false
    $installedCount += @($res | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
}

if ($noKbUpdates) {
    Write-Output ""Installing $($noKbUpdates.Count) non-KB updates...""
    foreach ($update in $noKbUpdates) {
        Write-Output ""Installing $($update.Title)...""
        $res2 = Install-WindowsUpdate -Title $update.Title -AcceptAll -Confirm:$false -Install -AutoReboot:$false
        $installedCount += @($res2 | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
    }
}
Write-Output ""INSTALLCOUNT=$installedCount""
";

        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Applying Windows Updates (This can take a long time)...", async ctx =>
            {
                var (exitCode, output) = await _powerShell.ExecuteScriptAsync(script, hidden: true, onOutputLine: line => 
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string safeLine = Markup.Escape(line);
                        safeLine = safeLine.Length > 80 ? safeLine.Substring(0, 80) + "..." : safeLine;
                        ctx.Status($"Applying Windows Updates: {safeLine}");
                    }
                });
                
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
