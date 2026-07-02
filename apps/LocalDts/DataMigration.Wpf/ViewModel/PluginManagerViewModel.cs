using DataMigration.Core;
using DataMigration.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataMigration.Wpf.Services;

namespace DataMigration.Wpf.ViewModel;

public partial class PluginManagerViewModel : ObservableObject
{
    private readonly IPluginManager _pluginManager;
    private readonly IPluginService _pluginService;

    [ObservableProperty]
    private ObservableCollection<PluginInfo> _plugins = new();

    [ObservableProperty]
    private string _statusMessage = "";

    public PluginManagerViewModel(IPluginManager pluginManager, IPluginService pluginService)
    {
        _pluginManager = pluginManager;
        _pluginService = pluginService;
        RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            
            if (!Directory.Exists(pluginsDirectory))
            {
                StatusMessage = "插件目录不存在，正在创建...";
                Directory.CreateDirectory(pluginsDirectory);
            }
            
            StatusMessage = $"正在加载插件，目录: {pluginsDirectory}";
            
            _pluginManager.LoadPlugins(pluginsDirectory);
            
            Plugins.Clear();
            var components = _pluginManager.ListAllComponents();
            
            if (components.Any())
            {
                foreach (var plugin in components)
                {
                    Plugins.Add(plugin);
                }
                StatusMessage = $"成功加载 {Plugins.Count} 个插件组件";
            }
            else
            {
                StatusMessage = "未找到插件组件";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载插件时发生错误: {ex.Message}";
        }
    }
}