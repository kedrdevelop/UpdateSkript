using System.Windows;

namespace UpdateSkriptApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModels.MainViewModel();
        }
    }
}
