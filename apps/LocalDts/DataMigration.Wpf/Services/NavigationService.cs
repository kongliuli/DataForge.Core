using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Collections.Generic;
using DataMigration.Wpf.ViewModel;

namespace DataMigration.Wpf.Services;

/// <summary>
/// 导航服务接口
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// 导航到指定页面
    /// </summary>
    /// <param name="pageName">页面名称</param>
    void Navigate(string pageName);

    /// <summary>
    /// 设置导航框架
    /// </summary>
    /// <param name="frame">导航框架</param>
    void SetFrame(Frame frame);

    /// <summary>
    /// 注册页面映射
    /// </summary>
    /// <param name="pageName">页面名称</param>
    /// <param name="pageType">页面类型</param>
    /// <param name="viewModelType">视图模型类型</param>
    void RegisterPage(string pageName, Type pageType, Type viewModelType);
}

/// <summary>
/// 导航服务实现
/// </summary>
public class NavigationService : INavigationService
{
    private Frame? _frame;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, PageMapping> _pageMappings = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        RegisterDefaultPages();
    }

    /// <summary>
    /// 注册默认页面
    /// </summary>
    private void RegisterDefaultPages()
    {
        RegisterPage("PluginManagerPage", typeof(PluginManagerPage), typeof(PluginManagerViewModel));
        RegisterPage("DataSourceConfigPage", typeof(DataSourceConfigPage), typeof(DataSourceConfigViewModel));
        RegisterPage("DataTargetConfigPage", typeof(DataTargetConfigPage), typeof(DataTargetConfigViewModel));
        RegisterPage("TransformerConfigPage", typeof(TransformerConfigPage), typeof(TransformerConfigViewModel));
        RegisterPage("TaskConfigPage", typeof(TaskConfigPage), typeof(TaskConfigViewModel));
        RegisterPage("TaskExecutionPage", typeof(TaskExecutionPage), typeof(TaskExecutionViewModel));
    }

    /// <summary>
    /// 设置导航框架
    /// </summary>
    /// <param name="frame">导航框架</param>
    public void SetFrame(Frame frame)
    {
        _frame = frame;
    }

    /// <summary>
    /// 注册页面映射
    /// </summary>
    /// <param name="pageName">页面名称</param>
    /// <param name="pageType">页面类型</param>
    /// <param name="viewModelType">视图模型类型</param>
    public void RegisterPage(string pageName, Type pageType, Type viewModelType)
    {
        if (!typeof(Page).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"{pageType.Name} must inherit from Page");
        }

        _pageMappings[pageName] = new PageMapping
        {
            PageType = pageType,
            ViewModelType = viewModelType
        };
    }

    /// <summary>
    /// 导航到指定页面
    /// </summary>
    /// <param name="pageName">页面名称</param>
    public void Navigate(string pageName)
    {
        try
        {
            if (_frame == null)
            {
                throw new InvalidOperationException("Navigation frame is not set");
            }

            if (!_pageMappings.TryGetValue(pageName, out var mapping))
            {
                throw new ArgumentException($"Unknown page name: {pageName}");
            }

            // 创建页面实例
            var page = (Page)Activator.CreateInstance(mapping.PageType) !;

            // 获取视图模型实例
            var viewModel = _serviceProvider.GetRequiredService(mapping.ViewModelType);
            page.DataContext = viewModel;

            // 执行导航
            _frame.Navigate(page);
        }
        catch (Exception ex)
        {
            // 记录错误日志
            Console.WriteLine($"Navigation error: {ex.Message}");
            
            // 显示错误消息
            MessageBox.Show($"导航失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 页面映射
    /// </summary>
    private class PageMapping
    {
        /// <summary>
        /// 页面类型
        /// </summary>
        public Type PageType { get; set; } = null!;

        /// <summary>
        /// 视图模型类型
        /// </summary>
        public Type ViewModelType { get; set; } = null!;
    }
}

