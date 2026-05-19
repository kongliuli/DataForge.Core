using System;

namespace DataForge.Core.Core.Infrastructure;

public class DefaultTypeConverter : ITypeConverter
{
    public bool CanConvert(Type sourceType, Type targetType)
    {
        if (sourceType == targetType)
            return true;

        var underlyingSourceType = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var underlyingTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingSourceType == underlyingTargetType)
            return true;

        if (IsNumericType(underlyingSourceType) && IsNumericType(underlyingTargetType))
            return true;

        if (underlyingSourceType == typeof(string))
        {
            if (IsNumericType(underlyingTargetType))
                return true;
            if (underlyingTargetType == typeof(DateTime) || underlyingTargetType == typeof(DateTimeOffset))
                return true;
            if (underlyingTargetType == typeof(bool))
                return true;
            if (underlyingTargetType.IsEnum)
                return true;
        }

        if (underlyingTargetType == typeof(string))
            return true;

        if (underlyingTargetType.IsEnum && (underlyingSourceType.IsEnum || IsNumericType(underlyingSourceType)))
            return true;

        try
        {
            var dummy = underlyingSourceType.IsValueType
                ? Activator.CreateInstance(underlyingSourceType)
                : null;
            System.Convert.ChangeType(dummy, underlyingTargetType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public object? Convert(object? value, Type targetType)
    {
        if (value == null)
        {
            if (Nullable.GetUnderlyingType(targetType) != null || !targetType.IsValueType)
                return null;
            throw new InvalidCastException($"Cannot convert null to non-nullable type {targetType.Name}");
        }

        var sourceType = value.GetType();

        if (sourceType == targetType)
            return value;

        var underlyingTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (sourceType == underlyingTargetType)
            return value;

        if (underlyingTargetType == typeof(string))
            return value.ToString();

        if (underlyingTargetType.IsEnum)
        {
            if (value is string strValue)
                return Enum.Parse(underlyingTargetType, strValue, ignoreCase: true);
            return Enum.ToObject(underlyingTargetType, value);
        }

        if (value is string stringValue)
        {
            if (underlyingTargetType == typeof(bool))
                return bool.Parse(stringValue);

            if (IsNumericType(underlyingTargetType))
                return System.Convert.ChangeType(stringValue, underlyingTargetType);

            if (underlyingTargetType == typeof(DateTime))
                return DateTime.Parse(stringValue);

            if (underlyingTargetType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(stringValue);
        }

        if (IsNumericType(sourceType) && IsNumericType(underlyingTargetType))
            return System.Convert.ChangeType(value, underlyingTargetType);

        return System.Convert.ChangeType(value, underlyingTargetType);
    }

    public TResult? Convert<TResult>(object? value)
    {
        var result = Convert(value, typeof(TResult));
        return (TResult?)result;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }
}
