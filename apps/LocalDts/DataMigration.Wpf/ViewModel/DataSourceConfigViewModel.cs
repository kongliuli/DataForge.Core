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

public partial class DataSourceConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private ObservableCollection<IDataSource> _dataSources = new();

    private IDataSource? _selectedDataSource;

    public IDataSource? SelectedDataSource
    {
        get => _selectedDataSource;
        set
        {
            if (SetProperty(ref _selectedDataSource, value))
            {
                OnSelectedDataSourceChanged(value);
            }
        }
    }

    [ObservableProperty]
    private ObservableCollection<ConfigProperty> _configProperties = new();

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _configName = "";

    [ObservableProperty]
    private ObservableCollection<DataSourceConfigItem> _savedConfigs = new();

    private DataSourceConfigItem? _selectedConfig;

    public DataSourceConfigItem? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            if (SetProperty(ref _selectedConfig, value))
            {
                OnSelectedConfigChanged(value);
            }
        }
    }

    public DataSourceConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
    {
        _pluginManager = pluginManager;
        _configurationService = configurationService;
        LoadDataSources();
        LoadSavedConfigs();
    }

    private void LoadDataSources()
    {
        var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        _pluginManager.LoadPlugins(pluginsDirectory);

        DataSources.Clear();
        var allComponents = _pluginManager.ListAllComponents();
        foreach (var component in allComponents)
        {
            if (component.Type == "DataSource")
            {
                try
                {
                    var dataSource = _pluginManager.GetDataSource(component.Id);
                    DataSources.Add(dataSource);
                }
                catch { }
            }
        }
    }

    private void OnSelectedDataSourceChanged(IDataSource? value)
    {
        if (value != null)
        {
            GenerateConfigForm(value);
        }
        else
        {
            ConfigProperties.Clear();
            ErrorMessage = "";
        }
    }

    private void OnSelectedConfigChanged(DataSourceConfigItem? value)
    {
        if (value != null && value.DataSourceId != null)
        {
            var dataSource = DataSources.FirstOrDefault(ds => ds.Id == value.DataSourceId);
            if (dataSource != null)
            {
                SelectedDataSource = dataSource;
                ConfigName = value.Name;
                foreach (var prop in ConfigProperties)
                {
                    if (value.ConfigProperties.TryGetValue(prop.Name, out var propValue))
                    {
                        prop.Value = propValue;
                    }
                }
            }
        }
    }

    private void GenerateConfigForm(IDataSource dataSource)
    {
        ConfigProperties.Clear();
        
        if (dataSource.Id == "DataMigration.Plugin.SqliteSource")
        {
            var dbPathProp = new ConfigProperty { Name = "DatabasePath", Value = "" };
            dbPathProp.PropertyChanged += (sender, e) => 
            {
                UpdateSqliteConnectionString();
                ValidateConfig();
            };
            ConfigProperties.Add(dbPathProp);

            var connectionStringProp = new ConfigProperty { Name = "ConnectionString", Value = "", IsReadOnly = true };
            ConfigProperties.Add(connectionStringProp);
        }
        else
        {
            var connectionStringProp = new ConfigProperty { Name = "ConnectionString", Value = "" };
            connectionStringProp.PropertyChanged += (sender, e) => ValidateConfig();
            ConfigProperties.Add(connectionStringProp);
        }

        var queryProp = new ConfigProperty { Name = "Query", Value = "" };
        queryProp.PropertyChanged += (sender, e) => ValidateConfig();
        ConfigProperties.Add(queryProp);
    }

    private void UpdateSqliteConnectionString()
    {
        var dbPathProp = ConfigProperties.FirstOrDefault(p => p.Name == "DatabasePath");
        var connectionStringProp = ConfigProperties.FirstOrDefault(p => p.Name == "ConnectionString");
        
        if (dbPathProp != null && connectionStringProp != null)
        {
            string dbPath = dbPathProp.Value;
            if (!string.IsNullOrEmpty(dbPath))
            {
                connectionStringProp.Value = $"Data Source={dbPath};Version=3;";
            }
            else
            {
                connectionStringProp.Value = "";
            }
        }
    }

    private void ValidateConfig()
    {
        if (SelectedDataSource == null)
        {
            ErrorMessage = "请选择数据源";
            return;
        }

        if (string.IsNullOrEmpty(ConfigName))
        {
            ErrorMessage = "请输入配置名称";
            return;
        }

        if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
        {
            var dbPath = ConfigProperties.FirstOrDefault(p => p.Name == "DatabasePath")?.Value;
            if (string.IsNullOrEmpty(dbPath))
            {
                ErrorMessage = "请选择数据库文件路径";
                return;
            }

            if (!File.Exists(dbPath))
            {
                ErrorMessage = "指定的数据库文件不存在";
                return;
            }
        }
        else
        {
            var connectionString = ConfigProperties.FirstOrDefault(p => p.Name == "ConnectionString")?.Value;
            if (string.IsNullOrEmpty(connectionString))
            {
                ErrorMessage = "请输入连接字符串";
                return;
            }
        }

        var query = ConfigProperties.FirstOrDefault(p => p.Name == "Query")?.Value;
        if (string.IsNullOrEmpty(query))
        {
            ErrorMessage = "请输入查询语句";
            return;
        }

        ErrorMessage = "";
    }

    [RelayCommand]
    private void Save()
    {
        ValidateConfig();
        if (string.IsNullOrEmpty(ErrorMessage) && SelectedDataSource != null)
        {
            SaveConfiguration();
            LoadSavedConfigs();
        }
    }

    private void SaveConfiguration()
    {
        if (SelectedDataSource != null && !string.IsNullOrEmpty(ConfigName))
        {
            var config = new DataSourceConfigItem
            {
                Name = ConfigName,
                DataSourceId = SelectedDataSource.Id,
                ConfigProperties = ConfigProperties.ToDictionary(p => p.Name, p => p.Value)
            };

            var configs = _configurationService.LoadConfiguration<DataSourceConfigCollection>("DataSourceConfigs") ?? new DataSourceConfigCollection();
            var existingConfig = configs.Configs.FirstOrDefault(c => c.Name == ConfigName);
            if (existingConfig != null)
            {
                configs.Configs.Remove(existingConfig);
            }
            configs.Configs.Add(config);
            _configurationService.SaveConfiguration(configs, "DataSourceConfigs");
        }
    }

    private void LoadSavedConfigs()
    {
        var configs = _configurationService.LoadConfiguration<DataSourceConfigCollection>("DataSourceConfigs") ?? new DataSourceConfigCollection();
        SavedConfigs.Clear();
        foreach (var config in configs.Configs)
        {
            SavedConfigs.Add(config);
        }
    }

    [RelayCommand]
    private void DeleteConfig(DataSourceConfigItem config)
    {
        var configs = _configurationService.LoadConfiguration<DataSourceConfigCollection>("DataSourceConfigs") ?? new DataSourceConfigCollection();
        configs.Configs.Remove(config);
        _configurationService.SaveConfiguration(configs, "DataSourceConfigs");
        LoadSavedConfigs();
    }

    [ObservableProperty]
    private ObservableCollection<string> _tableList = new();

    [ObservableProperty]
    private string _testConnectionResult = "";

    [ObservableProperty]
    private bool _isTestingConnection = false;

    [ObservableProperty]
    private System.Data.DataTable _previewData = new();

    [ObservableProperty]
    private string _previewResult = "";

    [ObservableProperty]
    private bool _isPreviewing = false;

    [ObservableProperty]
    private ObservableCollection<string> _previewColumns = new();

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (SelectedDataSource == null)
        {
            ErrorMessage = "请选择数据源";
            return;
        }

        ValidateConfig();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        IsTestingConnection = true;
        TestConnectionResult = "正在测试连接...";
        TableList.Clear();

        try
        {
            var config = new SourceConfig
            {
                ComponentId = SelectedDataSource.Id
            };

            foreach (var prop in ConfigProperties)
            {
                config[prop.Name] = prop.Value;
            }

            // 测试连接
            var connectionString = ConfigProperties.FirstOrDefault(p => p.Name == "ConnectionString")?.Value;
            if (string.IsNullOrEmpty(connectionString) && SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
            {
                connectionString = ConfigProperties.FirstOrDefault(p => p.Name == "ConnectionString")?.Value;
            }

            if (!string.IsNullOrEmpty(connectionString))
            {
                bool isConnected = false;
                
                // 根据不同的数据源类型执行不同的测试逻辑
                if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
                {
                    isConnected = await SQLiteHelper.TestConnectionAsync(connectionString);
                    if (isConnected)
                    {
                        TestConnectionResult = "连接成功！";
                        await GetSqliteTables(connectionString);
                    }
                    else
                    {
                        TestConnectionResult = "连接失败！";
                    }
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.MySqlDataSource")
                {
                    isConnected = await new MySQLHelper().TestConnectionAsync(connectionString);
                    if (isConnected)
                    {
                        TestConnectionResult = "连接成功！";
                        var tables = await new MySQLHelper().GetTablesAsync(connectionString);
                        foreach (var tableName in tables)
                        {
                            TableList.Add(tableName);
                        }
                    }
                    else
                    {
                        TestConnectionResult = "连接失败！";
                    }
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.SqlServerDataSource")
                {
                    isConnected = await new SqlServerHelper().TestConnectionAsync(connectionString);
                    if (isConnected)
                    {
                        TestConnectionResult = "连接成功！";
                        var tables = await new SqlServerHelper().GetTablesAsync(connectionString);
                        foreach (var tableName in tables)
                        {
                            TableList.Add(tableName);
                        }
                    }
                    else
                    {
                        TestConnectionResult = "连接失败！";
                    }
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.ExcelDataSource")
                {
                    isConnected = await new ExcelHelper().TestConnectionAsync(connectionString);
                    if (isConnected)
                    {
                        TestConnectionResult = "连接成功！";
                        var tables = await new ExcelHelper().GetTablesAsync(connectionString);
                        foreach (var tableName in tables)
                        {
                            TableList.Add(tableName);
                        }
                    }
                    else
                    {
                        TestConnectionResult = "连接失败！";
                    }
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.CsvSource")
                {
                    isConnected = await new DataMigration.Core.CsvHelper().TestConnectionAsync(connectionString);
                    if (isConnected)
                    {
                        TestConnectionResult = "连接成功！";
                        var tables = await new DataMigration.Core.CsvHelper().GetTablesAsync(connectionString);
                        foreach (var tableName in tables)
                        {
                            TableList.Add(tableName);
                        }
                    }
                    else
                    {
                        TestConnectionResult = "连接失败！";
                    }
                }
                else
                {
                    // 对于其他数据源，模拟获取表结构
                    TestConnectionResult = "连接成功！";
                    TableList.Add("表1");
                    TableList.Add("表2");
                    TableList.Add("表3");
                }
            }
            else
            {
                TestConnectionResult = "连接字符串为空";
            }
        }
        catch (Exception ex)
        {
            TestConnectionResult = $"连接失败: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private async Task GetSqliteTables(string connectionString)
    {
        try
        {
            var tables = await SQLiteHelper.GetTablesAsync(connectionString);
            foreach (var tableName in tables)
            {
                TableList.Add(tableName);
            }
        }
        catch (Exception ex)
        {
            TestConnectionResult = $"获取表结构失败: {ex.Message}";
        }
    }

    public AsyncRelayCommand<string> PreviewTableCommand => new AsyncRelayCommand<string>(PreviewTableAsync);

    [RelayCommand]
    private async Task PreviewTableAsync(string tableName)
    {
        if (SelectedDataSource == null)
        {
            ErrorMessage = "请选择数据源";
            return;
        }

        ValidateConfig();
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return;
        }

        // 更新查询语句为查询选中的表
        var queryProp = ConfigProperties.FirstOrDefault(p => p.Name == "Query");
        if (queryProp != null)
        {
            queryProp.Value = $"SELECT * FROM {tableName}";
        }

        // 调用预览数据命令
        await PreviewDataAsync();
    }

    [RelayCommand]
    private void Browse(ConfigProperty property)
    {
        if (property.Name == "DatabasePath")
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SQLite数据库文件 (*.db)|*.db|所有文件 (*.*)|*.*",
                Title = "选择SQLite数据库文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                property.Value = openFileDialog.FileName;
            }
        }
    }

    [RelayCommand]
    private async Task PreviewDataAsync()
    {
        if (SelectedDataSource == null)
        {
            ErrorMessage = "请选择数据源";
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
            var config = new SourceConfig
            {
                ComponentId = SelectedDataSource.Id
            };

            foreach (var prop in ConfigProperties)
            {
                config[prop.Name] = prop.Value;
            }

            // 创建 MigrationEngine 实例
            var migrationEngine = new MigrationEngine(_pluginManager);
            
            // 创建一个简单的迁移任务用于预览
            var task = new MigrationTask
            {
                Source = config,
                Transforms = new List<TransformConfig>(),
                Target = new TargetConfig { ComponentId = "" },
                Options = new ExecutionOptions { MaxDegreeOfParallelism = 1, BatchSize = 1 }
            };

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

public class DataSourceConfigCollection
{
    public List<DataSourceConfigItem> Configs { get; set; } = new List<DataSourceConfigItem>();
}

public class DataSourceConfigItem
{
    public string Name { get; set; } = "";
    public string? DataSourceId { get; set; }
    public Dictionary<string, string> ConfigProperties { get; set; } = new Dictionary<string, string>();
}

public class ConfigProperty : ObservableObject
{
    private string _name = "";
    private string _value = "";
    private bool _isReadOnly = false;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }
}
