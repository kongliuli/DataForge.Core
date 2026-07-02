using DataMigration.Wpf.ViewModel;
using DataMigration.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DataMigration.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        if (App.ServiceProvider != null)
        {
            var mainViewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
            DataContext = mainViewModel;
            
            var navigationService = App.ServiceProvider.GetRequiredService<INavigationService>();
            navigationService.SetFrame(MainFrame);
            
            // 导航到默认页面
            navigationService.Navigate("PluginManagerPage");
        }
    }
}