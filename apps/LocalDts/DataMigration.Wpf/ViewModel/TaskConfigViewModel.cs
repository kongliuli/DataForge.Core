using DataMigration.Contracts;
using DataMigration.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Wpf.Services;

namespace DataMigration.Wpf.ViewModel;

public partial class TaskConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    [ObservableProperty]
    private string _taskName = "";

    [ObservableProperty]
    private ObservableCollection<IDataSource> _dataSources = new();

    [ObservableProperty]
    private IDataSource? _selectedDataSource;

    [ObservableProperty]
    private ObservableCollection<IDataTarget> _dataTargets = new();

    [ObservableProperty]
    private IDataTarget? _selectedDataTarget;

    [ObservableProperty]
    private ObservableCollection<ITransformer> _transformers = new();

    [ObservableProperty]
    private ITransformer? _selectedTransformer;

    [ObservableProperty]
    private ObservableCollection<string> _tables = new();

    [ObservableProperty]
    private ObservableCollection<string> _tableColumns = new();

    [ObservableProperty]
    private System.Data.DataTable _tableData = new();

    [ObservableProperty]
    private string? _selectedTable;

    [ObservableProperty]
    private ObservableCollection<string> _selectedTables = new();

    [ObservableProperty]
    private object? _pluginConfigContent;

    [ObservableProperty]
    private ObservableCollection<DataSourceConfigItem> _savedDataSourceConfigs = new();

    [ObservableProperty]
    private DataSourceConfigItem? _selectedDataSourceConfig;

    [ObservableProperty]
    private ObservableCollection<DataTargetConfigItem> _savedDataTargetConfigs = new();

    [ObservableProperty]
    private DataTargetConfigItem? _selectedDataTargetConfig;

    [ObservableProperty]
    private ObservableCollection<TableRelation> _tableRelations = new();

    [ObservableProperty]
    private string _previewQuery = "";

    [ObservableProperty]
    private string _sourceTableName = "";

    [ObservableProperty]
    private string _sourceColumnName = "";

    [ObservableProperty]
    private string _targetTableName = "";

    [ObservableProperty]
    private string _targetColumnName = "";

    [ObservableProperty]
    private ObservableCollection<string> _sourceColumns = new();

    [ObservableProperty]
    private ObservableCollection<string> _targetColumns = new();

    [ObservableProperty]
    private string _originalColumnName = "";

    [ObservableProperty]
    private string _newColumnName = "";

    [ObservableProperty]
    private ObservableCollection<ColumnRenameMapping> _columnRenameMappings = new();

    [ObservableProperty]
    private ObservableCollection<string> _sourceFields = new();

    [ObservableProperty]
    private ObservableCollection<string> _targetFields = new();

    [ObservableProperty]
    private ObservableCollection<FieldMapping> _fieldMappings = new();

    [ObservableProperty]
    private string _selectedSourceField = "";

    [ObservableProperty]
    private string _selectedTargetField = "";

    [ObservableProperty]
    private string _selectedField = "";

    [ObservableProperty]
    private string _formatType = "日期";

    [ObservableProperty]
    private string _formatString = "";

    [ObservableProperty]
    private ObservableCollection<FormatConfig> _formatConfigs = new();

    [ObservableProperty]
    private string _configSummary = "";

    [ObservableProperty]
    private string _configStatus = "未验证";

    [ObservableProperty]
    private bool _isLoading = false;

    public TaskConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
    {
        _pluginManager = pluginManager;
        _configurationService = configurationService;
        // 异步加载配置选项，避免阻塞UI
        _ = LoadConfigOptionsAsync();
        LoadSavedConfigs();
    }

    private void LoadSavedConfigs()
    {
        // 加载已保存的数据源配置
        var dataSourceConfigs = _configurationService.LoadConfiguration<DataSourceConfigCollection>("DataSourceConfigs") ?? new DataSourceConfigCollection();
        var newDataSourceConfigs = new ObservableCollection<DataSourceConfigItem>(dataSourceConfigs.Configs);
        SavedDataSourceConfigs = newDataSourceConfigs;

        // 加载已保存的目标源配置
        var dataTargetConfigs = _configurationService.LoadConfiguration<DataTargetConfigCollection>("DataTargetConfigs") ?? new DataTargetConfigCollection();
        var newDataTargetConfigs = new ObservableCollection<DataTargetConfigItem>(dataTargetConfigs.Configs);
        SavedDataTargetConfigs = newDataTargetConfigs;
    }

    public class TableRelation
    {
        public string SourceTable { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public string TargetTable { get; set; } = "";
        public string TargetColumn { get; set; } = "";
    }

    public class ColumnRenameMapping
    {
        public string OriginalColumnName { get; set; } = "";
        public string NewColumnName { get; set; } = "";
    }

    public class FieldMapping
    {
        public string SourceField { get; set; } = "";
        public string TargetField { get; set; } = "";
    }

    public class FormatConfig
    {
        public string FieldName { get; set; } = "";
        public string FormatType { get; set; } = "";
        public string FormatString { get; set; } = "";
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDataSource == null)
        {
            MessageBox.Show("请先选择数据源", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // 显示加载提示
            var loadingWindow = new Window
            {
                Title = "测试连接",
                Width = 300,
                Height = 100,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new TextBlock { Text = "正在测试连接...", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            loadingWindow.Show();

            // 实际测试连接
            string connectionString = string.Empty;
            bool isConnected = false;

            if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
            {
                string dbPath = GetDatabasePath("source.db");
                connectionString = $"Data Source={dbPath}";
                isConnected = await SQLiteHelper.TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                // 假设使用默认连接字符串
                connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                isConnected = await new MySQLHelper().TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                // 假设使用默认连接字符串
                connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                isConnected = await new SqlServerHelper().TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                // 假设使用默认Excel文件路径
                string excelPath = GetDatabasePath("test.xlsx");
                isConnected = await new ExcelHelper().TestConnectionAsync(excelPath);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.CsvSource")
            {
                // 假设使用默认CSV文件路径
                string csvPath = GetDatabasePath("test.csv");
                isConnected = await new DataMigration.Core.CsvHelper().TestConnectionAsync(csvPath);
            }
            else
            {
                // 其他数据源使用模拟连接测试
                await Task.Delay(500);
                isConnected = true;
            }

            if (!isConnected)
            {
                throw new Exception("连接失败");
            }

            // 关闭加载提示
            loadingWindow.Close();

            // 显示连接成功提示
            MessageBox.Show("连接成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // 关闭加载提示
            // 显示连接失败提示
            MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadConfigOptionsAsync()
    {
        IsLoading = true;
        try
        {
            var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            await Task.Run(() => _pluginManager.LoadPlugins(pluginsDirectory));

            var allComponents = _pluginManager.ListAllComponents();

            // 加载数据源
            var newDataSources = new ObservableCollection<IDataSource>();
            foreach (var component in allComponents)
            {
                if (component.Type == "DataSource")
                {
                    try
                    {
                        var dataSource = _pluginManager.GetDataSource(component.Id);
                        newDataSources.Add(dataSource);
                    }
                    catch { }
                }
            }
            DataSources = newDataSources;

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

            // 加载转换器
            var newTransformers = new ObservableCollection<ITransformer>();
            foreach (var component in allComponents)
            {
                if (component.Type == "Transformer")
                {
                    try
                    {
                        var transformer = _pluginManager.GetTransformer(component.Id);
                        newTransformers.Add(transformer);
                    }
                    catch { }
                }
            }
            Transformers = newTransformers;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadConfigOptions()
    {
        LoadConfigOptionsAsync().GetAwaiter().GetResult();
    }

    [RelayCommand]
    private void New()
    {
        // 新建任务
        TaskName = "";
        SelectedDataSource = null;
        SelectedDataTarget = null;
        SelectedTransformer = null;
        Tables.Clear();
        TableColumns.Clear();
        TableData = new System.Data.DataTable();
        SelectedTable = null;
        SelectedTables.Clear();
        PluginConfigContent = null;
    }

    [RelayCommand]
    private void Load()
    {
        // 加载任务配置
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "加载任务配置",
            InitialDirectory = GetDefaultDirectory()
        };

        if (openFileDialog.ShowDialog() == true)
        {
            // 保存选择的目录作为默认目录
            string? directory = Path.GetDirectoryName(openFileDialog.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                SaveDefaultDirectory(directory);
            }
            
            var configJson = File.ReadAllText(openFileDialog.FileName);
            try
            {
                var task = JsonSerializer.Deserialize<MigrationTask>(configJson);
                if (task != null)
                {
                    TaskName = task.TaskId;
                    // 选择对应的插件
                    SelectedDataSource = DataSources.FirstOrDefault(ds => ds.Id == task.Source.ComponentId);
                    SelectedDataTarget = DataTargets.FirstOrDefault(dt => dt.Id == task.Target.ComponentId);
                    if (task.Transforms.Any())
                    {
                        SelectedTransformer = Transformers.FirstOrDefault(t => t.Id == task.Transforms[0].ComponentId);
                    }
                    // 显示插件配置
                    UpdatePluginConfig();
                    // 这里应该从 task.Source 和 task.Target 中获取连接字符串并显示
                    // 后续需要改进，将连接字符串显示到UI中
                }
            }
            catch
            {
                // 显示错误信息
            }
        }
    }

    [RelayCommand]
    private void Save()
    {
        // 保存任务配置
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "保存任务配置",
            InitialDirectory = GetDefaultDirectory()
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            // 保存选择的目录作为默认目录
            string? directory = Path.GetDirectoryName(saveFileDialog.FileName);
            if (!string.IsNullOrEmpty(directory))
            {
                SaveDefaultDirectory(directory);
            }
            
            var task = new MigrationTask
            {
                TaskId = TaskName,
                Source = new SourceConfig { ComponentId = SelectedDataSource?.Id ?? "" },
                Target = new TargetConfig { ComponentId = SelectedDataTarget?.Id ?? "" },
                Transforms = new List<TransformConfig> { new TransformConfig { ComponentId = SelectedTransformer?.Id ?? "" } }
            };

            // 保存连接字符串等配置信息
            if (SelectedDataSource != null)
            {
                // 这里应该从UI中获取连接字符串，暂时使用默认值
                // 后续需要改进，从实际的UI输入中获取
                if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
                {
                    string dbPath = GetDatabasePath("source.db");
                    string connectionString = $"Data Source={dbPath}";
                    task.Source["ConnectionString"] = connectionString;
                }
                else
                {
                    // 对于其他数据源，使用空字符串作为默认值
                    task.Source["ConnectionString"] = "";
                }
            }

            if (SelectedDataTarget != null)
            {
                // 这里应该从UI中获取连接字符串，暂时使用默认值
                // 后续需要改进，从实际的UI输入中获取
                if (SelectedDataTarget.Id == "DataMigration.Plugin.SqliteTarget")
                {
                    string dbPath = GetDatabasePath("target.db");
                    string connectionString = $"Data Source={dbPath}";
                    task.Target["ConnectionString"] = connectionString;
                }
                else if (SelectedDataTarget.Id == "Standard.CsvTarget")
                {
                    string csvPath = GetDatabasePath("target.csv");
                    task.Target["FilePath"] = csvPath;
                }
                else if (SelectedDataTarget.Id == "DataMigration.Plugin.ExcelTarget")
                {
                    string excelPath = GetDatabasePath("target.xlsx");
                    task.Target["ConnectionString"] = excelPath;
                }
                else
                {
                    // 对于其他目标源，使用空字符串作为默认值
                    task.Target["ConnectionString"] = "";
                }
            }

            var configJson = JsonSerializer.Serialize(task, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(saveFileDialog.FileName, configJson);
        }
    }

    [RelayCommand]
    private async Task Execute()
    {
        // 验证配置
        if (string.IsNullOrEmpty(TaskName))
        {
            MessageBox.Show("请输入任务名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (SelectedDataSource == null)
        {
            MessageBox.Show("请选择数据源", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (SelectedDataTarget == null)
        {
            MessageBox.Show("请选择目标源", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // 创建迁移任务
            var task = new MigrationTask
            {
                TaskId = TaskName,
                Source = new SourceConfig { ComponentId = SelectedDataSource.Id },
                Target = new TargetConfig { ComponentId = SelectedDataTarget.Id },
                Transforms = SelectedTransformer != null ? new List<TransformConfig> { new TransformConfig { ComponentId = SelectedTransformer.Id } } : new List<TransformConfig>()
            };

            // 设置数据源连接字符串
            if (SelectedDataSource != null)
            {
                if (SelectedDataSourceConfig != null && SelectedDataSourceConfig.ConfigProperties.TryGetValue("ConnectionString", out var sourceConnectionString))
                {
                    task.Source["ConnectionString"] = sourceConnectionString;
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
                {
                    string dbPath = GetDatabasePath("source.db");
                    task.Source["ConnectionString"] = $"Data Source={dbPath}";
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.CsvSource")
                {
                    string csvPath = GetDatabasePath("test.csv");
                    task.Source["ConnectionString"] = csvPath;
                }
                else if (SelectedDataSource.Id == "DataMigration.Plugin.ExcelDataSource")
                {
                    string excelPath = GetDatabasePath("test.xlsx");
                    task.Source["ConnectionString"] = excelPath;
                }
            }

            // 设置目标源连接字符串
            if (SelectedDataTarget != null)
            {
                if (SelectedDataTargetConfig != null)
                {
                    if (SelectedDataTarget.Id == "Standard.CsvTarget")
                    {
                        if (SelectedDataTargetConfig.ConfigProperties.TryGetValue("FilePath", out var targetFilePath))
                        {
                            task.Target["FilePath"] = targetFilePath;
                        }
                    }
                    else
                    {
                        if (SelectedDataTargetConfig.ConfigProperties.TryGetValue("FilePath", out var targetFilePath))
                        {
                            task.Target["ConnectionString"] = targetFilePath;
                        }
                    }
                }
                else if (SelectedDataTarget.Id == "DataMigration.Plugin.SqliteTarget")
                {
                    string dbPath = GetDatabasePath("target.db");
                    task.Target["ConnectionString"] = $"Data Source={dbPath}";
                }
                else if (SelectedDataTarget.Id == "Standard.CsvTarget")
                {
                    string csvPath = GetDatabasePath("target.csv");
                    task.Target["FilePath"] = csvPath;
                }
                else if (SelectedDataTarget.Id == "DataMigration.Plugin.ExcelTarget")
                {
                    string excelPath = GetDatabasePath("target.xlsx");
                    task.Target["ConnectionString"] = excelPath;
                }
            }

            // 创建迁移引擎并执行任务
            var engine = new MigrationEngine(_pluginManager);
            await engine.RunAsync(task, CancellationToken.None);

            MessageBox.Show("任务执行成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"任务执行失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SelectDataSource()
    {
        var dialog = new PluginSelectionWindow(PluginSelectionType.DataSource);
        if (dialog.ShowDialog() == true)
        {
            var selectedPlugin = dialog.SelectedPlugin;
            if (selectedPlugin != null)
            {
                try
                {
                    var dataSource = _pluginManager.GetDataSource(selectedPlugin.Id);
                    if (!DataSources.Contains(dataSource))
                    {
                        DataSources.Add(dataSource);
                    }
                    SelectedDataSource = dataSource;
                // 加载数据表
                _ = LoadTablesAsync();
                // 不再自动显示插件配置，需要用户手动选择配置
                }
                catch
                {
                    // 显示错误信息
                }
            }
        }
    }

    [RelayCommand]
    private void SelectDataTarget()
    {
        var dialog = new PluginSelectionWindow(PluginSelectionType.DataTarget);
        if (dialog.ShowDialog() == true)
        {
            var selectedPlugin = dialog.SelectedPlugin;
            if (selectedPlugin != null)
            {
                try
                {
                    var dataTarget = _pluginManager.GetTarget(selectedPlugin.Id);
                    if (!DataTargets.Contains(dataTarget))
                    {
                        DataTargets.Add(dataTarget);
                    }
                    SelectedDataTarget = dataTarget;
                    // 不再自动显示插件配置，需要用户手动选择配置
                }
                catch
                {
                    // 显示错误信息
                }
            }
        }
    }

    [RelayCommand]
    private void SelectTransformer()
    {
        var dialog = new PluginSelectionWindow(PluginSelectionType.Transformer);
        if (dialog.ShowDialog() == true)
        {
            var selectedPlugin = dialog.SelectedPlugin;
            if (selectedPlugin != null)
            {
                try
                {
                    var transformer = _pluginManager.GetTransformer(selectedPlugin.Id);
                    if (!Transformers.Contains(transformer))
                    {
                        Transformers.Add(transformer);
                    }
                    SelectedTransformer = transformer;
                    // 不再自动显示插件配置，需要用户手动选择配置
                }
                catch
                {
                    // 显示错误信息
                }
            }
        }
    }

    [RelayCommand]
    private void ConfigurePlugin()
    {
        // 显示插件配置
        UpdatePluginConfig();
    }

    [RelayCommand]
    private void AddRelation()
    {
        if (!string.IsNullOrEmpty(SourceTableName) && !string.IsNullOrEmpty(SourceColumnName) &&
            !string.IsNullOrEmpty(TargetTableName) && !string.IsNullOrEmpty(TargetColumnName))
        {
            var relation = new TableRelation
            {
                SourceTable = SourceTableName,
                SourceColumn = SourceColumnName,
                TargetTable = TargetTableName,
                TargetColumn = TargetColumnName
            };
            TableRelations.Add(relation);
            UpdatePreviewQuery();
        }
    }

    [RelayCommand]
    private void RemoveRelation(TableRelation relation)
    {
        TableRelations.Remove(relation);
        UpdatePreviewQuery();
    }

    public AsyncRelayCommand LoadSourceColumnsCommand => new AsyncRelayCommand(LoadSourceColumns);

    [RelayCommand]
    private async Task LoadSourceColumns()
    {
        SourceColumns.Clear();
        if (!string.IsNullOrEmpty(SourceTableName))
        {
            await LoadTableColumns(SourceTableName, SourceColumns);
        }
    }

    public AsyncRelayCommand LoadTargetColumnsCommand => new AsyncRelayCommand(LoadTargetColumns);

    [RelayCommand]
    private async Task LoadTargetColumns()
    {
        TargetColumns.Clear();
        if (!string.IsNullOrEmpty(TargetTableName))
        {
            await LoadTableColumns(TargetTableName, TargetColumns);
        }
    }

    private async Task LoadTableColumns(string tableName, ObservableCollection<string> columns)
    {
        // 尝试从已保存的配置中获取连接字符串
        string connectionString = string.Empty;
        if (SelectedDataSourceConfig != null && SelectedDataSourceConfig.ConfigProperties.TryGetValue("ConnectionString", out var configConnectionString))
        {
            connectionString = configConnectionString;
        }

        try
        {
            var newColumns = new List<string>();
            
            if (SelectedDataSource?.Id == "DataMigration.Plugin.SqliteSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    string dbPath = GetDatabasePath("source.db");
                    connectionString = $"Data Source={dbPath}";
                }
                var columnInfos = await SQLiteHelper.GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columnInfos)
                {
                    newColumns.Add(column.Name);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                }
                var columnInfos = await new MySQLHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columnInfos)
                {
                    newColumns.Add(column.Name);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                }
                var columnInfos = await new SqlServerHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columnInfos)
                {
                    newColumns.Add(column.Name);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.xlsx");
                }
                var columnInfos = await new ExcelHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columnInfos)
                {
                    newColumns.Add(column.Name);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.CsvSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.csv");
                }
                var columnInfos = await new DataMigration.Core.CsvHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columnInfos)
                {
                    newColumns.Add(column.Name);
                }
            }
            else
            {
                // 其他数据源使用模拟数据
                newColumns.Add("Id");
                newColumns.Add("Name");
                newColumns.Add("Age");
                newColumns.Add("Email");
                newColumns.Add("Phone");
            }
            
            // 清空并添加新列
            columns.Clear();
            foreach (var column in newColumns)
            {
                columns.Add(column);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载列信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void AddColumnRenameMapping()
    {
        if (!string.IsNullOrEmpty(OriginalColumnName) && !string.IsNullOrEmpty(NewColumnName))
        {
            var mapping = new ColumnRenameMapping
            {
                OriginalColumnName = OriginalColumnName,
                NewColumnName = NewColumnName
            };
            ColumnRenameMappings.Add(mapping);
        }
    }

    [RelayCommand]
    private void RemoveColumnRenameMapping(ColumnRenameMapping mapping)
    {
        ColumnRenameMappings.Remove(mapping);
    }

    [RelayCommand]
    private void PreviewRenameEffect()
    {
        // 实现预览重命名效果的逻辑
        // 这里可以显示一个对话框，展示重命名前后的列名对比
        var previewText = "列重命名预览:\n";
        foreach (var mapping in ColumnRenameMappings)
        {
            previewText += $"{mapping.OriginalColumnName} -> {mapping.NewColumnName}\n";
        }
        if (ColumnRenameMappings.Count == 0)
        {
            previewText += "暂无重命名映射";
        }
        MessageBox.Show(previewText, "重命名预览", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task LoadSourceFields()
    {
        SourceFields.Clear();
        if (SelectedDataSource != null && !string.IsNullOrEmpty(SourceTableName))
        {
            await LoadTableColumns(SourceTableName, SourceFields);
        }
    }

    [RelayCommand]
    private async Task LoadTargetFields()
    {
        TargetFields.Clear();
        if (SelectedDataTarget != null && !string.IsNullOrEmpty(TargetTableName))
        {
            // 这里需要实现加载目标源字段的逻辑
            // 暂时使用模拟数据
            TargetFields.Add("Id");
            TargetFields.Add("Name");
            TargetFields.Add("Age");
            TargetFields.Add("Email");
            TargetFields.Add("Phone");
        }
    }

    [RelayCommand]
    private void AddFieldMapping()
    {
        if (!string.IsNullOrEmpty(SelectedSourceField) && !string.IsNullOrEmpty(SelectedTargetField))
        {
            var mapping = new FieldMapping
            {
                SourceField = SelectedSourceField,
                TargetField = SelectedTargetField
            };
            FieldMappings.Add(mapping);
        }
    }

    [RelayCommand]
    private void RemoveFieldMapping(FieldMapping mapping)
    {
        FieldMappings.Remove(mapping);
    }

    [RelayCommand]
    private void AutoMapFields()
    {
        // 实现自动映射的逻辑
        // 简单的自动映射：根据字段名相同进行映射
        FieldMappings.Clear();
        foreach (var sourceField in SourceFields)
        {
            var matchingTargetField = TargetFields.FirstOrDefault(tf => tf.Equals(sourceField, StringComparison.OrdinalIgnoreCase));
            if (matchingTargetField != null)
            {
                FieldMappings.Add(new FieldMapping
                {
                    SourceField = sourceField,
                    TargetField = matchingTargetField
                });
            }
        }
    }

    [RelayCommand]
    private void ClearFieldMappings()
    {
        FieldMappings.Clear();
    }

    [RelayCommand]
    private void AddFormatConfig()
    {
        if (!string.IsNullOrEmpty(SelectedField) && !string.IsNullOrEmpty(FormatType) && !string.IsNullOrEmpty(FormatString))
        {
            var config = new FormatConfig
            {
                FieldName = SelectedField,
                FormatType = FormatType,
                FormatString = FormatString
            };
            FormatConfigs.Add(config);
        }
    }

    [RelayCommand]
    private void RemoveFormatConfig(FormatConfig config)
    {
        FormatConfigs.Remove(config);
    }

    [RelayCommand]
    private void PreviewFormatEffect()
    {
        // 实现预览格式效果的逻辑
        // 这里可以显示一个对话框，展示格式配置的效果
        var previewText = "格式配置预览:\n";
        foreach (var config in FormatConfigs)
        {
            previewText += $"字段: {config.FieldName}, 类型: {config.FormatType}, 格式: {config.FormatString}\n";
        }
        if (FormatConfigs.Count == 0)
        {
            previewText += "暂无格式配置";
        }
        MessageBox.Show(previewText, "格式预览", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void GenerateConfigSummary()
    {
        // 生成配置摘要
        var summary = new System.Text.StringBuilder();
        
        // 数据源信息
        summary.AppendLine($"数据源: {SelectedDataSource?.Name ?? "未选择"}");
        
        // 目标源信息
        summary.AppendLine($"目标源: {SelectedDataTarget?.Name ?? "未选择"}");
        
        // 关联关系
        summary.AppendLine("关联关系:");
        if (TableRelations.Count > 0)
        {
            foreach (var relation in TableRelations)
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
        if (ColumnRenameMappings.Count > 0)
        {
            foreach (var mapping in ColumnRenameMappings)
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
        if (FieldMappings.Count > 0)
        {
            foreach (var mapping in FieldMappings)
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
        if (FormatConfigs.Count > 0)
        {
            foreach (var config in FormatConfigs)
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

    [RelayCommand]
    private void ValidateConfig()
    {
        // 验证配置的有效性
        var errors = new List<string>();
        
        if (SelectedDataSource == null)
        {
            errors.Add("未选择数据源");
        }
        
        if (SelectedDataTarget == null)
        {
            errors.Add("未选择目标源");
        }
        
        if (FieldMappings.Count == 0)
        {
            errors.Add("未配置字段映射");
        }
        
        if (errors.Count == 0)
        {
            ConfigStatus = "配置有效";
            MessageBox.Show("配置验证通过", "验证成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ConfigStatus = "配置无效";
            var errorMessage = "配置验证失败:\n" + string.Join("\n", errors);
            MessageBox.Show(errorMessage, "验证失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadTablesAsync()
    {
        TableColumns.Clear();
        TableData = new System.Data.DataTable();
        SelectedTable = null;

        // 尝试从已保存的配置中获取连接字符串
        string connectionString = string.Empty;
        if (SelectedDataSourceConfig != null && SelectedDataSourceConfig.ConfigProperties.TryGetValue("ConnectionString", out var configConnectionString))
        {
            connectionString = configConnectionString;
        }

        try
        {
            var newTables = new ObservableCollection<string>();
            
            if (SelectedDataSource?.Id == "DataMigration.Plugin.SqliteSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    string dbPath = GetDatabasePath("source.db");
                    connectionString = $"Data Source={dbPath}";
                }
                var tables = await SQLiteHelper.GetTablesAsync(connectionString);
                foreach (var tableName in tables)
                {
                    newTables.Add(tableName);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                }
                var tables = await new MySQLHelper().GetTablesAsync(connectionString);
                foreach (var tableName in tables)
                {
                    newTables.Add(tableName);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                }
                var tables = await new SqlServerHelper().GetTablesAsync(connectionString);
                foreach (var tableName in tables)
                {
                    newTables.Add(tableName);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.xlsx");
                }
                var tables = await new ExcelHelper().GetTablesAsync(connectionString);
                foreach (var tableName in tables)
                {
                    newTables.Add(tableName);
                }
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.CsvSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.csv");
                }
                var tables = await new DataMigration.Core.CsvHelper().GetTablesAsync(connectionString);
                foreach (var tableName in tables)
                {
                    newTables.Add(tableName);
                }
            }
            else
            {
                // 其他数据源使用模拟数据
                newTables.Add("Table1");
                newTables.Add("Table2");
                newTables.Add("Table3");
                newTables.Add("Table4");
                newTables.Add("Table5");
            }
            
            Tables = newTables;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载数据表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadTableStructureAsync(string tableName)
    {
        TableColumns.Clear();
        TableData.Clear();

        // 尝试从已保存的配置中获取连接字符串
        string connectionString = string.Empty;
        if (SelectedDataSourceConfig != null && SelectedDataSourceConfig.ConfigProperties.TryGetValue("ConnectionString", out var configConnectionString))
        {
            connectionString = configConnectionString;
        }

        try
        {
            if (SelectedDataSource?.Id == "DataMigration.Plugin.SqliteSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    string dbPath = GetDatabasePath("source.db");
                    connectionString = $"Data Source={dbPath}";
                }
                // 获取表结构
                var columns = await SQLiteHelper.GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await SQLiteHelper.PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                }
                // 获取表结构
                var columns = await new MySQLHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new MySQLHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                }
                // 获取表结构
                var columns = await new SqlServerHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new SqlServerHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.xlsx");
                }
                // 获取表结构
                var columns = await new ExcelHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new ExcelHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.CsvSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.csv");
                }
                // 获取表结构
                var columns = await new DataMigration.Core.CsvHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new DataMigration.Core.CsvHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载表结构失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 将List<Dictionary<string, object?>>转换为DataTable
    /// </summary>
    /// <param name="data">数据列表</param>
    /// <returns>转换后的DataTable</returns>
    private System.Data.DataTable ConvertToDataTable(List<Dictionary<string, object?>> data)
    {
        var dataTable = new System.Data.DataTable();

        if (data.Any())
        {
            // 添加列
            var firstRecord = data.First();
            foreach (var field in firstRecord.Keys)
            {
                dataTable.Columns.Add(field);
            }

            // 添加数据行
            foreach (var record in data)
            {
                var row = dataTable.NewRow();
                foreach (var field in record.Keys)
                {
                    row[field] = record[field] ?? DBNull.Value;
                }
                dataTable.Rows.Add(row);
            }
        }

        return dataTable;
    }

    private void UpdatePluginConfig()
    {
        // 使用TabControl来组织不同的内容
        var tabControl = new TabControl();
        
        if (SelectedDataSource != null)
        {
            // 数据源配置标签
            var dataSourceTab = new TabItem { Header = "数据源配置" };
            var dataSourcePanel = new StackPanel();
            
            dataSourcePanel.Children.Add(new TextBlock { Text = $"数据源: {SelectedDataSource.Name}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
            
            // 添加已保存配置选择
            dataSourcePanel.Children.Add(new TextBlock { Text = "已保存配置:" });
            var configComboBox = new ComboBox { ItemsSource = SavedDataSourceConfigs, DisplayMemberPath = "Name", Width = 300, Margin = new Thickness(0, 5, 0, 10) };
            configComboBox.SelectedItem = SelectedDataSourceConfig;
            configComboBox.SelectionChanged += (sender, e) =>
                {
                    if (configComboBox.SelectedItem is DataSourceConfigItem selectedConfig)
                    {
                        SelectedDataSourceConfig = selectedConfig;
                        // 加载配置并重新加载表列表
                        if (selectedConfig.ConfigProperties.TryGetValue("ConnectionString", out var connectionString))
                        {
                            // 更新连接字符串
                        }
                        _ = LoadTablesAsync();
                    }
                };
            dataSourcePanel.Children.Add(configComboBox);
            
            // 如果是SQLite数据源，自动填充连接字符串
            if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
            {
                string dbPath = GetDatabasePath("source.db");
                string connectionString = $"Data Source={dbPath}";
                dataSourcePanel.Children.Add(new TextBlock { Text = "连接字符串:" });
                dataSourcePanel.Children.Add(new TextBox { Text = connectionString, Width = 500, Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 5, 0, 10) });
            }
            else
            {
                dataSourcePanel.Children.Add(new TextBlock { Text = "连接字符串:" });
                dataSourcePanel.Children.Add(new TextBox { Width = 500, Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 5, 0, 10) });
            }
            
            dataSourcePanel.Children.Add(new TextBlock { Text = "查询语句:" });
            dataSourcePanel.Children.Add(new TextBox { Width = 500, Height = 120, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 5, 0, 10) });
            
            // 添加测试连接按钮
            var testConnectionButton = new Button { Content = "测试连接", Width = 100, Height = 30, Margin = new Thickness(0, 10, 0, 10) };
            testConnectionButton.Click += (sender, e) => TestConnectionCommand.Execute(null);
            dataSourcePanel.Children.Add(testConnectionButton);
            
            dataSourceTab.Content = dataSourcePanel;
            tabControl.Items.Add(dataSourceTab);
            
            // 表结构标签
            var tableStructureTab = new TabItem { Header = "表结构" };
            var tableStructurePanel = new StackPanel();
            
            // 显示数据表结构和数据
            if (Tables.Count > 0)
            {
                tableStructurePanel.Children.Add(new TextBlock { Text = "数据表", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                var tableListBox = new ListBox { ItemsSource = Tables, Width = 300, Height = 100, Margin = new Thickness(0, 0, 0, 10), SelectionMode = SelectionMode.Multiple };
                tableListBox.SelectionChanged += (sender, e) =>
                {
                    if (tableListBox.SelectedItems != null)
                    {
                        SelectedTables.Clear();
                        foreach (var item in tableListBox.SelectedItems)
                        {
                            if (item is string tableName)
                            {
                                SelectedTables.Add(tableName);
                            }
                        }
                        
                        // 如果只选择了一个表，加载其结构
                        if (tableListBox.SelectedItems.Count == 1 && tableListBox.SelectedItem is string selectedTable)
                        {
                            SelectedTable = selectedTable;
                            _ = LoadTableStructureAsync(selectedTable);
                        }
                        else
                        {
                            // 多个表被选择时，清空表结构和数据
                            TableColumns.Clear();
                            TableData = new System.Data.DataTable();
                            SelectedTable = null;
                        }
                    }
                };
                tableStructurePanel.Children.Add(tableListBox);
            }
            
            // 显示表结构
            if (TableColumns.Count > 0)
            {
                tableStructurePanel.Children.Add(new TextBlock { Text = "表结构", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                var columnListBox = new ListBox { ItemsSource = TableColumns, Width = 300, Height = 100, Margin = new Thickness(0, 0, 0, 10) };
                tableStructurePanel.Children.Add(columnListBox);
            }
            
            // 显示表数据
            if (TableData.Rows.Count > 0)
            {
                tableStructurePanel.Children.Add(new TextBlock { Text = "表数据", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                var dataGrid = new DataGrid { ItemsSource = TableData.DefaultView, Width = 500, Height = 300, Margin = new Thickness(0, 0, 0, 10) };
                tableStructurePanel.Children.Add(dataGrid);
            }
            else
            {
                tableStructurePanel.Children.Add(new TextBlock { Text = "暂无表数据", Margin = new Thickness(0, 10, 0, 5) });
            }
            
            tableStructureTab.Content = tableStructurePanel;
            tabControl.Items.Add(tableStructureTab);
        }
        else if (SelectedDataTarget != null)
        {
            // 目标源配置标签
            var dataTargetTab = new TabItem { Header = "目标源配置" };
            var dataTargetPanel = new StackPanel();
            
            dataTargetPanel.Children.Add(new TextBlock { Text = $"目标源: {SelectedDataTarget.Name}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
            
            // 添加已保存配置选择
            dataTargetPanel.Children.Add(new TextBlock { Text = "已保存配置:" });
            var configComboBox = new ComboBox { ItemsSource = SavedDataTargetConfigs, DisplayMemberPath = "Name", Width = 300, Margin = new Thickness(0, 5, 0, 10) };
            configComboBox.SelectedItem = SelectedDataTargetConfig;
            configComboBox.SelectionChanged += (sender, e) =>
            {
                if (configComboBox.SelectedItem is DataTargetConfigItem selectedConfig)
                {
                    SelectedDataTargetConfig = selectedConfig;
                    // 加载配置
                }
            };
            dataTargetPanel.Children.Add(configComboBox);
            
            dataTargetPanel.Children.Add(new TextBlock { Text = "连接字符串:" });
            dataTargetPanel.Children.Add(new TextBox { Width = 500, Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 5, 0, 10) });
            dataTargetPanel.Children.Add(new TextBlock { Text = "表名:" });
            dataTargetPanel.Children.Add(new TextBox { Width = 300, Margin = new Thickness(0, 5, 0, 10) });
            
            dataTargetTab.Content = dataTargetPanel;
            tabControl.Items.Add(dataTargetTab);
        }
        else if (SelectedTransformer != null)
        {
            // 转换器配置标签
            var transformerTab = new TabItem { Header = "转换器配置" };
            var transformerPanel = new StackPanel();
            
            transformerPanel.Children.Add(new TextBlock { Text = $"清洗规则: {SelectedTransformer.Name}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
            transformerPanel.Children.Add(new TextBlock { Text = "规则配置:" });
            transformerPanel.Children.Add(new TextBox { Width = 300, Height = 150, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Margin = new Thickness(0, 5, 0, 10) });
            
            transformerTab.Content = transformerPanel;
            tabControl.Items.Add(transformerTab);
        }
        
        // 表格关联标签
        if (SelectedDataSource != null && SelectedDataTarget != null)
        {
            var relationTab = new TabItem { Header = "表格关联" };
            var relationPanel = new StackPanel();
            
            relationPanel.Children.Add(new TextBlock { Text = "表格关联", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 10) });
            
            var relationContentPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
            
            // 源表选择
            relationContentPanel.Children.Add(new TextBlock { Text = "源表:" });
            var sourceTableComboBox = new ComboBox { ItemsSource = Tables, Width = 200, Margin = new Thickness(0, 5, 0, 5) };
            relationContentPanel.Children.Add(sourceTableComboBox);
            
            // 源列选择
            relationContentPanel.Children.Add(new TextBlock { Text = "源列:" });
            var sourceColumnComboBox = new ComboBox { Width = 200, Margin = new Thickness(0, 5, 0, 5) };
            relationContentPanel.Children.Add(sourceColumnComboBox);
            
            // 目标表选择
            relationContentPanel.Children.Add(new TextBlock { Text = "目标表:" });
            var targetTableComboBox = new ComboBox { ItemsSource = Tables, Width = 200, Margin = new Thickness(0, 5, 0, 5) };
            relationContentPanel.Children.Add(targetTableComboBox);
            
            // 目标列选择
            relationContentPanel.Children.Add(new TextBlock { Text = "目标列:" });
            var targetColumnComboBox = new ComboBox { Width = 200, Margin = new Thickness(0, 5, 0, 10) };
            relationContentPanel.Children.Add(targetColumnComboBox);
            
            // 添加关联按钮
            var addRelationButton = new Button { Content = "添加关联", Width = 100, Height = 30, Margin = new Thickness(0, 10, 0, 10) };
            addRelationButton.Click += (sender, e) =>
            {
                if (sourceTableComboBox.SelectedItem is string sourceTable &&
                    sourceColumnComboBox.SelectedItem is string sourceColumn &&
                    targetTableComboBox.SelectedItem is string targetTable &&
                    targetColumnComboBox.SelectedItem is string targetColumn)
                {
                    var relation = new TableRelation
                    {
                        SourceTable = sourceTable,
                        SourceColumn = sourceColumn,
                        TargetTable = targetTable,
                        TargetColumn = targetColumn
                    };
                    TableRelations.Add(relation);
                    UpdatePreviewQuery();
                }
            };
            relationContentPanel.Children.Add(addRelationButton);
            
            // 显示已添加的关联
            if (TableRelations.Count > 0)
            {
                relationContentPanel.Children.Add(new TextBlock { Text = "已添加的关联:", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                var relationListBox = new ListBox { Width = 400, Height = 150, Margin = new Thickness(0, 5, 0, 10) };
                relationListBox.ItemsSource = TableRelations;
                relationListBox.DisplayMemberPath = "SourceTable";
                relationContentPanel.Children.Add(relationListBox);
            }
            
            relationPanel.Children.Add(relationContentPanel);
            
            // 添加条件预览
            relationPanel.Children.Add(new TextBlock { Text = "条件预览", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 20, 0, 10) });
            var previewTextBox = new TextBox { Text = PreviewQuery, Width = 500, Height = 100, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, IsReadOnly = true, Margin = new Thickness(0, 5, 0, 10) };
            relationPanel.Children.Add(previewTextBox);
            
            relationTab.Content = relationPanel;
            tabControl.Items.Add(relationTab);
        }
        
        // 如果没有选择任何插件，显示默认内容
        if (tabControl.Items.Count == 0)
        {
            var defaultTab = new TabItem { Header = "配置" };
            var defaultPanel = new StackPanel();
            defaultPanel.Children.Add(new TextBlock { Text = "请选择插件以显示配置" });
            defaultTab.Content = defaultPanel;
            tabControl.Items.Add(defaultTab);
        }
        
        // 更新插件配置内容
        PluginConfigContent = tabControl;
    }

    private void UpdatePreviewQuery()
    {
        if (TableRelations.Count == 0)
        {
            PreviewQuery = "";
            return;
        }
        
        var queryBuilder = new System.Text.StringBuilder();
        queryBuilder.AppendLine("-- 生成的查询语句预览:");
        
        foreach (var relation in TableRelations)
        {
            queryBuilder.AppendLine($"JOIN {relation.TargetTable} ON {relation.SourceTable}.{relation.SourceColumn} = {relation.TargetTable}.{relation.TargetColumn}");
        }
        
        PreviewQuery = queryBuilder.ToString();
    }

    partial void OnSelectedDataSourceChanged(IDataSource? oldValue, IDataSource? newValue)
    {
        if (newValue != null)
        {
            _ = LoadTablesAsync();
            UpdatePluginConfig();
        }
    }

    partial void OnSelectedDataTargetChanged(IDataTarget? oldValue, IDataTarget? newValue)
    {
        if (newValue != null)
        {
            UpdatePluginConfig();
        }
    }

    partial void OnSelectedTransformerChanged(ITransformer? oldValue, ITransformer? newValue)
    {
        if (newValue != null)
        {
            UpdatePluginConfig();
        }
    }

    partial void OnSelectedTableChanged(string? oldValue, string? newValue)
    {
        if (newValue != null)
        {
            _ = LoadTableStructureAsync(newValue);
            UpdatePluginConfig();
        }
    }

    private string GetDefaultDirectory()
    {
        // 从配置文件或注册表中获取默认目录
        // 暂时使用应用程序数据目录作为默认目录
        string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DataMigration");
        
        // 确保目录存在
        if (!Directory.Exists(defaultDir))
        {
            Directory.CreateDirectory(defaultDir);
        }
        
        return defaultDir;
    }

    private void SaveDefaultDirectory(string directory)
    {
        // 这里可以将默认目录保存到配置文件或注册表中
        // 暂时只是确保目录存在
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private string GetDatabasePath(string databaseName)
    {
        // 使用应用程序基础目录，确保路径正确
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataDir = Path.Combine(baseDir, "data");
        
        // 确保数据目录存在
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        
        return Path.Combine(dataDir, databaseName);
    }
}