using DataMigration.Core;
using DataMigration.Contracts;
using System;
using System.IO;
using System.Text.Json;

namespace DataMigration.Console;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // 解析命令行参数
            if (args.Length != 1)
            {
                System.Console.WriteLine("Usage: DataMigration.Console <task-config-file>");
                return;
            }

            var configFile = args[0];
            if (!File.Exists(configFile))
            {
                System.Console.WriteLine($"Config file not found: {configFile}");
                return;
            }

            // 读取任务配置
            var configJson = await File.ReadAllTextAsync(configFile);
            var task = JsonSerializer.Deserialize<MigrationTask>(configJson);

            if (task == null)
            {
                System.Console.WriteLine("Invalid task configuration");
                return;
            }

            // 初始化插件管理器
            var pluginManager = new PluginManager();
            var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            pluginManager.LoadPlugins(pluginsDirectory);

            // 显示加载的插件
            System.Console.WriteLine("Loaded plugins:");
            var plugins = pluginManager.ListAllComponents();
            foreach (var plugin in plugins)
            {
                System.Console.WriteLine($"  - {plugin.Type}: {plugin.Name} (ID: {plugin.Id}, Version: {plugin.Version})");
            }

            // 初始化执行引擎
            var engine = new MigrationEngine(pluginManager);

            // 执行迁移任务
            System.Console.WriteLine($"\nExecuting task: {task.TaskId}");
            var startTime = DateTime.Now;

            await engine.RunAsync(task, CancellationToken.None);

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            System.Console.WriteLine($"Task completed successfully in {duration.TotalSeconds:F2} seconds");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error: {ex.Message}");
            System.Console.WriteLine(ex.StackTrace);
        }
    }
}
