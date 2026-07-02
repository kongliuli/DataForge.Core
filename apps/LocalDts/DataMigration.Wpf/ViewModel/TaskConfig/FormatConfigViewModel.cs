using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace DataMigration.Wpf.ViewModel.TaskConfig;

/// <summary>
/// 格式配置视图模型
/// </summary>
public partial class FormatConfigViewModel : ObservableObject
{
    /// <summary>
    /// 选中的字段
    /// </summary>
    [ObservableProperty]
    private string _selectedField = "";

    /// <summary>
    /// 格式类型
    /// </summary>
    [ObservableProperty]
    private string _formatType = "日期";

    /// <summary>
    /// 格式字符串
    /// </summary>
    [ObservableProperty]
    private string _formatString = "";

    /// <summary>
    /// 格式配置列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FormatConfig> _formatConfigs = new();

    /// <summary>
    /// 添加格式配置
    /// </summary>
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

    /// <summary>
    /// 移除格式配置
    /// </summary>
    /// <param name="config">格式配置</param>
    [RelayCommand]
    private void RemoveFormatConfig(FormatConfig config)
    {
        FormatConfigs.Remove(config);
    }

    /// <summary>
    /// 预览格式效果
    /// </summary>
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
        System.Windows.MessageBox.Show(previewText, "格式预览", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 重置配置
    /// </summary>
    public void Reset()
    {
        SelectedField = "";
        FormatType = "日期";
        FormatString = "";
        FormatConfigs.Clear();
    }

    /// <summary>
    /// 格式配置
    /// </summary>
    public class FormatConfig
    {
        /// <summary>
        /// 字段名
        /// </summary>
        public string FieldName { get; set; } = "";

        /// <summary>
        /// 格式类型
        /// </summary>
        public string FormatType { get; set; } = "";

        /// <summary>
        /// 格式字符串
        /// </summary>
        public string FormatString { get; set; } = "";
    }
}
