<# :
@echo off
:: Batch entry point: relaunch this same file under PowerShell
echo Starting the update process... Please do not close this window.

:: Pass current file path (%~f0) into PowerShell as a parameter
powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-Command -ScriptBlock ([ScriptBlock]::Create((Get-Content '%~f0' -Raw))) -ArgumentList '%~f0'"
echo.
echo Script execution finished.
pause
exit /b
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalScriptPath
)

# --- Configuration & Paths ---
$ScriptDir = Split-Path -Path $OriginalScriptPath
$WuFlagPath = "$env:PUBLIC\UpdateSkript_WU.flag"

function udf_EnsureTls12 {
    $currentProtocol = [Net.ServicePointManager]::SecurityProtocol
    if ($currentProtocol -notmatch 'Tls12') {
        Write-Host "Enabling TLS 1.2 support..." -ForegroundColor Yellow
        [Net.ServicePointManager]::SecurityProtocol = $currentProtocol -bor [Net.SecurityProtocolType]::Tls12
    }
}

Write-Host "`n--- WINDOWS UPDATES PHASE ---" -ForegroundColor Magenta
udf_EnsureTls12

if (Test-Path $WuFlagPath) {
    Write-Host "Windows Updates were already completed previously. Skipping this phase." -ForegroundColor Green
} else {
    Write-Host "Preparing NuGet provider..." -ForegroundColor Cyan
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -ErrorAction SilentlyContinue | Out-Null

    Write-Host "Loading PSWindowsUpdate module..." -ForegroundColor Cyan
    Install-Module -Name PSWindowsUpdate -Force -AllowClobber -SkipPublisherCheck -ErrorAction SilentlyContinue
    Import-Module PSWindowsUpdate

    Write-Host "Scanning and installing Windows updates. This may take a while..." -ForegroundColor Cyan

    # Get available updates, filter them, and install safely to avoid pipeline binding issues
    $pendingUpdates = Get-WindowsUpdate
    $filteredUpdates = $pendingUpdates | Where-Object { $_.Title -notmatch 'Security Intelligence Update|Malicious Software Removal Tool|Antimalware Platform|Security platform' }

    $Result = @()
    if ($filteredUpdates) {
        foreach ($update in $filteredUpdates) {
            if ($update.KB) {
                Install-WindowsUpdate -KBArticleID $update.KB -AcceptAll -Install -Verbose -OutVariable tempRes
                if ($tempRes) { $Result += $tempRes }
            } else {
                Install-WindowsUpdate -Title $update.Title -AcceptAll -Install -Verbose -OutVariable tempRes
                if ($tempRes) { $Result += $tempRes }
            }
        }
    }

    $updatesInstalled = $false

    if ($null -ne $Result) {
        $resultArray = @($Result)
        $installedCount = ($resultArray | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
        if ($installedCount -gt 0) { $updatesInstalled = $true }
    }

    # We unconditionally restart the computer if Windows Updates were installed, to allow multiple passes.
    if ($updatesInstalled) {
        Write-Host "`nWindows Updates were installed successfully!" -ForegroundColor Green
        Write-Host "To ensure all dependent updates are caught, the system will now reboot." -ForegroundColor Yellow
        Write-Host "Please MANUALLY rerun this script after rebooting to continue the process." -ForegroundColor Red
        
        Start-Sleep -Seconds 10
        Restart-Computer -Force
        exit
    }

    # If no updates were installed, we mark the phase as completed to skip it on next run
    Write-Host "`nNo new Windows Updates were found. Marking phase as completed." -ForegroundColor Green
    New-Item -Path $WuFlagPath -ItemType File -Force | Out-Null
}

# If no updates were installed, we proceed to Dell updates
Write-Host "`nNo new Windows Updates were found. Proceeding to Dell updates..." -ForegroundColor Green

Write-Host "`n--- DELL UPDATES PHASE ---" -ForegroundColor Magenta

$DcuPath = $null
$PossiblePaths = @(
    "C:\Program Files\Dell\CommandUpdate\dcu-cli.exe",
    "C:\Program Files (x86)\Dell\CommandUpdate\dcu-cli.exe",
    "C:\Program Files\Dell\CommandUpdate\cli\dcu-cli.exe"
)

foreach ($path in $PossiblePaths) {
    if (Test-Path $path) {
        $DcuPath = $path
        break
    }
}

if (-not $DcuPath) {
    Write-Host "Dell Command Update is not installed. Looking for an installer in the script directory..." -ForegroundColor Yellow
    $Installer = Get-ChildItem -Path $ScriptDir -Filter "Dell-Command-Update-Windows-Universal-Application*.exe" | Select-Object -First 1
    if (-not $Installer) {
        $Installer = Get-ChildItem -Path $ScriptDir -Filter "DCU_Setup.exe" | Select-Object -First 1
    }
    
    if ($Installer) {
        Write-Host "Checking for required .NET 8 Desktop Runtime..." -ForegroundColor Yellow
        $DotNetUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
        $DotNetInstaller = Join-Path $ScriptDir "windowsdesktop-runtime-win-x64.exe"
        if (-not (Test-Path $DotNetInstaller)) {
            Write-Host "Downloading .NET 8 Desktop Runtime from Microsoft..." -ForegroundColor Cyan
            Invoke-WebRequest -Uri $DotNetUrl -OutFile $DotNetInstaller -UseBasicParsing
        }
        if (Test-Path $DotNetInstaller) {
            Write-Host "Installing .NET 8 Desktop Runtime silently..." -ForegroundColor Cyan
            Start-Process -FilePath $DotNetInstaller -ArgumentList "/quiet /norestart" -Wait -NoNewWindow
        }

        Write-Host "Found installer: $($Installer.Name). Launching Dell Command Update installer..." -ForegroundColor Cyan
        Start-Process -FilePath $Installer.FullName -Wait -NoNewWindow
        
        Write-Host "Waiting up to 2 minutes for installation to finish..." -ForegroundColor DarkCyan
        $retryCount = 0
        while ($retryCount -lt 12) {
            Start-Sleep -Seconds 10
            foreach ($path in $PossiblePaths) {
                if (Test-Path $path) {
                    $DcuPath = $path
                    break
                }
            }
            if ($DcuPath) { break }
            $retryCount++
        }
    }
}
if (-not $DcuPath) {
    Write-Host "ERROR: dcu-cli.exe could not be found!" -ForegroundColor Red
    Write-Host "Please download the 'Dell Command | Update Windows Universal Application' from the Dell Support website." -ForegroundColor Yellow
    Write-Host "Place the downloaded .exe installer next to this script and rerun it. The script will install it for you." -ForegroundColor Yellow
    Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
    exit
} else {
    Write-Host "Found Dell Command CLI at: $DcuPath" -ForegroundColor DarkCyan
    Write-Host "Applying Dell firmware and driver updates..." -ForegroundColor Cyan
    Write-Host "Executing dcu-cli.exe... Check the standard output below for detailed module statuses:`n" -ForegroundColor DarkCyan
    
    $dcuProcess = Start-Process -FilePath $DcuPath -ArgumentList "/applyUpdates -reboot=disable" -Wait -NoNewWindow -PassThru
    $exitCode = $dcuProcess.ExitCode
    
    Write-Host "Dell Update finished with exit code: $exitCode" -ForegroundColor Cyan
    
    # 0 = Success, 4 = Password Required (usually for BIOS, implies completion otherwise)
    if ($exitCode -eq 0 -or $exitCode -eq 4) {
        Write-Host "Dell Updates complete (no reboot required)." -ForegroundColor Green
    }
    elseif ($exitCode -eq 2 -or $exitCode -eq 3) {
        Write-Host "Dell Updates installed. A reboot is required to proceed." -ForegroundColor Green
        Write-Host "Please MANUALLY rerun this script after rebooting to ensure no further updates remain." -ForegroundColor Red
        
        Start-Sleep -Seconds 10
        Restart-Computer -Force
        exit
    }
    else {
        Write-Host "Dell Update failed or reported an issue. (Code: $exitCode)" -ForegroundColor Yellow
        Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
        exit
    }
}

Write-Host "`n--- UPDATE PROCESS FULLY COMPLETED ---" -ForegroundColor Green
Write-Host "No further Windows or Dell updates are available." -ForegroundColor Cyan
if (Test-Path $WuFlagPath) { Remove-Item -Path $WuFlagPath -Force | Out-Null }