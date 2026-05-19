namespace DataForge.Core.Core.Infrastructure;

public interface ITypeConverter
{
    bool CanConvert(Type sourceType, Type targetType);
    object? Convert(object? value, Type targetType);
    TResult? Convert<TResult>(object? value);
}
