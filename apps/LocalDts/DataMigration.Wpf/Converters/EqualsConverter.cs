using System;
using System.Globalization;
using System.Windows.Data;

namespace DataMigration.Wpf.Converters;

/// <summary>
/// 比较两个值是否相等的转换器
/// </summary>
public class EqualsConverter : IMultiValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    /// <param name="values">要转换的值数组</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>如果两个值相等，则返回 true；否则返回 false</returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2)
        {
            return false;
        }

        var value1 = values[0];
        var value2 = values[1];

        if (value1 == null || value2 == null)
        {
            return value1 == value2;
        }

        return value1.Equals(value2);
    }

    /// <summary>
    /// 转换回值
    /// </summary>
    /// <param name="value">要转换回的值</param>
    /// <param name="targetTypes">目标类型数组</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>转换回的值数组</returns>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}