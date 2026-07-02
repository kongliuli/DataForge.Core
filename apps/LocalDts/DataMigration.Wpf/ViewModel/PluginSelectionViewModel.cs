using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Contracts;
using DataMigration.Core;

namespace DataMigration.Wpf.ViewModel;

/// <summary>
/// 插件类型枚举
/// </summary>
public enum PluginSelectionType
{
    /// <summary>
    /// 数据源插件
    /// </summary>
    DataSource,

    /// <summary>
    /// 目标源插件
    /// </summary>
    DataTarget,

    /// <summary>
    /// 转换器插件
    /// </summary>
    Transformer
}

/// <summary>
/// 插件选择视图模型
/// </summary>
public partial class PluginSelectionViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private List<PluginInfo> _allPlugins = new();

    /// <summary>
    /// 插件类型列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _pluginTypes = new()
    {
        "数据源",
        "目标源",
        "清洗规则"
    };

    /// <summary>
    /// 当前选中的插件类型索引
    /// </summary>
    [ObservableProperty]
    private int _selectedPluginTypeIndex;

    /// <summary>
    /// 过滤后的插件列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PluginInfo> _filteredPlugins = new();

    private PluginInfo? _selectedPlugin;

    /// <summary>
    /// 当前选中的插件
    /// </summary>
    public PluginInfo? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (SetProperty(ref _selectedPlugin, value))
            {
                OnSelectedPluginChanged(value);
            }
        }
    }

    /// <summary>
    /// 对话框结果
    /// </summary>
    [ObservableProperty]
    private bool? _dialogResult;

    /// <summary>
    /// 运行时构造函数
    /// </summary>
    /// <param name="pluginManager">插件管理器</param>
    public PluginSelectionViewModel(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        LoadPlugins();
    }

    /// <summary>
    /// 设计时构造函数
    /// </summary>
    public PluginSelectionViewModel()
    {
        // 设计时使用，创建模拟数据
        _pluginManager = null!;
        FilteredPlugins.Add(new PluginInfo { Name = "SQLite数据源", Id = "sqlite", Version = new Version(1, 0, 0), Type = "DataSource" });
        FilteredPlugins.Add(new PluginInfo { Name = "MySQL数据源", Id = "mysql", Version = new Version(1, 0, 0), Type = "DataSource" });
    }

    /// <summary>
    /// 初始化插件类型
    /// </summary>
    /// <param name="pluginType">插件类型</param>
    public void Initialize(PluginSelectionType pluginType)
    {
        SelectedPluginTypeIndex = (int)pluginType;
    }

    /// <summary>
    /// 加载所有插件
    /// </summary>
    private void LoadPlugins()
    {
        _allPlugins = _pluginManager.ListAllComponents().ToList();
        FilterPlugins();
    }

    /// <summary>
    /// 当选择的插件类型改变时过滤插件
    /// </summary>
    partial void OnSelectedPluginTypeIndexChanged(int value)
    {
        FilterPlugins();
    }

    /// <summary>
    /// 过滤插件列表
    /// </summary>
    private void FilterPlugins()
    {
        var selectedType = (PluginSelectionType)SelectedPluginTypeIndex;
        IEnumerable<PluginInfo> filtered = selectedType switch
        {
            PluginSelectionType.DataSource => _allPlugins.Where(p => p.Type == "DataSource"),
            PluginSelectionType.DataTarget => _allPlugins.Where(p => p.Type == "DataTarget"),
            PluginSelectionType.Transformer => _allPlugins.Where(p => p.Type == "Transformer"),
            _ => Enumerable.Empty<PluginInfo>()
        };

        FilteredPlugins.Clear();
        foreach (var plugin in filtered)
        {
            FilteredPlugins.Add(plugin);
        }
    }

    /// <summary>
    /// 确认选择命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        DialogResult = true;
    }

    /// <summary>
    /// 是否可以确认
    /// </summary>
    private bool CanConfirm => SelectedPlugin != null;

    /// <summary>
    /// 取消选择命令
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    /// <summary>
    /// 当选择插件时更新命令状态
    /// </summary>
    private void OnSelectedPluginChanged(PluginInfo? value)
    {
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}
