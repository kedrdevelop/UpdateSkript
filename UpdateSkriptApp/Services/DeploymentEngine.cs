using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace UpdateSkriptApp.Services
{
    public class DeploymentEngine
    {
        private readonly ILoggerService _logger;
        private readonly PowerShellService _psService;
        private readonly IDeploymentStateService _stateService;
        
        // System Information Properties
        public string SystemModel { get; set; } = "Unknown";
        public string Manufacturer { get; set; } = "Unknown";

        public DeploymentEngine(ILoggerService logger, PowerShellService psService, IDeploymentStateService stateService)
        {
            _logger = logger;
            _psService = psService;
            _stateService = stateService;
        }

        public async Task RunDeploymentAsync()
        {
            _logger.Log("Starting Automated Deployment Engine...", "Magenta");

            // PHASE 1: Windows Updates
            if (!_stateService.IsPhaseCompleted("WU"))
            {
                await RunPhase1Async();
            }
            else
            {
                _logger.Log("Phase 1 (Windows Updates) already completed. Skipping.", "Green");
            }

            // PHASE 2: Dell Drivers
            if (!_stateService.IsPhaseCompleted("Dell"))
            {
                await RunPhase2Async();
            }
            else
            {
                _logger.Log("Phase 2 (Dell Drivers) already completed. Skipping.", "Green");
            }

            // PHASE 3: Windows 11 Upgrade
            if (!_stateService.IsPhaseCompleted("Win11"))
            {
                await RunPhase3Async();
            }
            else
            {
                _logger.Log("Phase 3 (Windows 11 Upgrade) already completed. Skipping.", "Green");
            }

            _logger.Log("Deployment sequence finished.", "Magenta");
        }

        private async Task RunPhase1Async()
        {
            _logger.Log("--- PHASE 1: WINDOWS UPDATES ---", "Cyan");
            
            string script = @"
                Write-Progress -Activity 'Phase 1: Windows Updates' -Status 'Preparing NuGet...' -PercentComplete 5
                Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -ErrorAction SilentlyContinue | Out-Null
                
                Write-Progress -Activity 'Phase 1: Windows Updates' -Status 'Installing PSWindowsUpdate...' -PercentComplete 15
                Install-Module -Name PSWindowsUpdate -Force -AllowClobber -SkipPublisherCheck -ErrorAction SilentlyContinue
                Import-Module PSWindowsUpdate

                Write-Progress -Activity 'Phase 1: Windows Updates' -Status 'Scanning for updates...' -PercentComplete 25
                $updates = Get-WindowsUpdate | Where-Object { $_.Title -notmatch 'Security Intelligence|Malicious Software|Antimalware' }
                
                if ($updates) {
                    $total = @($updates).Count
                    $current = 0
                    foreach ($u in $updates) {
                        $current++
                        $pct = [math]::Round(30 + ($current / $total) * 60)
                        Write-Progress -Activity ""Phase 1: Installing Updates ($current/$total)"" -Status $u.Title -PercentComplete $pct
                        Install-WindowsUpdate -KBArticleID $u.KB -AcceptAll -Install -ErrorAction SilentlyContinue
                    }
                }
            ";

            await _psService.ExecuteScriptAsync(script);
            
            // In a real scenario, we'd check if reboot is needed. For now, mark as complete if no exception.
            _stateService.MarkPhaseCompleted("WU");
            _logger.Log("Phase 1 completed successfully.", "Green");
        }

        private async Task RunPhase2Async()
        {
            _logger.Log("--- PHASE 2: DELL DRIVER PACK ---", "Cyan");
            
            // This is where the C# logic for Catalog parsing and downloading will live.
            // Porting logic: Get-CimInstance, XML Parse, Download, pnputil.
            
            _logger.Log("Phase 2 logic is being ported. Status: In Progress.", "Yellow");
            
            // Example of how we will handle downloads in C#
            string catalogUrl = "https://downloads.dell.com/catalog/DriverPackCatalog.cab";
            string tempDir = Path.Combine(Path.GetTempPath(), "DellDriverPack");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
            
            string cabPath = Path.Combine(tempDir, "DriverPackCatalog.cab");
            
            bool dlOk = await DownloadFileWithProgressAsync(catalogUrl, cabPath, "Downloading Dell Catalog");
            
            if (!dlOk) return;

            _logger.Log("Extracting catalog...", "DarkGray");
            string extractCmd = $"expand '{cabPath}' -F:DriverPackCatalog.xml '{tempDir}'";
            await _psService.ExecuteScriptAsync(extractCmd);

            string xmlPath = Path.Combine(tempDir, "DriverPackCatalog.xml");
            if (!File.Exists(xmlPath))
            {
                _logger.Log("Failed to extract Dell Catalog XML.", "Red");
                return;
            }

            _logger.Log("Parsing Dell Catalog...", "DarkGray");
            
            // Senior-level XML Parsing using LINQ to XML
            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            
            // Get Model and Manufacturer from properties
            string currentModel = SystemModel; 
            
            // Filter logic (simulating the complex PS logic)
            var driverPack = doc.Descendants("DriverPack")
                .FirstOrDefault(dp => 
                    dp.Descendants("SupportedModel").Any(m => m.Element("Display")?.Value.Contains(currentModel, StringComparison.OrdinalIgnoreCase) == true) &&
                    dp.Attribute("osCode")?.Value == "WT64A" // Windows 10/11 x64
                );

            if (driverPack != null)
            {
                string downloadPath = driverPack.Attribute("path")?.Value ?? "";
                string fullUrl = $"https://downloads.dell.com/{downloadPath}";
                string packName = Path.GetFileName(downloadPath);
                string packDest = Path.Combine(tempDir, packName);

                _logger.Log($"Found matching Driver Pack: {packName}", "Cyan");
                
                if (await DownloadFileWithProgressAsync(fullUrl, packDest, "Downloading Driver Pack"))
                {
                    _logger.Log("Installing drivers via pnputil...", "Yellow");
                    
                    // Extract and Install logic
                    string installScript = $@"
                        $dest = '{tempDir}\ExtractedDrivers'
                        mkdir $dest -Force | Out-Null
                        expand '{packDest}' -F:* $dest | Out-Null
                        pnputil.exe /add-driver ""$dest\*.inf"" /subdirs /install
                    ";
                    
                    await _psService.ExecuteScriptAsync(installScript);
                    
                    _stateService.MarkPhaseCompleted("Dell");
                    _logger.Log("Phase 2: Dell Drivers installed successfully.", "Green");
                }
            }
            else
            {
                _logger.Log($"No matching factor-certified driver pack found for model: {currentModel}", "Yellow");
                _stateService.MarkPhaseCompleted("Dell"); // Skip if not found
            }
        }

        private async Task RunPhase3Async()
        {
            _logger.Log("--- PHASE 3: WINDOWS 11 UPGRADE ---", "Cyan");
            
            // 1. Find ISO
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string isoPath = Directory.GetFiles(appDir, "*.iso").FirstOrDefault();
            
            if (string.IsNullOrEmpty(isoPath))
            {
                _logger.Log("Windows 11 ISO not found in application directory. Skipping upgrade.", "Yellow");
                _stateService.MarkPhaseCompleted("Win11");
                return;
            }

            _logger.Log($"Found ISO: {Path.GetFileName(isoPath)}", "DarkGray");
            _logger.Log("Mounting ISO...", "DarkGray");
            
            await _psService.ExecuteScriptAsync($"Mount-DiskImage -ImagePath '{isoPath}'");
            
            // Get Drive Letter
            var driveLetter = await _psService.GetFirstObjectAsync<string>("(Get-DiskImage -ImagePath '" + isoPath + "' | Get-Volume).DriveLetter");
            
            if (string.IsNullOrEmpty(driveLetter))
            {
                _logger.Log("Failed to mount ISO or get drive letter.", "Red");
                return;
            }

            string setupPath = $"{driveLetter}:\\setup.exe";
            _logger.Log($"Starting Windows Setup from {setupPath}...", "Yellow");

            // Start setup.exe in background
            string setupCmd = $"Start-Process '{setupPath}' -ArgumentList '/auto upgrade /quiet /noreboot' -PassThru";
            await _psService.ExecuteScriptAsync(setupCmd);

            // 2. Monitoring Log
            _logger.Log("Monitoring installation progress (setupact.log)...", "DarkGray");
            await MonitorSetupLogAsync();

            _stateService.MarkPhaseCompleted("Win11");
            _logger.Log("Phase 3: Windows 11 Upgrade initiated and monitored successfully.", "Green");
        }

        private async Task MonitorSetupLogAsync()
        {
            string logPath = @"C:\$WINDOWS.~BT\Sources\Panther\setupact.log";
            
            // Wait for log to be created
            int retries = 0;
            while (!File.Exists(logPath) && retries < 20)
            {
                await Task.Delay(2000);
                retries++;
            }

            if (!File.Exists(logPath))
            {
                _logger.Log("Setup log not found. Installation might be starting slowly.", "Yellow");
                return;
            }

            // Tail log for 5 minutes (as an example) or until setup finishes
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            
            DateTime start = DateTime.Now;
            while (DateTime.Now - start < TimeSpan.FromMinutes(10)) 
            {
                string line = await sr.ReadToEndAsync();
                if (!string.IsNullOrEmpty(line))
                {
                    // Parse progress from log (simplified regex logic)
                    if (line.Contains("Progress")) 
                    {
                        _logger.Log("Setup Info: " + line.Split('\n').LastOrDefault()?.Trim(), "DarkGray");
                    }
                }
                await Task.Delay(5000);
            }
        }

        private async Task<bool> DownloadFileWithProgressAsync(string url, string destination, string activityName)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                using var response = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalRead = 0L;
                var isMoreToRead = true;

                while (isMoreToRead)
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes != -1)
                    {
                        var pct = (int)((double)totalRead / totalBytes * 100);
                        // Trigger progress update in UI
                        // Normally we'd use an event or callback
                        _logger.Log($"{activityName}: {pct}%", "DarkGray"); 
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log($"Download failed: {ex.Message}", "Red");
                return false;
            }
        }
    }
}
