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

Write-Host "`n--- DELL DRIVER PACK PHASE ---" -ForegroundColor Magenta

# --- Step 1: Identify the system ---
$SystemInfo = Get-CimInstance Win32_ComputerSystem
$SystemModel = $SystemInfo.Model.Trim()
$SystemManufacturer = $SystemInfo.Manufacturer.Trim()

if ($SystemManufacturer -notmatch 'Dell') {
    Write-Host "This system is not manufactured by Dell ($SystemManufacturer). Skipping Dell driver phase." -ForegroundColor Yellow
} else {
    Write-Host "Detected Dell system model: $SystemModel" -ForegroundColor Cyan

    # --- Step 2: Download and extract Dell Driver Pack Catalog ---
    $CatalogUrl = "https://downloads.dell.com/catalog/DriverPackCatalog.cab"
    $TempDir = Join-Path $env:TEMP "DellDriverPack"
    $CabPath = Join-Path $TempDir "DriverPackCatalog.cab"
    $XmlPath = Join-Path $TempDir "DriverPackCatalog.xml"

    if (-not (Test-Path $TempDir)) { New-Item -Path $TempDir -ItemType Directory -Force | Out-Null }

    Write-Host "Downloading Dell Driver Pack Catalog..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $CatalogUrl -OutFile $CabPath -UseBasicParsing -ErrorAction Stop
    } catch {
        Write-Host "ERROR: Failed to download Dell catalog. Check your internet connection." -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
        exit
    }

    Write-Host "Extracting catalog XML..." -ForegroundColor Cyan
    expand.exe $CabPath -F:DriverPackCatalog.xml $TempDir | Out-Null

    if (-not (Test-Path $XmlPath)) {
        Write-Host "ERROR: Failed to extract DriverPackCatalog.xml" -ForegroundColor Red
        Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
        exit
    }

    # --- Step 3: Find matching driver pack ---
    Write-Host "Parsing catalog for model: $SystemModel ..." -ForegroundColor Cyan
    [xml]$Catalog = Get-Content $XmlPath

    # Get the current OS version info for matching
    $OSVersion = (Get-CimInstance Win32_OperatingSystem).Version
    $OSBuild = [System.Environment]::OSVersion.Version.Build

    # Determine OS string to match (e.g. "Windows 11" or "Windows 10")
    if ($OSBuild -ge 22000) { $OSTarget = "Windows 11" } else { $OSTarget = "Windows 10" }
    Write-Host "Target OS: $OSTarget (Build $OSBuild)" -ForegroundColor DarkCyan

    $MatchingPacks = $Catalog.DriverPackManifest.DriverPackage | Where-Object {
        $modelMatch = $false
        foreach ($brand in $_.SupportedSystems.Brand) {
            foreach ($model in $brand.Model) {
                if ($model.name -eq $SystemModel) {
                    $modelMatch = $true
                    break
                }
            }
            if ($modelMatch) { break }
        }
        $osMatch = $false
        foreach ($os in $_.SupportedOperatingSystems.OperatingSystem) {
            if ($os.Display -match $OSTarget -and $os.osArch -eq 'x64') {
                $osMatch = $true
                break
            }
        }
        $modelMatch -and $osMatch -and $_.type -ne 'WinPE'
    }

    if (-not $MatchingPacks) {
        Write-Host "WARNING: No driver pack found for model '$SystemModel' and $OSTarget x64." -ForegroundColor Yellow
        Write-Host "Trying a broader search by model name..." -ForegroundColor Yellow

        $MatchingPacks = $Catalog.DriverPackManifest.DriverPackage | Where-Object {
            $modelMatch = $false
            foreach ($brand in $_.SupportedSystems.Brand) {
                foreach ($model in $brand.Model) {
                    if ($model.name -match [regex]::Escape($SystemModel)) {
                        $modelMatch = $true
                        break
                    }
                }
                if ($modelMatch) { break }
            }
            $modelMatch -and $_.type -ne 'WinPE'
        }
    }

    if (-not $MatchingPacks) {
        Write-Host "ERROR: No driver pack found for this Dell model ($SystemModel)." -ForegroundColor Red
        Write-Host "This model may not have an enterprise driver pack available in the Dell catalog." -ForegroundColor Yellow
        Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
        exit
    }

    # Take the first (most relevant) match
    $DriverPack = $MatchingPacks | Select-Object -First 1
    $DriverPackUrl = "https://downloads.dell.com/$($DriverPack.path)"
    $DriverPackFilename = Split-Path $DriverPack.path -Leaf
    $DriverPackDownload = Join-Path $TempDir $DriverPackFilename
    $DriverExtractDir = Join-Path $TempDir "Drivers"

    Write-Host "Found driver pack: $($DriverPack.Name)" -ForegroundColor Green
    Write-Host "Release date: $($DriverPack.releaseDate)" -ForegroundColor DarkCyan
    Write-Host "Download URL: $DriverPackUrl" -ForegroundColor DarkCyan

    # --- Step 4: Download driver pack ---
    Write-Host "`nDownloading driver pack (~500MB-1.5GB). This may take a while..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $DriverPackUrl -OutFile $DriverPackDownload -UseBasicParsing -ErrorAction Stop
    } catch {
        Write-Host "ERROR: Failed to download driver pack." -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        Write-Host "Exiting script to preserve progress flags." -ForegroundColor Red
        exit
    }

    # --- Step 5: Extract drivers ---
    Write-Host "Extracting driver pack..." -ForegroundColor Cyan
    if (-not (Test-Path $DriverExtractDir)) { New-Item -Path $DriverExtractDir -ItemType Directory -Force | Out-Null }

    if ($DriverPackFilename -match '\.cab$') {
        expand.exe $DriverPackDownload -F:* $DriverExtractDir | Out-Null
    } elseif ($DriverPackFilename -match '\.exe$') {
        Start-Process -FilePath $DriverPackDownload -ArgumentList "/s /e=$DriverExtractDir" -Wait -NoNewWindow
    } else {
        Write-Host "ERROR: Unknown driver pack format: $DriverPackFilename" -ForegroundColor Red
        exit
    }

    # --- Step 6: Install drivers via pnputil ---
    Write-Host "Installing drivers using pnputil. This may take several minutes..." -ForegroundColor Cyan
    $infFiles = Get-ChildItem -Path $DriverExtractDir -Recurse -Filter "*.inf" -File
    $infCount = $infFiles.Count
    Write-Host "Found $infCount driver INF files to process." -ForegroundColor DarkCyan

    $pnpResult = pnputil.exe /add-driver "$DriverExtractDir\*.inf" /subdirs /install 2>&1
    $pnpResult | ForEach-Object { Write-Host $_ -ForegroundColor Gray }

    # --- Step 7: Cleanup temp files ---
    Write-Host "`nCleaning up temporary files..." -ForegroundColor DarkCyan
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "`nDell driver pack installation completed successfully!" -ForegroundColor Green
    Write-Host "A reboot is recommended to finalize driver installation." -ForegroundColor Yellow
}

Write-Host "`n--- UPDATE PROCESS FULLY COMPLETED ---" -ForegroundColor Green
Write-Host "No further Windows or Dell updates are available." -ForegroundColor Cyan
if (Test-Path $WuFlagPath) { Remove-Item -Path $WuFlagPath -Force | Out-Null }