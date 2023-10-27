using System.Windows;
using ViewModel;

namespace Lab2
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