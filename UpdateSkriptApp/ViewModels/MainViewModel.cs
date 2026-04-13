using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;

namespace UpdateSkriptApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Services.ILoggerService _logger;
        private readonly Services.PowerShellService _psService;
        private readonly Services.IDeploymentStateService _stateService;
        private readonly Services.DeploymentEngine _engine;

        [ObservableProperty]
        private string _systemModel = "Detecting...";

        [ObservableProperty]
        private string _manufacturer = "Detecting...";

        [ObservableProperty]
        private string _logOutput = "Application started. Ready to deploy.\n";

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _currentAction = "Waiting for user...";

        [ObservableProperty]
        private SolidColorBrush _statusBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Default Blue

        public MainViewModel()
        {
            _logger = new Services.FileLoggerService();
            _logger.OnLogLineReceived += (msg, color) => 
            {
                // Ensure UI update on the correct thread
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    LogOutput += msg + "\n";
                });
            };
            
            _logger.Log("Application initialized. Architecture: Senior MVVM.");
            
            _psService = new Services.PowerShellService();
            _psService.OnOutputReceived += (msg, color) => _logger.Log(msg, color);
            _psService.OnProgressChanged += (pct, status) => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    ProgressPercentage = pct;
                    if (!string.IsNullOrEmpty(status)) CurrentAction = status;
                });
            };

            _stateService = new Services.DeploymentStateService();
            _engine = new Services.DeploymentEngine(_logger, _psService, _stateService);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _logger.Log("Detecting system hardware...");
                
                // Fetch Manufacturer
                var mfr = await _psService.GetFirstObjectAsync<string>("(Get-CimInstance Win32_ComputerSystem).Manufacturer");
                if (mfr != null) Manufacturer = mfr.Trim();

                // Fetch Model
                var mdl = await _psService.GetFirstObjectAsync<string>("(Get-CimInstance Win32_ComputerSystem).Model");
                if (mdl != null) SystemModel = mdl.Trim();

                _engine.SystemModel = SystemModel;
                _engine.Manufacturer = Manufacturer;

                _logger.Log($"Hardware detected: {Manufacturer} {SystemModel}", "Green");
            }
            catch (Exception ex)
            {
                _logger.Log($"Error during initialization: {ex.Message}", "Red");
            }
        }

        [RelayCommand]
        private async Task Start()
        {
            try
            {
                StatusBrush = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Blue
                await _engine.RunDeploymentAsync();
                StatusBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Success Green
                CurrentAction = "Deployment Completed Successfully.";
            }
            catch (Exception ex)
            {
                StatusBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Error Red
                _logger.Log($"Critical error during deployment: {ex.Message}", "Red");
            }
        }

        [RelayCommand]
        private void Reinstall()
        {
            _logger.Log("User requested progress marker reset.", "Red");
            _stateService.ResetAll();
            _logger.Log("All progress flags have been deleted. Ready for clean run.", "Green");
            ProgressPercentage = 0;
            CurrentAction = "System reset complete.";
        }
    }
}
