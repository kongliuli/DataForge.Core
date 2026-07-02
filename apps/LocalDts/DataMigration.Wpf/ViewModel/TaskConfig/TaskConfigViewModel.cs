using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Wpf.Services;
using System.Collections.ObjectModel;
using DataMigration.Contracts;
using DataMigration.Core;

namespace DataMigration.Wpf.ViewModel.TaskConfig;

/// <summary>
/// 任务配置主视图模型，协调各个子视图模型
/// </summary>
public partial class TaskConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// 任务名称
    /// </summary>
    [ObservableProperty]
    private string _taskName = "";

    /// <summary>
    /// 数据源配置视图模型
    /// </summary>
    public DataSourceConfigViewModel DataSourceConfig { get; }

    /// <summary>
    /// 目标源配置视图模型
    /// </summary>
    public DataTargetConfigViewModel DataTargetConfig { get; }

    /// <summary>
    /// 字段映射配置视图模型
    /// </summary>
    public FieldMappingViewModel FieldMappingConfig { get; }

    /// <summary>
    /// 格式配置视图模型
    /// </summary>
    public FormatConfigViewModel FormatConfig { get; }

    /// <summary>
    /// 配置摘要
    /// </summary>
    [ObservableProperty]
    private string _configSummary = "";

    /// <summary>
    /// 配置状态
    /// </summary>
    [ObservableProperty]
    private string _configStatus = "未验证";

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading = false;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginManager">插件管理器</param>
    /// <param name="configurationService">配置服务</param>
    public TaskConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
    {
        _pluginManager = pluginManager;
        _configurationService = configurationService;

        // 初始化子视图模型
        DataSourceConfig = new DataSourceConfigViewModel(pluginManager, configurationService);
        DataTargetConfig = new DataTargetConfigViewModel(pluginManager, configurationService);
        FieldMappingConfig = new FieldMappingViewModel();
        FormatConfig = new FormatConfigViewModel();

        // 异步加载配置选项，避免阻塞UI
        _ = LoadConfigOptionsAsync();
    }

    /// <summary>
    /// 加载配置选项
    /// </summary>
    private async Task LoadConfigOptionsAsync()
    {
        IsLoading = true;
        try
        {
            await DataSourceConfig.LoadConfigOptionsAsync();
            await DataTargetConfig.LoadConfigOptionsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 新建任务
    /// </summary>
    [RelayCommand]
    private void New()
    {
        TaskName = "";
        DataSourceConfig.Reset();
        DataTargetConfig.Reset();
        FieldMappingConfig.Reset();
        FormatConfig.Reset();
        ConfigSummary = "";
        ConfigStatus = "未验证";
    }

    /// <summary>
    /// 加载任务配置
    /// </summary>
    [RelayCommand]
    private void Load()
    {
        // 实现加载任务配置的逻辑
        // 这里可以复用原有的Load方法逻辑
    }

    /// <summary>
    /// 保存任务配置
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        // 实现保存任务配置的逻辑
        // 这里可以复用原有的Save方法逻辑
    }

    /// <summary>
    /// 执行任务
    /// </summary>
    [RelayCommand]
    private async Task Execute()
    {
        // 实现执行任务的逻辑
        // 这里可以复用原有的Execute方法逻辑
    }

    /// <summary>
    /// 生成配置摘要
    /// </summary>
    [RelayCommand]
    private void GenerateConfigSummary()
    {
        // 生成配置摘要
        var summary = new System.Text.StringBuilder();
        
        // 数据源信息
        summary.AppendLine($"数据源: {DataSourceConfig.SelectedDataSource?.Name ?? "未选择"}");
        
        // 目标源信息
        summary.AppendLine($"目标源: {DataTargetConfig.SelectedDataTarget?.Name ?? "未选择"}");
        
        // 关联关系
        summary.AppendLine("关联关系:");
        if (FieldMappingConfig.TableRelations.Count > 0)
        {
            foreach (var relation in FieldMappingConfig.TableRelations)
            {
                summary.AppendLine($"  {relation.SourceTable}.{relation.SourceColumn} -> {relation.TargetTable}.{relation.TargetColumn}");
            }
        }
        else
        {
            summary.AppendLine("  无");
        }
        
        // 列重命名
        summary.AppendLine("列重命名:");
        if (FieldMappingConfig.ColumnRenameMappings.Count > 0)
        {
            foreach (var mapping in FieldMappingConfig.ColumnRenameMappings)
            {
                summary.AppendLine($"  {mapping.OriginalColumnName} -> {mapping.NewColumnName}");
            }
        }
        else
        {
            summary.AppendLine("  无");
        }
        
        // 字段映射
        summary.AppendLine("字段映射:");
        if (FieldMappingConfig.FieldMappings.Count > 0)
        {
            foreach (var mapping in FieldMappingConfig.FieldMappings)
            {
                summary.AppendLine($"  {mapping.SourceField} -> {mapping.TargetField}");
            }
        }
        else
        {
            summary.AppendLine("  无");
        }
        
        // 格式配置
        summary.AppendLine("格式配置:");
        if (FormatConfig.FormatConfigs.Count > 0)
        {
            foreach (var config in FormatConfig.FormatConfigs)
            {
                summary.AppendLine($"  {config.FieldName}: {config.FormatType} - {config.FormatString}");
            }
        }
        else
        {
            summary.AppendLine("  无");
        }
        
        ConfigSummary = summary.ToString();
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    [RelayCommand]
    private void ValidateConfig()
    {
        // 验证配置的有效性
        var errors = new List<string>();
        
        if (DataSourceConfig.SelectedDataSource == null)
        {
            errors.Add("未选择数据源");
        }
        
        if (DataTargetConfig.SelectedDataTarget == null)
        {
            errors.Add("未选择目标源");
        }
        
        if (FieldMappingConfig.FieldMappings.Count == 0)
        {
            errors.Add("未配置字段映射");
        }
        
        if (errors.Count == 0)
        {
            ConfigStatus = "配置有效";
            System.Windows.MessageBox.Show("配置验证通过", "验证成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        else
        {
            ConfigStatus = "配置无效";
            var errorMessage = "配置验证失败:\n" + string.Join("\n", errors);
            System.Windows.MessageBox.Show(errorMessage, "验证失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
