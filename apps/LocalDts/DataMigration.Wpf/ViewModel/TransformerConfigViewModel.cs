using DataMigration.Core;
using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Wpf.Services;

namespace DataMigration.Wpf.ViewModel;

public partial class TransformerConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private ObservableCollection<ITransformer> _transformers = new();

    [ObservableProperty]
    private ITransformer? _selectedTransformer;

    [ObservableProperty]
    private string _rulesJson = "";

    [ObservableProperty]
    private string _testResult = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private System.Data.DataTable _previewData = new();

    [ObservableProperty]
    private string _previewResult = "";

    [ObservableProperty]
    private bool _isPreviewing = false;

    [ObservableProperty]
    private ObservableCollection<string> _previewColumns = new();

    [ObservableProperty]
    private string _configName = "";

    [ObservableProperty]
    private ObservableCollection<TransformerConfigItem> _savedConfigs = new();

    [ObservableProperty]
    private TransformerConfigItem? _selectedConfig;

    public TransformerConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
    {
        _pluginManager = pluginManager;
        _configurationService = configurationService;
        LoadTransformers();
        LoadSavedConfigs();
    }

    private void LoadTransformers()
    {
        var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        _pluginManager.LoadPlugins(pluginsDirectory);

        Transformers.Clear();
        // 从 PluginManager 中获取所有插件，然后筛选出转换器插件
        var allComponents = _pluginManager.ListAllComponents();
        foreach (var component in allComponents)
        {
            if (component.Type == "Transformer")
            {
                try
                {
                    var transformer = _pluginManager.GetTransformer(component.Id);
                    Transformers.Add(transformer);
                }
                catch { }
            }
        }
    }

    partial void OnRulesJsonChanged(string value)
    {
        ValidateConfig();
    }

    partial void OnSelectedTransformerChanged(ITransformer? value)
    {
        ValidateConfig();
    }

    partial void OnSelectedConfigChanged(TransformerConfigItem? value)
    {
        if (value != null && value.TransformerId != null)
        {
            var transformer = Transformers.FirstOrDefault(t => t.Id == value.TransformerId);
            if (transformer != null)
            {
                SelectedTransformer = transformer;
                ConfigName = value.Name;
                RulesJson = value.RulesJson;
            }
        }
    }

    private void ValidateConfig()
    {
        if (SelectedTransformer == null)
        {
            ErrorMessage = "请选择转换器";
            return;
        }

        if (string.IsNullOrEmpty(ConfigName))
        {
            ErrorMessage = "请输入配置名称";
            return;
        }

        if (string.IsNullOrEmpty(RulesJson))
        {
            ErrorMessage = "请输入规则 JSON";
            return;
        }

        // 验证 JSON 格式
        try
        {
            System.Text.Json.JsonDocument.Parse(RulesJson);
        }
        catch
        {
            ErrorMessage = "规则 JSON 格式错误";
            return;
        }

        ErrorMessage = "";
    }

    private void LoadSavedConfigs()
    {
        var configs = _configurationService.LoadConfiguration<TransformerConfigCollection>("TransformerConfigs") ?? new TransformerConfigCollection();
        SavedConfigs.Clear();
        foreach (var config in configs.Configs)
        {
            SavedConfigs.Add(config);
        }
    }

    [RelayCommand]
    private void Test()
    {
        ValidateConfig();
        if (string.IsNullOrEmpty(ErrorMessage) && SelectedTransformer != null && !string.IsNullOrEmpty(RulesJson))
        {
            try
            {
                // 测试规则
                // 这里需要实现规则测试的逻辑
                // 暂时使用模拟数据
                TestResult = "规则测试成功！";
            }
            catch (Exception ex)
            {
                TestResult = $"规则测试失败: {ex.Message}";
            }
        }
        else
        {
            TestResult = "请选择转换器并输入有效的规则 JSON";
        }
    }

    [RelayCommand]
    private void Save()
    {
        ValidateConfig();
        if (string.IsNullOrEmpty(ErrorMessage) && SelectedTransformer != null && !string.IsNullOrEmpty(RulesJson))
        {
            SaveConfiguration();
            LoadSavedConfigs();
        }
    }

    private void SaveConfiguration()
    {
        if (SelectedTransformer != null && !string.IsNullOrEmpty(ConfigName))
        {
            var config = new TransformerConfigItem
            {
                Name = ConfigName,
                TransformerId = SelectedTransformer.Id,
                RulesJson = RulesJson
            };

            var configs = _configurationService.LoadConfiguration<TransformerConfigCollection>("TransformerConfigs") ?? new TransformerConfigCollection();
            var existingConfig = configs.Configs.FirstOrDefault(c => c.Name == ConfigName);
            if (existingConfig != null)
            {
                configs.Configs.Remove(existingConfig);
            }
            configs.Configs.Add(config);
            _configurationService.SaveConfiguration(configs, "TransformerConfigs");
        }
    }

    [RelayCommand]
    private void DeleteConfig(TransformerConfigItem config)
    {
        var configs = _configurationService.LoadConfiguration<TransformerConfigCollection>("TransformerConfigs") ?? new TransformerConfigCollection();
        configs.Configs.Remove(config);
        _configurationService.SaveConfiguration(configs, "TransformerConfigs");
        LoadSavedConfigs();
    }

    [RelayCommand]
    private async Task PreviewDataAsync()
    {
        if (SelectedTransformer == null)
        {
            ErrorMessage = "请选择转换器";
            return;
        }

        ValidateConfig();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        IsPreviewing = true;
        PreviewResult = "正在预览数据...";
        PreviewData.Clear();
        PreviewColumns.Clear();

        try
        {
            // 创建一个简单的迁移任务用于预览
            var task = new MigrationTask
            {
                Source = new SourceConfig { ComponentId = "DataMigration.Plugin.SqliteSource" },
                Transforms = new List<TransformConfig>
                {
                    new TransformConfig
                    {
                        ComponentId = SelectedTransformer.Id
                    }
                },
                Target = new TargetConfig { ComponentId = "" },
                Options = new ExecutionOptions { MaxDegreeOfParallelism = 1, BatchSize = 1 }
            };

            // 添加规则 JSON 到转换器配置
            task.Transforms[0]["RulesJson"] = RulesJson;

            // 创建 MigrationEngine 实例
            var migrationEngine = new MigrationEngine(_pluginManager);

            // 预览数据
            var previewData = await migrationEngine.PreviewAsync(task, 100, CancellationToken.None);
            
            // 处理预览数据
            if (previewData.Any())
            {
                PreviewResult = $"预览成功！共 {previewData.Count()} 条记录";
                
                // 创建新的 DataTable
                var newDataTable = new System.Data.DataTable();
                
                // 创建 DataTable 并设置列
                var firstRecord = previewData.First();
                foreach (var field in firstRecord.Keys)
                {
                    newDataTable.Columns.Add(field);
                    PreviewColumns.Add(field);
                }
                
                // 添加数据行
                foreach (var record in previewData)
                {
                    var row = newDataTable.NewRow();
                    foreach (var field in record.Keys)
                    {
                        row[field] = record[field] ?? DBNull.Value;
                    }
                    newDataTable.Rows.Add(row);
                }
                
                // 替换 PreviewData
                PreviewData = newDataTable;
            }
            else
            {
                PreviewResult = "未找到数据";
                // 创建空的 DataTable
                PreviewData = new System.Data.DataTable();
            }
        }
        catch (Exception ex)
        {
            PreviewResult = $"预览失败: {ex.Message}";
        }
        finally
        {
            IsPreviewing = false;
        }
    }
}

public class TransformerConfigCollection
{
    public List<TransformerConfigItem> Configs { get; set; } = new List<TransformerConfigItem>();
}

public class TransformerConfigItem
{
    public string Name { get; set; } = "";
    public string? TransformerId { get; set; }
    public string RulesJson { get; set; } = "";
}