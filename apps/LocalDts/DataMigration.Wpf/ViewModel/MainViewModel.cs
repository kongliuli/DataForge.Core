using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Wpf.Services;

namespace DataMigration.Wpf.ViewModel;

/// <summary>
/// 主窗口视图模型接口
/// </summary>
public interface IMainViewModel
{
    string CurrentPageTitle { get; set; }
    string CurrentPage { get; set; }
    void Navigate(string pageName);
}

/// <summary>
/// 主窗口视图模型，管理应用程序的导航和页面状态
/// </summary>
public partial class MainViewModel : ObservableObject, IMainViewModel
{
    private readonly INavigationService? _navigationService;

    /// <summary>
    /// 当前页面标题
    /// </summary>
    [ObservableProperty]
    private string _currentPageTitle = "欢迎使用数据迁移工具";

    /// <summary>
    /// 当前页面标识
    /// </summary>
    [ObservableProperty]
    private string _currentPage = string.Empty;

    /// <summary>
    /// 运行时构造函数
    /// </summary>
    /// <param name="navigationService">导航服务</param>
    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    /// <summary>
    /// 设计时构造函数
    /// </summary>
    public MainViewModel()
    {
        // 设计时使用，不需要实际的导航服务
        CurrentPageTitle = "数据源配置";
        CurrentPage = "DataSourceConfigPage";
    }

    /// <summary>
    /// 导航命令
    /// </summary>
    /// <param name="pageName">目标页面名称</param>
    [RelayCommand]
    public void Navigate(string pageName)
    {
        _navigationService?.Navigate(pageName);
        UpdatePageInfo(pageName);
    }

    /// <summary>
    /// 更新页面信息
    /// </summary>
    /// <param name="pageName">页面名称</param>
    private void UpdatePageInfo(string pageName)
    {
        CurrentPage = pageName;
        CurrentPageTitle = pageName switch
        {
            "PluginManagerPage" => "插件管理",
            "DataSourceConfigPage" => "数据源配置",
            "DataTargetConfigPage" => "目标源配置",
            "TransformerConfigPage" => "清洗规则配置",
            "TaskConfigPage" => "任务管理",
            "TaskExecutionPage" => "任务执行",
            _ => "欢迎使用数据迁移工具"
        };
    }
}
