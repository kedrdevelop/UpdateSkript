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
$LogFile = Join-Path $ScriptDir "UpdatesSkript.log"

# --- Logging Function ---
# Writes a timestamped message to both the console and the log file
function Log {
    param(
        [string]$Message,
        [string]$Color = "White",
        [switch]$NoConsole
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logLine = "[$timestamp] $Message"
    Add-Content -Path $LogFile -Value $logLine -Encoding UTF8
    if (-not $NoConsole) {
        Write-Host $logLine -ForegroundColor $Color
    }
}

# --- Reliable Download Function ---
# Uses WebClient with retry logic for stable large-file downloads
function Download-File {
    param(
        [string]$Url,
        [string]$Destination,
        [int]$MaxRetries = 3,
        [int]$MinExpectedBytes = 0
    )
    $attempt = 0
    while ($attempt -lt $MaxRetries) {
        $attempt++
        Log "Download attempt $attempt/$MaxRetries : $Url" -Color DarkCyan
        try {
            $webClient = New-Object System.Net.WebClient
            $webClient.DownloadFile($Url, $Destination)
            $webClient.Dispose()

            if (Test-Path $Destination) {
                $fileSize = (Get-Item $Destination).Length
                $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
                Log "Downloaded $fileSizeMB MB ($fileSize bytes) -> $Destination" -Color Green

                if ($MinExpectedBytes -gt 0 -and $fileSize -lt $MinExpectedBytes) {
                    Log "WARNING: File is smaller than expected minimum ($MinExpectedBytes bytes). Retrying..." -Color Yellow
                    Remove-Item -Path $Destination -Force -ErrorAction SilentlyContinue
                    Start-Sleep -Seconds 5
                    continue
                }
                return $true
            } else {
                Log "WARNING: File was not saved to disk. Retrying..." -Color Yellow
            }
        } catch {
            Log "ERROR on attempt $attempt : $($_.Exception.Message)" -Color Red
        }
        Start-Sleep -Seconds 5
    }
    Log "FAILED: Could not download after $MaxRetries attempts: $Url" -Color Red
    return $false
}

# --- TLS Configuration ---
function udf_EnsureTls12 {
    $currentProtocol = [Net.ServicePointManager]::SecurityProtocol
    if ($currentProtocol -notmatch 'Tls12') {
        Log "Enabling TLS 1.2 support... (was: $currentProtocol)" -Color Yellow
        [Net.ServicePointManager]::SecurityProtocol = $currentProtocol -bor [Net.SecurityProtocolType]::Tls12
    } else {
        Log "TLS 1.2 is already enabled. ($currentProtocol)" -Color DarkGray
    }
}

# =============================================================================
# START
# =============================================================================
Log "========================================" -Color Magenta
Log "UPDATE SCRIPT STARTED" -Color Magenta
Log "Script directory: $ScriptDir" -Color DarkGray
Log "========================================" -Color Magenta

udf_EnsureTls12

# =============================================================================
# PHASE 1: WINDOWS UPDATES
# =============================================================================
Log "" -Color White
Log "--- WINDOWS UPDATES PHASE ---" -Color Magenta

if (Test-Path $WuFlagPath) {
    Log "Windows Updates were already completed previously. Skipping this phase." -Color Green
    Log "Flag file: $WuFlagPath" -Color DarkGray
} else {
    Log "Preparing NuGet provider..." -Color Cyan
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -ErrorAction SilentlyContinue | Out-Null

    Log "Loading PSWindowsUpdate module..." -Color Cyan
    Install-Module -Name PSWindowsUpdate -Force -AllowClobber -SkipPublisherCheck -ErrorAction SilentlyContinue
    Import-Module PSWindowsUpdate

    Log "Scanning for Windows updates..." -Color Cyan

    # Get available updates, filter out known looping updates
    $pendingUpdates = Get-WindowsUpdate
    $pendingCount = @($pendingUpdates).Count
    Log "Found $pendingCount total updates in pre-search." -Color DarkCyan

    $filteredUpdates = $pendingUpdates | Where-Object { $_.Title -notmatch 'Security Intelligence Update|Malicious Software Removal Tool|Antimalware Platform|Security platform' }
    $filteredCount = @($filteredUpdates).Count
    Log "After filtering looping updates: $filteredCount updates eligible for installation." -Color DarkCyan

    if ($filteredUpdates) {
        foreach ($update in $filteredUpdates) {
            Log "  -> $($update.Title) [KB$($update.KB)]" -Color DarkGray
        }
    }

    $Result = @()
    if ($filteredUpdates) {
        foreach ($update in $filteredUpdates) {
            Log "Installing: $($update.Title)..." -Color Cyan
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
        Log "Installation result: $installedCount updates installed." -Color DarkCyan
    }

    if ($updatesInstalled) {
        Log "Windows Updates were installed successfully!" -Color Green
        Log "System will reboot in 10 seconds to allow dependent updates." -Color Yellow
        Log "Please MANUALLY rerun this script after rebooting." -Color Red

        Start-Sleep -Seconds 10
        Restart-Computer -Force
        exit
    }

    Log "No new Windows Updates to install. Marking phase as completed." -Color Green
    New-Item -Path $WuFlagPath -ItemType File -Force | Out-Null
    Log "Flag file created: $WuFlagPath" -Color DarkGray
}

# =============================================================================
# PHASE 2: DELL DRIVER PACK
# =============================================================================
Log "" -Color White
Log "--- DELL DRIVER PACK PHASE ---" -Color Magenta

# --- Step 1: Identify the system ---
$SystemInfo = Get-CimInstance Win32_ComputerSystem
$SystemModel = $SystemInfo.Model.Trim()
$SystemManufacturer = $SystemInfo.Manufacturer.Trim()
Log "Manufacturer: $SystemManufacturer" -Color DarkGray
Log "Model: $SystemModel" -Color DarkGray

if ($SystemManufacturer -notmatch 'Dell') {
    Log "This system is not manufactured by Dell. Skipping Dell driver phase." -Color Yellow
} else {
    Log "Confirmed Dell system: $SystemModel" -Color Cyan

    # --- Step 2: Download and extract Dell Driver Pack Catalog ---
    $CatalogUrl = "https://downloads.dell.com/catalog/DriverPackCatalog.cab"
    $TempDir = Join-Path $env:TEMP "DellDriverPack"
    $CabPath = Join-Path $TempDir "DriverPackCatalog.cab"
    $XmlPath = Join-Path $TempDir "DriverPackCatalog.xml"

    # Clean up temp dir first to avoid using stale cached files
    if (Test-Path $TempDir) {
        Log "Removing stale temp directory from previous run..." -Color DarkGray
        Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -Path $TempDir -ItemType Directory -Force | Out-Null
    Log "Temp directory: $TempDir" -Color DarkGray

    Log "Downloading Dell Driver Pack Catalog (~300 KB compressed)..." -Color Cyan
    $downloadOk = Download-File -Url $CatalogUrl -Destination $CabPath -MaxRetries 3

    if (-not $downloadOk) {
        Log "CRITICAL: Could not download Dell catalog after multiple retries." -Color Red
        Log "Current TLS protocols: $([Net.ServicePointManager]::SecurityProtocol)" -Color DarkGray
        Log "Exiting script to preserve progress flags." -Color Red
        exit
    }

    Log "Extracting catalog XML using expand.exe..." -Color Cyan
    $expandOutput = expand.exe $CabPath -F:DriverPackCatalog.xml $TempDir 2>&1
    $expandOutput | ForEach-Object { Log "  expand: $_" -Color DarkGray }

    if (-not (Test-Path $XmlPath)) {
        Log "ERROR: Failed to extract DriverPackCatalog.xml" -Color Red
        Log "CAB file path: $CabPath" -Color DarkGray
        Log "Files in temp directory:" -Color DarkGray
        Get-ChildItem -Path $TempDir | ForEach-Object { Log "  $($_.Name) ($($_.Length) bytes)" -Color DarkGray }
        Log "Exiting script to preserve progress flags." -Color Red
        exit
    }

    $XmlSize = (Get-Item $XmlPath).Length
    $XmlSizeMB = [math]::Round($XmlSize / 1MB, 2)
    Log "Catalog XML extracted successfully. Size: $XmlSizeMB MB" -Color Green

    # --- Step 3: Find matching driver pack ---
    Log "Parsing catalog for model: $SystemModel" -Color Cyan
    [xml]$Catalog = Get-Content $XmlPath

    # Map OS build to Dell osCode values
    $OSBuild = [System.Environment]::OSVersion.Version.Build
    if ($OSBuild -ge 22000) {
        $OSTarget = "Windows 11"
        $OsCodeTargets = @('Windows11', 'Windows10') # Win11 packs often use Win10 code
    } else {
        $OSTarget = "Windows 10"
        $OsCodeTargets = @('Windows10')
    }
    Log "Target OS: $OSTarget (Build $OSBuild), checking osCodes: $($OsCodeTargets -join ', ')" -Color DarkCyan

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
            # Use osCode attribute (e.g. 'Windows10','Windows11') instead of CDATA Display text
            if ($OsCodeTargets -contains $os.osCode -and $os.osArch -eq 'x64') {
                $osMatch = $true
                break
            }
        }
        $modelMatch -and $osMatch -and $_.type -ne 'WinPE'
    }

    if (-not $MatchingPacks) {
        Log "No exact match found. Trying broader search..." -Color Yellow

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
        Log "ERROR: No driver pack found for Dell model '$SystemModel'." -Color Red
        Log "This model may be too new or not included in the Dell enterprise DriverPackCatalog." -Color Yellow
        Log "Please download drivers manually from the Dell Support website:" -Color Yellow
        # Build a search-friendly model name for the URL
        $UrlModel = [System.Uri]::EscapeDataString($SystemModel)
        Log "  https://www.dell.com/support/home/en-us/product-support/product/$UrlModel/drivers" -Color Cyan
        Log "Exiting script to preserve progress flags." -Color Red
        exit
    }

    # Take the first (most relevant) match
    $DriverPack = $MatchingPacks | Select-Object -First 1
    $DriverPackUrl = "https://downloads.dell.com/$($DriverPack.path)"
    $DriverPackFilename = Split-Path $DriverPack.path -Leaf
    $DriverPackDownload = Join-Path $TempDir $DriverPackFilename
    $DriverExtractDir = Join-Path $TempDir "Drivers"

    Log "Found driver pack: $($DriverPack.Name)" -Color Green
    Log "Release date: $($DriverPack.releaseDate)" -Color DarkCyan
    Log "File: $DriverPackFilename" -Color DarkCyan
    Log "URL: $DriverPackUrl" -Color DarkGray

    # --- Step 4: Download driver pack ---
    Log "Downloading driver pack. This may take several minutes..." -Color Cyan
    # MinExpectedBytes = 100 MB - driver packs are always large
    $downloadOk = Download-File -Url $DriverPackUrl -Destination $DriverPackDownload -MaxRetries 3 -MinExpectedBytes 100000000

    if (-not $downloadOk) {
        Log "CRITICAL: Could not download driver pack after multiple retries." -Color Red
        Log "Exiting script to preserve progress flags." -Color Red
        exit
    }

    # --- Step 5: Extract drivers ---
    Log "Extracting driver pack..." -Color Cyan
    if (-not (Test-Path $DriverExtractDir)) { New-Item -Path $DriverExtractDir -ItemType Directory -Force | Out-Null }

    if ($DriverPackFilename -match '\.cab$') {
        Log "Format: CAB archive. Using expand.exe..." -Color DarkGray
        $expandOut = expand.exe $DriverPackDownload -F:* $DriverExtractDir 2>&1
        $expandOut | ForEach-Object { Log "  expand: $_" -Color DarkGray -NoConsole }
    } elseif ($DriverPackFilename -match '\.exe$') {
        Log "Format: EXE self-extractor. Extracting silently..." -Color DarkGray
        Start-Process -FilePath $DriverPackDownload -ArgumentList "/s /e=$DriverExtractDir" -Wait -NoNewWindow
    } else {
        Log "ERROR: Unknown driver pack format: $DriverPackFilename" -Color Red
        exit
    }

    # --- Step 6: Install drivers via pnputil ---
    $infFiles = Get-ChildItem -Path $DriverExtractDir -Recurse -Filter "*.inf" -File
    $infCount = $infFiles.Count
    Log "Found $infCount driver INF files. Installing via pnputil..." -Color Cyan

    $pnpResult = pnputil.exe /add-driver "$DriverExtractDir\*.inf" /subdirs /install 2>&1
    $pnpResult | ForEach-Object { Log "  pnputil: $_" -Color Gray -NoConsole }
    # Show summary lines on console
    $pnpResult | Where-Object { $_ -match 'Published|Failed|Total' } | ForEach-Object { Log "  $_" -Color DarkCyan }

    # --- Step 7: Cleanup temp files ---
    Log "Cleaning up temporary files..." -Color DarkCyan
    Remove-Item -Path $TempDir -Recurse -Force -ErrorAction SilentlyContinue

    Log "Dell driver pack installation completed successfully!" -Color Green
    Log "A reboot is recommended to finalize driver installation." -Color Yellow
}

# =============================================================================
# DONE
# =============================================================================
Log "" -Color White
Log "========================================" -Color Green
Log "UPDATE PROCESS FULLY COMPLETED" -Color Green
Log "========================================" -Color Green
Log "No further Windows or Dell updates are available." -Color Cyan
if (Test-Path $WuFlagPath) { Remove-Item -Path $WuFlagPath -Force | Out-Null }
Log "Progress flag removed. Script is reset for next full run." -Color DarkGray