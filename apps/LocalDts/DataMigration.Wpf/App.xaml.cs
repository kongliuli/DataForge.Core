using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DataMigration.Wpf.ViewModel;
using DataMigration.Wpf.Services;
using DataMigration.Contracts;
using DataMigration.Core;

namespace DataMigration.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static ServiceProvider? ServiceProvider { get; private set; }

    public App()
    {
        // 配置依赖注入容器
        var services = new ServiceCollection();
        ServiceProvider = ConfigureServices(services) as ServiceProvider;
    }

    public static IServiceProvider ConfigureServices(IServiceCollection services)
    {
        // 注册ViewModel
        services.AddTransient<IMainViewModel, MainViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DataSourceConfigViewModel>();
        services.AddTransient<DataTargetConfigViewModel>();
        services.AddTransient<PluginManagerViewModel>();
        services.AddTransient<TaskConfigViewModel>();
        services.AddTransient<TaskExecutionViewModel>();
        services.AddTransient<TransformerConfigViewModel>();
        services.AddTransient<PluginSelectionViewModel>();

        // 注册服务
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IPluginService, PluginService>();
        services.AddSingleton<DataMigration.Contracts.IMigrationService, DataMigration.Core.MigrationService>();
        services.AddSingleton<IPluginManager, PluginManager>();

        return services.BuildServiceProvider();
    }
}

