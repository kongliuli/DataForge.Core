using DataMigration.Contracts;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataMigration.Plugin.GeometryTransformer;

public class GeometryTransformer : ITransformer
{
    public string Id => "DataMigration.Plugin.GeometryTransformer";
    public string Name => "几何转换器";
    public string Description => "对 CAD 几何数据进行坐标变换、过滤和清洗";
    public Version Version => new Version(1, 0, 0);

    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct)
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

    public async IAsyncEnumerable<DataRecord> TransformAsync(IAsyncEnumerable<DataRecord> input, TransformConfig config, [EnumeratorCancellation] CancellationToken ct)
    {
        // 解析配置
        var offsetX = ParseDoubleConfig(config, "OffsetX");
        var offsetY = ParseDoubleConfig(config, "OffsetY");
        var offsetZ = ParseDoubleConfig(config, "OffsetZ");
        var scale = ParseDoubleConfig(config, "Scale") ?? 1.0;
        var rotation = ParseDoubleConfig(config, "Rotation") ?? 0.0;
        var layerFilter = ParseStringListConfig(config, "LayerFilter");
        var entityTypeFilter = ParseStringListConfig(config, "EntityTypeFilter");
        var expandBlockAttributes = ParseBoolConfig(config, "ExpandBlockAttributes") ?? false;
        var checkClosed = ParseBoolConfig(config, "CheckClosed") ?? false;
        var markDuplicates = ParseBoolConfig(config, "MarkDuplicates") ?? false;

        // 用于去重检测
        var geometryHashes = new HashSet<string>();

        await foreach (var record in input)
        {
            ct.ThrowIfCancellationRequested();

            // 实体过滤
            if (layerFilter.Count > 0)
            {
                var layer = record.GetValue<string>("Layer");
                if (layer == null || !layerFilter.Contains(layer))
                {
                    continue;
                }
            }

            if (entityTypeFilter.Count > 0)
            {
                var entityType = record.GetValue<string>("Type");
                if (entityType == null || !entityTypeFilter.Contains(entityType))
                {
                    continue;
                }
            }

            // 处理几何数据
            if (record.TryGetValue("Geometry", out var geometryObj) && geometryObj is string geometryJson)
            {
                try
                {
                    var geometry = JsonSerializer.Deserialize<JsonElement>(geometryJson);
                    var transformedGeometry = TransformGeometry(geometry, offsetX, offsetY, offsetZ, scale, rotation);
                    record["Geometry"] = JsonSerializer.Serialize(transformedGeometry);

                    // 闭合校验
                    if (checkClosed && record.GetValue<string>("Type") is "Polyline" or "LwPolyline")
                    {
                        var isClosed = CheckPolylineClosed(transformedGeometry);
                        record["IsClosed"] = isClosed;
                        if (!isClosed)
                        {
                            record["ClosedCheckError"] = "Polyline is not closed";
                        }
                    }

                    // 去重标记
                    if (markDuplicates)
                    {
                        var hash = ComputeGeometryHash(transformedGeometry);
                        if (geometryHashes.Contains(hash))
                        {
                            record["IsDuplicate"] = true;
                        }
                        else
                        {
                            geometryHashes.Add(hash);
                            record["IsDuplicate"] = false;
                        }
                    }
                }
                catch
                {
                    // 几何数据解析失败，保留原始数据
                }
            }

            // 块属性展开
            if (expandBlockAttributes && record.GetValue<string>("Type") == "Insert")
            {
                ExpandBlockAttributes(record);
            }

            yield return record;
        }
    }

    private double? ParseDoubleConfig(TransformConfig config, string key)
    {
        if (config.TryGetValue(key, out var value) && double.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }

    private bool? ParseBoolConfig(TransformConfig config, string key)
    {
        if (config.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }

    private HashSet<string> ParseStringListConfig(TransformConfig config, string key)
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

    private JsonElement TransformGeometry(JsonElement geometry, double? offsetX, double? offsetY, double? offsetZ, double scale, double rotation)
    {
        var type = geometry.GetProperty("type").GetString();

        using var doc = JsonDocument.Parse(geometry.GetRawText());
        var root = doc.RootElement;
        var dict = new Dictionary<string, object?>();

        foreach (var prop in root.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }

        // 应用坐标变换
        if (offsetX.HasValue || offsetY.HasValue || offsetZ.HasValue)
        {
            ApplyOffset(dict, offsetX ?? 0, offsetY ?? 0, offsetZ ?? 0);
        }

        if (scale != 1.0)
        {
            ApplyScale(dict, scale);
        }

        if (rotation != 0.0)
        {
            ApplyRotation(dict, rotation);
        }

        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(dict));
    }

    private void ApplyOffset(Dictionary<string, object?> dict, double offsetX, double offsetY, double offsetZ)
    {
        // 处理 start/end (Line)
        if (dict.ContainsKey("start"))
        {
            dict["start"] = OffsetPoint(dict["start"], offsetX, offsetY, offsetZ);
        }
        if (dict.ContainsKey("end"))
        {
            dict["end"] = OffsetPoint(dict["end"], offsetX, offsetY, offsetZ);
        }

        // 处理 center (Circle, Arc)
        if (dict.ContainsKey("center"))
        {
            dict["center"] = OffsetPoint(dict["center"], offsetX, offsetY, offsetZ);
        }

        // 处理 position (Insert, Text, MText)
        if (dict.ContainsKey("position"))
        {
            dict["position"] = OffsetPoint(dict["position"], offsetX, offsetY, offsetZ);
        }

        // 处理 vertices (Polyline, LwPolyline)
        if (dict.ContainsKey("vertices"))
        {
            dict["vertices"] = OffsetVertices(dict["vertices"], offsetX, offsetY, offsetZ);
        }
    }

    private double[] OffsetPoint(object? pointObj, double offsetX, double offsetY, double offsetZ)
    {
        if (pointObj is JsonElement pointElement && pointElement.ValueKind == JsonValueKind.Array)
        {
            var coords = pointElement.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            if (coords.Length >= 2)
            {
                coords[0] += offsetX;
                coords[1] += offsetY;
            }
            if (coords.Length >= 3)
            {
                coords[2] += offsetZ;
            }
            return coords;
        }
        return new double[0];
    }

    private List<double[]> OffsetVertices(object? verticesObj, double offsetX, double offsetY, double offsetZ)
    {
        var result = new List<double[]>();
        if (verticesObj is JsonElement verticesElement && verticesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var vertex in verticesElement.EnumerateArray())
            {
                result.Add(OffsetPoint(vertex, offsetX, offsetY, offsetZ));
            }
        }
        return result;
    }

    private void ApplyScale(Dictionary<string, object?> dict, double scale)
    {
        // 处理 start/end (Line)
        if (dict.ContainsKey("start"))
        {
            dict["start"] = ScalePoint(dict["start"], scale);
        }
        if (dict.ContainsKey("end"))
        {
            dict["end"] = ScalePoint(dict["end"], scale);
        }

        // 处理 center (Circle, Arc)
        if (dict.ContainsKey("center"))
        {
            dict["center"] = ScalePoint(dict["center"], scale);
        }

        // 处理 position (Insert, Text, MText)
        if (dict.ContainsKey("position"))
        {
            dict["position"] = ScalePoint(dict["position"], scale);
        }

        // 处理 vertices (Polyline, LwPolyline)
        if (dict.ContainsKey("vertices"))
        {
            dict["vertices"] = ScaleVertices(dict["vertices"], scale);
        }

        // 处理 radius (Circle, Arc)
        if (dict.ContainsKey("radius"))
        {
            var radius = ((JsonElement)dict["radius"]!).GetDouble();
            dict["radius"] = radius * scale;
        }

        // 处理 scale (Insert)
        if (dict.ContainsKey("scale"))
        {
            dict["scale"] = ScalePoint(dict["scale"], scale);
        }
    }

    private double[] ScalePoint(object? pointObj, double scale)
    {
        if (pointObj is JsonElement pointElement && pointElement.ValueKind == JsonValueKind.Array)
        {
            return pointElement.EnumerateArray().Select(e => e.GetDouble() * scale).ToArray();
        }
        return new double[0];
    }

    private List<double[]> ScaleVertices(object? verticesObj, double scale)
    {
        var result = new List<double[]>();
        if (verticesObj is JsonElement verticesElement && verticesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var vertex in verticesElement.EnumerateArray())
            {
                result.Add(ScalePoint(vertex, scale));
            }
        }
        return result;
    }

    private void ApplyRotation(Dictionary<string, object?> dict, double rotation)
    {
        var radians = rotation * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // 处理 start/end (Line)
        if (dict.ContainsKey("start"))
        {
            dict["start"] = RotatePoint(dict["start"], cos, sin);
        }
        if (dict.ContainsKey("end"))
        {
            dict["end"] = RotatePoint(dict["end"], cos, sin);
        }

        // 处理 center (Circle, Arc)
        if (dict.ContainsKey("center"))
        {
            dict["center"] = RotatePoint(dict["center"], cos, sin);
        }

        // 处理 position (Insert, Text, MText)
        if (dict.ContainsKey("position"))
        {
            dict["position"] = RotatePoint(dict["position"], cos, sin);
        }

        // 处理 vertices (Polyline, LwPolyline)
        if (dict.ContainsKey("vertices"))
        {
            dict["vertices"] = RotateVertices(dict["vertices"], cos, sin);
        }

        // 更新 rotation 字段 (Insert, Arc)
        if (dict.ContainsKey("rotation"))
        {
            var existingRotation = ((JsonElement)dict["rotation"]!).GetDouble();
            dict["rotation"] = existingRotation + rotation;
        }
    }

    private double[] RotatePoint(object? pointObj, double cos, double sin)
    {
        if (pointObj is JsonElement pointElement && pointElement.ValueKind == JsonValueKind.Array)
        {
            var coords = pointElement.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            if (coords.Length >= 2)
            {
                var x = coords[0];
                var y = coords[1];
                coords[0] = x * cos - y * sin;
                coords[1] = x * sin + y * cos;
            }
            return coords;
        }
        return new double[0];
    }

    private List<double[]> RotateVertices(object? verticesObj, double cos, double sin)
    {
        var result = new List<double[]>();
        if (verticesObj is JsonElement verticesElement && verticesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var vertex in verticesElement.EnumerateArray())
            {
                result.Add(RotatePoint(vertex, cos, sin));
            }
        }
        return result;
    }

    private bool CheckPolylineClosed(JsonElement geometry)
    {
        if (geometry.TryGetProperty("closed", out var closedProp))
        {
            return closedProp.GetBoolean();
        }
        return false;
    }

    private string ComputeGeometryHash(JsonElement geometry)
    {
        // 简化：使用几何数据的字符串表示作为哈希
        var normalized = NormalizeGeometryForHash(geometry);
        return normalized.GetHashCode().ToString();
    }

    private string NormalizeGeometryForHash(JsonElement geometry)
    {
        // 提取关键几何特征用于去重检测
        var type = geometry.GetProperty("type").GetString() ?? "";

        switch (type)
        {
            case "Line":
                var start = GetPointString(geometry, "start");
                var end = GetPointString(geometry, "end");
                return $"Line:{start}-{end}";

            case "Circle":
                var center = GetPointString(geometry, "center");
                var radius = GetDoubleValue(geometry, "radius");
                return $"Circle:{center}:{radius:F4}";

            case "Arc":
                var arcCenter = GetPointString(geometry, "center");
                var arcRadius = GetDoubleValue(geometry, "radius");
                var startAngle = GetDoubleValue(geometry, "startAngle");
                var endAngle = GetDoubleValue(geometry, "endAngle");
                return $"Arc:{arcCenter}:{arcRadius:F4}:{startAngle:F4}:{endAngle:F4}";

            case "Polyline":
            case "LwPolyline":
                var vertices = GetVerticesString(geometry);
                return $"{type}:{vertices}";

            default:
                return type;
        }
    }

    private string GetPointString(JsonElement geometry, string propertyName)
    {
        if (geometry.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var coords = prop.EnumerateArray().Select(e => e.GetDouble()).ToArray();
            return string.Join(",", coords.Select(c => c.ToString("F4")));
        }
        return "";
    }

    private double GetDoubleValue(JsonElement geometry, string propertyName)
    {
        if (geometry.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetDouble();
        }
        return 0;
    }

    private string GetVerticesString(JsonElement geometry)
    {
        if (geometry.TryGetProperty("vertices", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var vertices = new List<string>();
            foreach (var vertex in prop.EnumerateArray())
            {
                if (vertex.ValueKind == JsonValueKind.Array)
                {
                    var coords = vertex.EnumerateArray().Select(e => e.GetDouble().ToString("F4"));
                    vertices.Add($"[{string.Join(",", coords)}]");
                }
            }
            return string.Join("|", vertices);
        }
        return "";
    }

    private void ExpandBlockAttributes(DataRecord record)
    {
        // 从 Geometry 中提取块属性信息
        if (record.TryGetValue("Geometry", out var geometryObj) && geometryObj is string geometryJson)
        {
            try
            {
                var geometry = JsonSerializer.Deserialize<JsonElement>(geometryJson);
                if (geometry.TryGetProperty("blockName", out var blockName))
                {
                    record["BlockName"] = blockName.GetString();
                }
                if (geometry.TryGetProperty("position", out var position))
                {
                    var coords = position.EnumerateArray().Select(e => e.GetDouble()).ToArray();
                    if (coords.Length >= 2)
                    {
                        record["BlockPositionX"] = coords[0];
                        record["BlockPositionY"] = coords[1];
                    }
                    if (coords.Length >= 3)
                    {
                        record["BlockPositionZ"] = coords[2];
                    }
                }
                if (geometry.TryGetProperty("rotation", out var rotation))
                {
                    record["BlockRotation"] = rotation.GetDouble();
                }
            }
            catch
            {
                // 解析失败，忽略
            }
        }
    }
}
