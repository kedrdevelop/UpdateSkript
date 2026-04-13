namespace UpdateSkriptApp.Services
{
    public class DeploymentEngine
    {
        private readonly ILoggerService _logger;
        private readonly PowerShellService _psService;
        private readonly IDeploymentStateService _stateService;

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

            // PHASE 3 will follow...
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
            
            if (dlOk) _logger.Log("Catalog downloaded successfully.", "Green");
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
