using System.Windows;
using ViewModel;

namespace WpfApp
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel viewModel = new();
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}