using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources.Implementations;

internal class ExcelSource<T> : IFileDataSource<T>
{
    public string FilePath { get; }
    private readonly string _sheetName;

    public ExcelSource(string filePath, string sheetName)
    {
        FilePath = filePath;
        _sheetName = sheetName;
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(FilePath);
        string? headerLine = null;
        string[]? headers = null;

        headerLine = await reader.ReadLineAsync().ConfigureAwait(false);
        headers = headerLine?.Split(',');

        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = line.Split(',');
            var item = MapToObject(values, headers);
            yield return item;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "Excel",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(FilePath));
    }

    private T MapToObject(string[] values, string[]? headers)
    {
        var type = typeof(T);
        
        if (type == typeof(string))
        {
            return (T)(object)string.Join(",", values);
        }

        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(string))
        {
            return values.Length > 0 ? (T)Convert.ChangeType(values[0], typeof(T)) : default!;
        }

        var instance = Activator.CreateInstance<T>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            string? value = null;

            if (headers != null)
            {
                var headerIndex = Array.IndexOf(headers, property.Name);
                if (headerIndex >= 0 && headerIndex < values.Length)
                {
                    value = values[headerIndex];
                }
            }
            else if (i < values.Length)
            {
                value = values[i];
            }

            if (value != null)
            {
                try
                {
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch
                {
                }
            }
        }

        return instance;
    }
}