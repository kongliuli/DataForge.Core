using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace DataMigration.Wpf.ViewModel.TaskConfig;

/// <summary>
/// 字段映射配置视图模型
/// </summary>
public partial class FieldMappingViewModel : ObservableObject
{
    /// <summary>
    /// 表关联关系
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TableRelation> _tableRelations = new();

    /// <summary>
    /// 源表名
    /// </summary>
    [ObservableProperty]
    private string _sourceTableName = "";

    /// <summary>
    /// 源列名
    /// </summary>
    [ObservableProperty]
    private string _sourceColumnName = "";

    /// <summary>
    /// 目标表名
    /// </summary>
    [ObservableProperty]
    private string _targetTableName = "";

    /// <summary>
    /// 目标列名
    /// </summary>
    [ObservableProperty]
    private string _targetColumnName = "";

    /// <summary>
    /// 源列列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _sourceColumns = new();

    /// <summary>
    /// 目标列列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _targetColumns = new();

    /// <summary>
    /// 原始列名
    /// </summary>
    [ObservableProperty]
    private string _originalColumnName = "";

    /// <summary>
    /// 新列名
    /// </summary>
    [ObservableProperty]
    private string _newColumnName = "";

    /// <summary>
    /// 列重命名映射
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ColumnRenameMapping> _columnRenameMappings = new();

    /// <summary>
    /// 源字段列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _sourceFields = new();

    /// <summary>
    /// 目标字段列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _targetFields = new();

    /// <summary>
    /// 字段映射
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FieldMapping> _fieldMappings = new();

    /// <summary>
    /// 选中的源字段
    /// </summary>
    [ObservableProperty]
    private string _selectedSourceField = "";

    /// <summary>
    /// 选中的目标字段
    /// </summary>
    [ObservableProperty]
    private string _selectedTargetField = "";

    /// <summary>
    /// 预览查询
    /// </summary>
    [ObservableProperty]
    private string _previewQuery = "";

    /// <summary>
    /// 添加关联关系
    /// </summary>
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

    /// <summary>
    /// 移除关联关系
    /// </summary>
    /// <param name="relation">关联关系</param>
    [RelayCommand]
    private void RemoveRelation(TableRelation relation)
    {
        TableRelations.Remove(relation);
        UpdatePreviewQuery();
    }

    /// <summary>
    /// 添加列重命名映射
    /// </summary>
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

    /// <summary>
    /// 移除列重命名映射
    /// </summary>
    /// <param name="mapping">列重命名映射</param>
    [RelayCommand]
    private void RemoveColumnRenameMapping(ColumnRenameMapping mapping)
    {
        ColumnRenameMappings.Remove(mapping);
    }

    /// <summary>
    /// 预览重命名效果
    /// </summary>
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
        System.Windows.MessageBox.Show(previewText, "重命名预览", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// 添加字段映射
    /// </summary>
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

    /// <summary>
    /// 移除字段映射
    /// </summary>
    /// <param name="mapping">字段映射</param>
    [RelayCommand]
    private void RemoveFieldMapping(FieldMapping mapping)
    {
        FieldMappings.Remove(mapping);
    }

    /// <summary>
    /// 自动映射字段
    /// </summary>
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

    /// <summary>
    /// 清空字段映射
    /// </summary>
    [RelayCommand]
    private void ClearFieldMappings()
    {
        FieldMappings.Clear();
    }

    /// <summary>
    /// 更新预览查询
    /// </summary>
    private void UpdatePreviewQuery()
    {
        // 生成预览查询
        var query = new System.Text.StringBuilder();
        query.AppendLine("-- 预览查询");
        query.AppendLine("SELECT");
        
        // 添加字段映射
        if (FieldMappings.Count > 0)
        {
            for (int i = 0; i < FieldMappings.Count; i++)
            {
                var mapping = FieldMappings[i];
                query.AppendLine($"    {mapping.SourceField} AS {mapping.TargetField}{(i < FieldMappings.Count - 1 ? "," : "")}");
            }
        }
        else
        {
            query.AppendLine("    *");
        }
        
        query.AppendLine("FROM");
        query.AppendLine("    " + SourceTableName);
        
        // 添加关联关系
        if (TableRelations.Count > 0)
        {
            query.AppendLine("JOIN");
            for (int i = 0; i < TableRelations.Count; i++)
            {
                var relation = TableRelations[i];
                query.AppendLine($"    {relation.TargetTable} ON {relation.SourceTable}.{relation.SourceColumn} = {relation.TargetTable}.{relation.TargetColumn}");
            }
        }
        
        PreviewQuery = query.ToString();
    }

    /// <summary>
    /// 重置配置
    /// </summary>
    public void Reset()
    {
        TableRelations.Clear();
        SourceTableName = "";
        SourceColumnName = "";
        TargetTableName = "";
        TargetColumnName = "";
        SourceColumns.Clear();
        TargetColumns.Clear();
        OriginalColumnName = "";
        NewColumnName = "";
        ColumnRenameMappings.Clear();
        SourceFields.Clear();
        TargetFields.Clear();
        FieldMappings.Clear();
        SelectedSourceField = "";
        SelectedTargetField = "";
        PreviewQuery = "";
    }

    /// <summary>
    /// 表关联关系
    /// </summary>
    public class TableRelation
    {
        /// <summary>
        /// 源表
        /// </summary>
        public string SourceTable { get; set; } = "";

        /// <summary>
        /// 源列
        /// </summary>
        public string SourceColumn { get; set; } = "";

        /// <summary>
        /// 目标表
        /// </summary>
        public string TargetTable { get; set; } = "";

        /// <summary>
        /// 目标列
        /// </summary>
        public string TargetColumn { get; set; } = "";
    }

    /// <summary>
    /// 列重命名映射
    /// </summary>
    public class ColumnRenameMapping
    {
        /// <summary>
        /// 原始列名
        /// </summary>
        public string OriginalColumnName { get; set; } = "";

        /// <summary>
        /// 新列名
        /// </summary>
        public string NewColumnName { get; set; } = "";
    }

    /// <summary>
    /// 字段映射
    /// </summary>
    public class FieldMapping
    {
        /// <summary>
        /// 源字段
        /// </summary>
        public string SourceField { get; set; } = "";

        /// <summary>
        /// 目标字段
        /// </summary>
        public string TargetField { get; set; } = "";
    }
}
