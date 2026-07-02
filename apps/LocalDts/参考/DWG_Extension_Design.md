# LocalDts 扩展 DWG 支持设计方案

本方案旨在为 `LocalDts` 增加对 AutoCAD DWG 文件的实体结构读取与数据迁移支持。通过引入专门的 **DWGDataSource** 插件和 **GeometryTransformer** 清洗规则，使工具具备处理工程图形数据的能力。

---

## 1. 核心技术选型

由于 DWG 是闭源格式，.NET 环境下的读取方案主要分为以下三类：

| 方案 | 库名称 | 优点 | 缺点 | 推荐度 |
| :--- | :--- | :--- | :--- | :--- |
| **开源库** | [ACadSharp](https://github.com/DomCR/ACadSharp) | 纯 C# 实现，支持 .NET 8，无外部依赖。 | 对高版本 DWG 支持可能滞后。 | **首选 (POC)** |
| **商业 SDK** | [Teigha (ODA)](https://www.opendesign.com/) | 行业标准，支持 100% 实体数据，性能极高。 | 授权费用高昂。 | **企业级首选** |
| **官方组件** | AutoCAD RealDWG | 官方支持，最稳定。 | 需付费授权，部署复杂。 | **备选** |

---

## 2. 插件设计：DWGDataSource

### 2.1 数据提取模型
DWG 文件并非扁平表结构，而是层级对象模型。插件需要将图形实体（Entity）映射为 `DataRecord`。

**映射策略：**
*   **EntityId**: 映射为记录的唯一主键。
*   **EntityType**: 记录实体的类型（Line, Polyline, Circle, Insert 等）。
*   **Layer**: 所属图层。
*   **Geometry**: 序列化为 JSON 或 WKT 格式的几何数据（例如 `{"type":"Line", "start":[0,0], "end":[10,10]}`）。
*   **Attributes (XData)**: 提取实体的扩展数据或块属性，映射为键值对。

### 2.2 核心实现逻辑
```csharp
public async IAsyncEnumerable<DataRecord> ExtractAsync(SourceConfig config, CancellationToken ct)
{
    string filePath = config["FilePath"];
    using var dwgReader = new DwgReader(filePath);
    var database = dwgReader.Read();

    foreach (var entity in database.Entities)
    {
        var record = _pool.Rent();
        record["Handle"] = entity.Handle;
        record["Type"] = entity.ObjectName;
        record["Layer"] = entity.Layer.Name;
        record["Color"] = entity.Color.ToString();
        
        // 提取几何属性
        if (entity is Line line) {
            record["Geometry"] = $"LINE({line.StartPoint}, {line.EndPoint})";
        }
        
        // 提取扩展属性 (XData)
        foreach (var xdata in entity.ExtendedData) {
            record[$"XData_{xdata.Key}"] = xdata.Value;
        }

        yield return record;
    }
}
```

---

## 3. 清洗规则设计：GeometryTransformer

由于 CAD 数据通常需要转换为 GIS 坐标或结构化业务数据，需要设计专门的清洗规则：

### 3.1 坐标系转换 (Coordinate Transformation)
*   **平移/旋转/缩放**: 将 CAD 的相对坐标转换为地理坐标（如 WGS84）。
*   **规则参数**: `Offset(x,y)`, `Scale(factor)`, `Rotation(degree)`。

### 3.2 实体过滤与重组 (Filtering & Regrouping)
*   **图层过滤**: 仅提取 `WALL` 或 `EQUIPMENT` 图层的实体。
*   **块属性展开**: 将 `Insert` 实体的块属性（Attributes）提升为 `DataRecord` 的顶级字段，方便存入数据库。

### 3.3 几何拓扑清洗
*   **去重**: 自动删除重叠的线条。
*   **闭合校验**: 确保表示房间的 `Polyline` 是闭合的，否则标记为错误。

---

## 4. 目标源适配 (DataTarget)

提取并清洗后的 DWG 数据可以迁移至：
1.  **PostGIS / SQL Server Spatial**: 存储为几何字段。
2.  **Excel / CSV**: 存储实体的属性清单（如设备统计表）。
3.  **JSON**: 用于 Web 端可视化展示。

---

## 5. 总结

通过扩展 DWG 支持，`LocalDts` 可以打通 **设计数据 (CAD)** 与 **管理数据 (DB)** 之间的隔阂。建议第一步先基于 `ACadSharp` 实现基础的实体属性提取插件，再逐步丰富几何转换规则。
