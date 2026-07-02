using CommunityToolkit.Mvvm.ComponentModel;
using DataMigration.Contracts;
using DataMigration.Wpf.Services;
using System.Collections.ObjectModel;
using System.IO;
using DataMigration.Core;

namespace DataMigration.Wpf.ViewModel.TaskConfig;

/// <summary>
/// 目标源配置视图模型
/// </summary>
public partial class DataTargetConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// 目标源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<IDataTarget> _dataTargets = new();

    /// <summary>
    /// 选中的目标源
    /// </summary>
    [ObservableProperty]
    private IDataTarget? _selectedDataTarget;

    /// <summary>
    /// 已保存的目标源配置
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataTargetConfigItem> _savedDataTargetConfigs = new();

    /// <summary>
    /// 选中的目标源配置
    /// </summary>
    [ObservableProperty]
    private DataTargetConfigItem? _selectedDataTargetConfig;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginManager">插件管理器</param>
    /// <param name="configurationService">配置服务</param>
    public DataTargetConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
    {
        _pluginManager = pluginManager;
        _configurationService = configurationService;
        LoadSavedConfigs();
    }

    /// <summary>
    /// 加载已保存的配置
    /// </summary>
    private void LoadSavedConfigs()
    {
        // 加载已保存的目标源配置
        var dataTargetConfigs = _configurationService.LoadConfiguration<DataTargetConfigCollection>("DataTargetConfigs") ?? new DataTargetConfigCollection();
        var newDataTargetConfigs = new ObservableCollection<DataTargetConfigItem>(dataTargetConfigs.Configs);
        SavedDataTargetConfigs = newDataTargetConfigs;
    }

    /// <summary>
    /// 加载配置选项
    /// </summary>
    public async Task LoadConfigOptionsAsync()
    {
        var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        await Task.Run(() => _pluginManager.LoadPlugins(pluginsDirectory));

        var allComponents = _pluginManager.ListAllComponents();

        // 加载目标源
        var newDataTargets = new ObservableCollection<IDataTarget>();
        foreach (var component in allComponents)
        {
            if (component.Type == "DataTarget")
            {
                try
                {
                    var dataTarget = _pluginManager.GetTarget(component.Id);
                    newDataTargets.Add(dataTarget);
                }
                catch { }
            }
        }
        DataTargets = newDataTargets;
    }

    /// <summary>
    /// 重置配置
    /// </summary>
    public void Reset()
    {
        SelectedDataTarget = null;
        SelectedDataTargetConfig = null;
    }

    /// <summary>
    /// 获取数据库路径
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>数据库路径</returns>
    public string GetDatabasePath(string fileName)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataPath, "DataMigrationTool");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, fileName);
    }
}
