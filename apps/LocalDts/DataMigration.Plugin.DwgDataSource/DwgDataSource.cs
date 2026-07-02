using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.XData;
using DataMigration.Contracts;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataMigration.Plugin.DwgDataSource;

public class DwgDataSource : IDataSource
{
    public string Id => "DataMigration.Plugin.DwgDataSource";
    public string Name => "DWG 数据源";
    public Version Version => new Version(1, 0, 0);

    public Task InitializeAsync(IServiceProvider services, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!config.TryGetValue("FilePath", out var filePath) || string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("DWG 文件路径未配置");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("DWG 文件不存在", filePath);
        }

        // 解析过滤配置
        var layerFilter = ParseFilterConfig(config, "LayerFilter");
        var entityTypeFilter = ParseFilterConfig(config, "EntityTypes");

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new DwgReader(fs);
        var doc = reader.Read();

        foreach (var entity in doc.Entities)
        {
            ct.ThrowIfCancellationRequested();

            // 应用图层过滤
            if (layerFilter.Count > 0 && !layerFilter.Contains(entity.Layer.Name))
            {
                continue;
            }

            // 应用实体类型过滤
            var entityType = GetEntityTypeName(entity);
            if (entityTypeFilter.Count > 0 && !entityTypeFilter.Contains(entityType))
            {
                continue;
            }

            var record = new DataRecord();

            // 基础属性
            record["Handle"] = entity.Handle.ToString();
            record["Type"] = entityType;
            record["Layer"] = entity.Layer.Name;
            record["Color"] = entity.Color.ToString();
            record["LineType"] = entity.LineType?.Name ?? "ByLayer";
            record["LineWeight"] = entity.LineWeight.ToString();

            // 几何数据
            var geometry = ExtractGeometry(entity);
            if (geometry != null)
            {
                record["Geometry"] = JsonSerializer.Serialize(geometry);
            }

            // 扩展数据 (XData)
            ExtractXData(entity, record);

            yield return record;
        }

        await Task.CompletedTask;
    }

    private HashSet<string> ParseFilterConfig(SourceConfig config, string key)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (config.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                result.Add(item.Trim());
            }
        }
        return result;
    }

    private string GetEntityTypeName(Entity entity)
    {
        // 使用 if-else 而不是 switch 表达式来避免类型层次结构问题
        if (entity is Line) return "Line";
        if (entity is Arc) return "Arc";
        if (entity is Circle) return "Circle";
        if (entity is LwPolyline) return "LwPolyline";
        if (entity is Insert) return "Insert";
        if (entity is TextEntity) return "Text";
        if (entity is MText) return "MText";
        return entity.GetType().Name;
    }

    private object? ExtractGeometry(Entity entity)
    {
        // 使用 if-else 而不是 switch 来避免类型层次结构问题
        if (entity is Line line)
        {
            return new
            {
                type = "Line",
                start = new[] { line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z },
                end = new[] { line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z }
            };
        }

        if (entity is Arc arc)
        {
            return new
            {
                type = "Arc",
                center = new[] { arc.Center.X, arc.Center.Y, arc.Center.Z },
                radius = arc.Radius,
                startAngle = arc.StartAngle,
                endAngle = arc.EndAngle
            };
        }

        if (entity is Circle circle)
        {
            return new
            {
                type = "Circle",
                center = new[] { circle.Center.X, circle.Center.Y, circle.Center.Z },
                radius = circle.Radius
            };
        }

        if (entity is LwPolyline lwPolyline)
        {
            return new
            {
                type = "LwPolyline",
                vertices = lwPolyline.Vertices.Select(v => new[] { v.Location.X, v.Location.Y }).ToList(),
                closed = lwPolyline.IsClosed
            };
        }

        if (entity is Insert insert)
        {
            return new
            {
                type = "Insert",
                blockName = insert.Block?.Name ?? "Unknown",
                position = new[] { insert.InsertPoint.X, insert.InsertPoint.Y, insert.InsertPoint.Z },
                scale = new[] { insert.XScale, insert.YScale, insert.ZScale },
                rotation = insert.Rotation
            };
        }

        if (entity is TextEntity text)
        {
            return new
            {
                type = "Text",
                position = new[] { text.InsertPoint.X, text.InsertPoint.Y, text.InsertPoint.Z },
                text = text.Value,
                height = text.Height
            };
        }

        if (entity is MText mtext)
        {
            return new
            {
                type = "MText",
                position = new[] { mtext.InsertPoint.X, mtext.InsertPoint.Y, mtext.InsertPoint.Z },
                text = mtext.Value,
                height = mtext.Height
            };
        }

        return null;
    }

    private void ExtractXData(Entity entity, DataRecord record)
    {
        if (entity.ExtendedData == null)
        {
            return;
        }

        // ExtendedDataDictionary 实现了 IEnumerable<KeyValuePair<AppId, ExtendedData>>
        foreach (var entry in entity.ExtendedData)
        {
            var appId = entry.Key.Name;
            var extendedData = entry.Value;

            if (extendedData == null || extendedData.Records == null)
            {
                continue;
            }

            // 将 XData 数据提取为列表
            var xDataValues = new List<object?>();
            foreach (var dataRecord in extendedData.Records)
            {
                xDataValues.Add(ConvertXDataRecord(dataRecord));
            }

            // 使用 XData_ 前缀存储到 DataRecord
            record[$"XData_{appId}"] = JsonSerializer.Serialize(xDataValues);
        }
    }

    private object? ConvertXDataRecord(ExtendedDataRecord dataRecord)
    {
        if (dataRecord == null)
        {
            return null;
        }

        // ExtendedDataRecord 是一个抽象类，实际类型是泛型 ExtendedDataRecord<T>
        // 我们需要通过反射获取 Value 属性
        var valueProperty = dataRecord.GetType().GetProperty("Value");
        if (valueProperty != null)
        {
            var value = valueProperty.GetValue(dataRecord);
            return ConvertXDataValue(value);
        }

        return dataRecord.ToString();
    }

    private object? ConvertXDataValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        // 处理不同类型的 XData 值
        return value switch
        {
            string str => str,
            int i => i,
            double d => d,
            short s => s,
            long l => l,
            byte b => b,
            bool bl => bl,
            CSMath.XYZ xyz => new[] { xyz.X, xyz.Y, xyz.Z },
            CSMath.XY xy => new[] { xy.X, xy.Y },
            _ => value.ToString()
        };
    }
}
