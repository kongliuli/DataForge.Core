using DataForge.Core.Core.Models;
using DataForge.Core.Core.Sources.Options;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DataForge.Core.Core.Sources.Implementations;

internal class CsvSource<T> : IFileDataSource<T>
{
    private readonly CsvSourceOptions _options;

    public string FilePath { get; }

    public CsvSource(string filePath, CsvSourceOptions? options = null)
    {
        FilePath = filePath;
        _options = options ?? new CsvSourceOptions();
    }

    public async IAsyncEnumerable<T> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(FilePath, _options.Encoding);
        string? headerLine = null;
        string[]? headers = null;

        if (_options.HasHeaderRow)
        {
            headerLine = await reader.ReadLineAsync().ConfigureAwait(false);
            headers = headerLine?.Split(_options.Delimiter);
        }

        for (var i = 0; i < _options.SkipRows; i++)
        {
            await reader.ReadLineAsync().ConfigureAwait(false);
        }

        var rowCount = 0;
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.CommentPrefix != null && line.StartsWith(_options.CommentPrefix))
            {
                continue;
            }

            if (_options.MaxRows.HasValue && rowCount >= _options.MaxRows.Value)
            {
                break;
            }

            var values = ParseCsvLine(line);
            var item = MapToObject(values, headers);
            yield return item;
            rowCount++;
        }
    }

    public Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(FilePath);
        return Task.FromResult(new DataSourceMetadata
        {
            SourceType = "CSV",
            Location = FilePath,
            Size = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(FilePath));
    }

    private string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var currentValue = new char[line.Length];
        var inQuotes = false;
        var valueIndex = 0;
        var charIndex = 0;

        foreach (var c in line)
        {
            if (c == _options.QuoteChar)
            {
                inQuotes = !inQuotes;
            }
            else if (c == _options.Delimiter && !inQuotes)
            {
                values.Add(new string(currentValue, 0, valueIndex));
                valueIndex = 0;
            }
            else
            {
                currentValue[valueIndex++] = c;
            }
            charIndex++;
        }
        values.Add(new string(currentValue, 0, valueIndex));

        return values.ToArray();
    }

    private T MapToObject(string[] values, string[]? headers)
    {
        var type = typeof(T);
        if (type == typeof(string))
        {
            return (T)(object)string.Join(_options.Delimiter, values);
        }

        if (type.IsPrimitive || type == typeof(decimal) || type == typeof(string))
        {
            return values.Length > 0 ? ConvertValue<T>(values[0]) : default!;
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
                var convertedValue = Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
                property.SetValue(instance, convertedValue);
            }
        }

        return instance;
    }

    private T ConvertValue<T>(string value)
    {
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }
}