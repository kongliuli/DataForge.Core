using DataMigration.Core;
using DataMigration.Contracts;
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace DataMigration.Wpf.ViewModel;

public partial class TaskExecutionViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private MigrationTask? _task;
    private CancellationTokenSource? _cts;
    private readonly object _logLock = new object();

    [ObservableProperty]
    private string _taskStatus = "就绪";

    [ObservableProperty]
    private string _log = "";

    [ObservableProperty]
    private string _taskDetails = "";

    [ObservableProperty]
    private int _progress = 0;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private long _recordsProcessed = 0;

    [ObservableProperty]
    private long _recordsTotal = 0;

    public TaskExecutionViewModel(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    [RelayCommand]
    private void Load()
    {
        // 加载任务配置
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "加载任务配置"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var configJson = File.ReadAllText(openFileDialog.FileName);
                _task = JsonSerializer.Deserialize<MigrationTask>(configJson);
                if (_task != null)
                {
                    TaskStatus = "已加载任务: " + _task.TaskId;
                    UpdateLog("任务已加载成功");
                    TaskDetails = GenerateTaskDetails(_task);
                }
                else
                {
                    UpdateLog("任务配置文件格式错误");
                }
            }
            catch (Exception ex)
            {
                UpdateLog($"加载任务失败: {ex.Message}");
            }
        }
    }

    private string GenerateTaskDetails(MigrationTask task)
    {
        var details = new System.Text.StringBuilder();
        
        // 数据源信息
        details.AppendLine("=== 数据源信息 ===");
        details.AppendLine($"插件: {task.Source.ComponentId}");
        details.AppendLine($"描述: {task.Source.Description ?? "无"}");
        details.AppendLine("连接信息:");
        foreach (var kvp in task.Source)
        {
            if (kvp.Key.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase))
            {
                details.AppendLine($"  {kvp.Key}: {MaskConnectionString(kvp.Value)}");
            }
            else
            {
                details.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        // 转换器信息
        if (task.Transforms.Count > 0)
        {
            details.AppendLine();
            details.AppendLine("=== 转换器信息 ===");
            for (int i = 0; i < task.Transforms.Count; i++)
            {
                var transform = task.Transforms[i];
                details.AppendLine($"转换器 {i + 1}: {transform.ComponentId}");
                details.AppendLine($"  描述: {transform.Description ?? "无"}");
                foreach (var kvp in transform)
                {
                    details.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
                details.AppendLine();
            }
        }
        
        // 目标源信息
        details.AppendLine("=== 目标源信息 ===");
        details.AppendLine($"插件: {task.Target.ComponentId}");
        details.AppendLine($"描述: {task.Target.Description ?? "无"}");
        details.AppendLine("连接信息:");
        foreach (var kvp in task.Target)
        {
            if (kvp.Key.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase))
            {
                details.AppendLine($"  {kvp.Key}: {MaskConnectionString(kvp.Value)}");
            }
            else
            {
                details.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        // 执行选项
        details.AppendLine();
        details.AppendLine("=== 执行选项 ===");
        details.AppendLine($"批处理大小: {task.Options.BatchSize}");
        details.AppendLine($"启用断点续传: {task.Options.EnableCheckpoint}");
        details.AppendLine($"最大并行度: {task.Options.MaxDegreeOfParallelism}");
        
        return details.ToString();
    }

    private string MaskConnectionString(string connectionString)
    {
        // 简单的连接字符串脱敏处理
        if (string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }
        
        // 脱敏密码部分
        var masked = connectionString;
        var passwordPatterns = new[] { "Password=", "pwd=", "Password=", "PWD=" };
        
        foreach (var pattern in passwordPatterns)
        {
            int startIndex = masked.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                startIndex += pattern.Length;
                int endIndex = masked.IndexOf(';', startIndex);
                if (endIndex < 0)
                {
                    endIndex = masked.Length;
                }
                
                var password = masked.Substring(startIndex, endIndex - startIndex);
                var maskedPassword = new string('*', Math.Min(password.Length, 8));
                masked = masked.Substring(0, startIndex) + maskedPassword + masked.Substring(endIndex);
            }
        }
        
        return masked;
    }

    [RelayCommand]
    private async Task Start()
    {
        if (_task == null)
        {
            UpdateLog("请先加载任务配置");
            return;
        }

        // 重置进度相关属性
        Progress = 0;
        ProgressText = "0%";
        RecordsProcessed = 0;
        RecordsTotal = 0;

        TaskStatus = "执行中";
        UpdateLog("开始执行任务...");

        _cts = new CancellationTokenSource();

        try
        {
            var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            _pluginManager.LoadPlugins(pluginsDirectory);

            var engine = new MigrationEngine(_pluginManager);
            
            // 模拟进度更新（实际项目中应该从引擎获取真实进度）
            var progressTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested && TaskStatus == "执行中")
                {
                    await Task.Delay(1000, _cts.Token);
                    
                    // 模拟进度增长
                    if (Progress < 90)
                    {
                        Progress += 5;
                        ProgressText = $"{Progress}%";
                    }
                }
            }, _cts.Token);

            await engine.RunAsync(_task, _cts.Token);

            // 完成进度
            Progress = 100;
            ProgressText = "100%";
            
            TaskStatus = "执行完成";
            UpdateLog("任务执行完成");
        }
        catch (Exception ex)
        {
            TaskStatus = "执行失败";
            UpdateLog($"执行失败: {ex.Message}");
        }
        finally
        {
            _cts?.Cancel();
            _cts = null;
        }
    }

    /// <summary>
    /// 安全地更新日志，确保在UI线程上执行
    /// </summary>
    /// <param name="message">日志消息</param>
    private void UpdateLog(string message)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        if (dispatcher.CheckAccess())
        {
            lock (_logLock)
            {
                Log += $"\n{DateTime.Now:HH:mm:ss} - {message}";
            }
        }
        else
        {
            dispatcher.Invoke(() => UpdateLog(message));
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            TaskStatus = "已停止";
            UpdateLog("任务已停止");
        }
        else
        {
            UpdateLog("没有正在执行的任务");
        }
    }
}