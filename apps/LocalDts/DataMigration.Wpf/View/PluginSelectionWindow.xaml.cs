using System.Windows;
using DataMigration.Contracts;
using DataMigration.Core;
using DataMigration.Wpf.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace DataMigration.Wpf;

/// <summary>
/// 插件选择窗口
/// </summary>
public partial class PluginSelectionWindow : Window
{
    /// <summary>
    /// 选中的插件信息
    /// </summary>
    public PluginInfo? SelectedPlugin { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginType">插件类型</param>
    public PluginSelectionWindow(PluginSelectionType pluginType)
    {
        InitializeComponent();

        // 从 DI 容器获取 ViewModel
        if (App.ServiceProvider != null)
        {
            var viewModel = App.ServiceProvider.GetRequiredService<PluginSelectionViewModel>();
            viewModel.Initialize(pluginType);
            DataContext = viewModel;
        }

        // 直接使用窗口的按钮事件
        // 确认按钮和取消按钮的事件处理在 XAML 中绑定
    }

    /// <summary>
    /// 确认按钮点击事件
    /// </summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as PluginSelectionViewModel;
        if (viewModel != null && viewModel.SelectedPlugin != null)
        {
            SelectedPlugin = viewModel.SelectedPlugin;
            DialogResult = true;
        }
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}