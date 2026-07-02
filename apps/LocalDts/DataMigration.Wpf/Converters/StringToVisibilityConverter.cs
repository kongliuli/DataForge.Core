using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataMigration.Wpf.Converters;

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? stringValue = value as string;
        string? parameterValue = parameter as string;
        if (!string.IsNullOrEmpty(parameterValue))
        {
            string[] parameters = parameterValue.Split(',');
            foreach (string param in parameters)
            {
                if (stringValue == param)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }
        return stringValue == parameterValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
