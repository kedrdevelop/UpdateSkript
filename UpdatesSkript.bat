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
$DcuPath = Join-Path -Path $ScriptDir -ChildPath "dcu-cli.exe"

function udf_EnsureTls12 {
    $currentProtocol = [Net.ServicePointManager]::SecurityProtocol
    if ($currentProtocol -notmatch 'Tls12') {
        Write-Host "Enabling TLS 1.2 support..." -ForegroundColor Yellow
        [Net.ServicePointManager]::SecurityProtocol = $currentProtocol -bor [Net.SecurityProtocolType]::Tls12
    }
}

Write-Host "`n--- WINDOWS UPDATES PHASE ---" -ForegroundColor Magenta
udf_EnsureTls12

Write-Host "Preparing NuGet provider..." -ForegroundColor Cyan
Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -ErrorAction SilentlyContinue | Out-Null

Write-Host "Loading PSWindowsUpdate module..." -ForegroundColor Cyan
Install-Module -Name PSWindowsUpdate -Force -AllowClobber -SkipPublisherCheck -ErrorAction SilentlyContinue
Import-Module PSWindowsUpdate

Write-Host "Scanning and installing Windows updates. This may take a while..." -ForegroundColor Cyan

# We use -OutVariable to capture the result while allowing it to stream to the console in real-time
Install-WindowsUpdate -NotTitle "Antivirus" -AcceptAll -Install -Verbose -OutVariable Result

$updatesInstalled = $false

if ($null -ne $Result) {
    $resultArray = @($Result)
    $installedCount = ($resultArray | Where-Object { $_.Result -match 'Installed' -or $_.Status -match 'Installed' -or $_.Installed -eq $true }).Count
    if ($installedCount -gt 0) { $updatesInstalled = $true }
}

# We unconditionally restart the computer if Windows Updates were installed, to allow multiple passes.
# If an update was installed, there could be subsequent dependent updates available only after reboot.
if ($updatesInstalled) {
    Write-Host "`nWindows Updates were installed successfully!" -ForegroundColor Green
    Write-Host "To ensure all dependent updates are caught, the system will now reboot." -ForegroundColor Yellow
    Write-Host "Please MANUALLY rerun this script after rebooting to continue the process." -ForegroundColor Red
    
    Start-Sleep -Seconds 10
    Restart-Computer -Force
    exit
}

# If no updates were installed, we proceed to Dell updates
Write-Host "`nNo new Windows Updates were found. Proceeding to Dell updates..." -ForegroundColor Green

Write-Host "`n--- DELL UPDATES PHASE ---" -ForegroundColor Magenta

if (-not (Test-Path $DcuPath)) {
    Write-Host "dcu-cli.exe not found alongside the script! Skipping Dell updates phase." -ForegroundColor Yellow
} else {
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
    }
}

Write-Host "`n--- UPDATE PROCESS FULLY COMPLETED ---" -ForegroundColor Green
Write-Host "No further Windows or Dell updates are available." -ForegroundColor Cyan