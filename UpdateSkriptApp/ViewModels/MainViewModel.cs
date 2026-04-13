using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace UpdateSkriptApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
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

        [RelayCommand]
        private async Task Start()
        {
            CurrentAction = "Initializing PowerShell Session...";
            LogOutput += "Starting deployment process...\n";
            // Logic to be implemented
            await Task.Delay(1000); 
            ProgressPercentage = 10;
        }

        [RelayCommand]
        private void Reinstall()
        {
            LogOutput += "Resetting progress markers...\n";
            // Logic to be implemented
        }
    }
}
