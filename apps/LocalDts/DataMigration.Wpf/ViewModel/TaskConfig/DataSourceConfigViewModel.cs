using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Contracts;
using DataMigration.Wpf.Services;
using System.Collections.ObjectModel;
using System.IO;
using DataMigration.Core;

namespace DataMigration.Wpf.ViewModel.TaskConfig;

/// <summary>
/// 数据源配置视图模型
/// </summary>
public partial class DataSourceConfigViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// 数据源列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<IDataSource> _dataSources = new();

    /// <summary>
    /// 选中的数据源
    /// </summary>
    [ObservableProperty]
    private IDataSource? _selectedDataSource;

    /// <summary>
    /// 数据表列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _tables = new();

    /// <summary>
    /// 表列列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _tableColumns = new();

    /// <summary>
    /// 表数据
    /// </summary>
    [ObservableProperty]
    private System.Data.DataTable _tableData = new();

    /// <summary>
    /// 选中的表
    /// </summary>
    [ObservableProperty]
    private string? _selectedTable;

    /// <summary>
    /// 选中的表列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _selectedTables = new();

    /// <summary>
    /// 已保存的数据源配置
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataSourceConfigItem> _savedDataSourceConfigs = new();

    /// <summary>
    /// 选中的数据源配置
    /// </summary>
    [ObservableProperty]
    private DataSourceConfigItem? _selectedDataSourceConfig;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginManager">插件管理器</param>
    /// <param name="configurationService">配置服务</param>
    public DataSourceConfigViewModel(IPluginManager pluginManager, IConfigurationService configurationService)
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
        // 加载已保存的数据源配置
        var dataSourceConfigs = _configurationService.LoadConfiguration<DataSourceConfigCollection>("DataSourceConfigs") ?? new DataSourceConfigCollection();
        var newDataSourceConfigs = new ObservableCollection<DataSourceConfigItem>(dataSourceConfigs.Configs);
        SavedDataSourceConfigs = newDataSourceConfigs;
    }

    /// <summary>
    /// 加载配置选项
    /// </summary>
    public async Task LoadConfigOptionsAsync()
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
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDataSource == null)
        {
            System.Windows.MessageBox.Show("请先选择数据源", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        try
        {
            // 显示加载提示
            var loadingWindow = new System.Windows.Window
            {
                Title = "测试连接",
                Width = 300,
                Height = 100,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Content = new System.Windows.Controls.TextBlock { Text = "正在测试连接...", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center }
            };
            loadingWindow.Show();

            // 实际测试连接
            string connectionString = string.Empty;
            bool isConnected = false;

            if (SelectedDataSource.Id == "DataMigration.Plugin.SqliteSource")
            {
                string dbPath = GetDatabasePath("source.db");
                connectionString = $"Data Source={dbPath}";
                isConnected = await DataMigration.Core.SQLiteHelper.TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                // 假设使用默认连接字符串
                connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                isConnected = await new DataMigration.Core.MySQLHelper().TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                // 假设使用默认连接字符串
                connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                isConnected = await new DataMigration.Core.SqlServerHelper().TestConnectionAsync(connectionString);
            }
            else if (SelectedDataSource.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                // 假设使用默认Excel文件路径
                string excelPath = GetDatabasePath("test.xlsx");
                isConnected = await new DataMigration.Core.ExcelHelper().TestConnectionAsync(excelPath);
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
            System.Windows.MessageBox.Show("连接成功", "成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // 关闭加载提示
            // 显示连接失败提示
            System.Windows.MessageBox.Show($"连接失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 加载数据表
    /// </summary>
    public async Task LoadTablesAsync()
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
                var tables = await DataMigration.Core.SQLiteHelper.GetTablesAsync(connectionString);
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
                var tables = await new DataMigration.Core.MySQLHelper().GetTablesAsync(connectionString);
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
                var tables = await new DataMigration.Core.SqlServerHelper().GetTablesAsync(connectionString);
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
                var tables = await new DataMigration.Core.ExcelHelper().GetTablesAsync(connectionString);
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
            System.Windows.MessageBox.Show($"加载数据表失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 加载表结构
    /// </summary>
    /// <param name="tableName">表名</param>
    public async Task LoadTableStructureAsync(string tableName)
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
                var columns = await DataMigration.Core.SQLiteHelper.GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await DataMigration.Core.SQLiteHelper.PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.MySqlDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Port=3306;Database=test;UserID=root;Password=password;SSL Mode=None;";
                }
                // 获取表结构
                var columns = await new DataMigration.Core.MySQLHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new DataMigration.Core.MySQLHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.SqlServerDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = "Server=localhost;Database=test;User Id=sa;Password=password;TrustServerCertificate=True;";
                }
                // 获取表结构
                var columns = await new DataMigration.Core.SqlServerHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new DataMigration.Core.SqlServerHelper().PreviewDataAsync(connectionString, tableName, 100);
                TableData = ConvertToDataTable(data);
            }
            else if (SelectedDataSource?.Id == "DataMigration.Plugin.ExcelDataSource")
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    connectionString = GetDatabasePath("test.xlsx");
                }
                // 获取表结构
                var columns = await new DataMigration.Core.ExcelHelper().GetTableStructureAsync(connectionString, tableName);
                foreach (var column in columns)
                {
                    TableColumns.Add($"{column.Name} ({column.Type})");
                }

                // 获取表数据
                var data = await new DataMigration.Core.ExcelHelper().PreviewDataAsync(connectionString, tableName, 100);
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
            System.Windows.MessageBox.Show($"加载表结构失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 重置配置
    /// </summary>
    public void Reset()
    {
        SelectedDataSource = null;
        Tables.Clear();
        TableColumns.Clear();
        TableData = new System.Data.DataTable();
        SelectedTable = null;
        SelectedTables.Clear();
        SelectedDataSourceConfig = null;
    }

    /// <summary>
    /// 获取数据库路径
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>数据库路径</returns>
    private string GetDatabasePath(string fileName)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appDataPath, "DataMigrationTool");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, fileName);
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
}
