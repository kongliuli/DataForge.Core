using DataForge.Jobs.Execution;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Xunit;

namespace DataForge.Sync.Tests;

/// <summary>迁移行数对账（reconciliation）相关测试。</summary>
public class JobReconciliationTests
{
    /// <summary>无 transforms：CSV 全量 → JSON，行数应自洽，且报告 JSON 里出现 reconciliation。</summary>
    [Fact]
    public async Task RunAsync_NoTransforms_CsvToJson_Reconciles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-recon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "input.csv");
        var outPath = Path.Combine(tempDir, "output.json");
        var jobPath = Path.Combine(tempDir, "job.yaml");
        var reportPath = Path.Combine(tempDir, "run.json");

        try
        {
            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,20
                3,30
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            var recon = result.Reconciliation;
            Assert.NotNull(recon);
            Assert.Equal(3, recon!.RowsRead);
            Assert.Equal(0, recon.RowsSkippedByWatermark);
            Assert.Equal(0, recon.RowsFiltered);
            Assert.Equal(0, recon.RowsRejectedByValidation);
            Assert.Equal(3, recon.RowsWritten);
            Assert.Equal(0, recon.Difference);
            Assert.True(recon.Reconciled);

            // 报告 JSON 里能找到 Reconciliation 字段（与现有 JobName/Export 一样用 PascalCase）
            await JobRunReportWriter.WriteAsync(result, reportPath);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            Assert.True(doc.RootElement.TryGetProperty("Reconciliation", out var node));
            Assert.Equal(3, node.GetProperty("RowsRead").GetInt64());
            Assert.Equal(3, node.GetProperty("RowsWritten").GetInt64());
            Assert.True(node.GetProperty("Reconciled").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>带 where 过滤：过滤行计入 RowsFiltered，对账差异仍为 0。</summary>
    [Fact]
    public async Task RunAsync_WhereFilter_Reconciles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-recon-where-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "input.csv");
        var outPath = Path.Combine(tempDir, "output.json");
        var jobPath = Path.Combine(tempDir, "job.yaml");

        try
        {
            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,-1
                3,5
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                transforms:
                  - where: "Amount > 0"
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            var recon = result.Reconciliation;
            Assert.NotNull(recon);
            Assert.Equal(3, recon!.RowsRead);
            Assert.Equal(1, recon.RowsFiltered);
            Assert.Equal(2, recon.RowsWritten);
            Assert.Equal(0, recon.Difference);
            Assert.True(recon.Reconciled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>watermark 跳过旧行：跳过数计入 RowsSkippedByWatermark，对账差异为 0。</summary>
    [Fact]
    public async Task RunAsync_WatermarkSkipsOldRows_Reconciles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-recon-wm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "in.csv");
        var outPath = Path.Combine(tempDir, "out.json");
        var statePath = Path.Combine(tempDir, "wm.txt");
        var jobPath = Path.Combine(tempDir, "job.yaml");

        try
        {
            await File.WriteAllTextAsync(csvPath, "Id,Name\n1,a\n2,b\n3,c\n");
            await File.WriteAllTextAsync(statePath, "1");
            await File.WriteAllTextAsync(jobPath, $"""
                name: incremental
                watermark:
                  column: Id
                  statePath: {statePath.Replace('\\', '/')}
                source:
                  type: csv
                  path: {csvPath.Replace('\\', '/')}
                sink:
                  type: json
                  path: {outPath.Replace('\\', '/')}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            var recon = result.Reconciliation;
            Assert.NotNull(recon);
            Assert.Equal(3, recon!.RowsRead); // CSV 3 行都进入了执行期管道
            Assert.Equal(1, recon.RowsSkippedByWatermark); // Id=1 与水位相等，不算 after
            Assert.Equal(0, recon.RowsFiltered);
            Assert.Equal(2, recon.RowsWritten);
            Assert.Equal(0, recon.Difference);
            Assert.True(recon.Reconciled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>validate continue：坏行按行计数，对账仍自洽。</summary>
    [Fact]
    public async Task RunAsync_ValidateContinue_CountsRejectedRows()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-recon-val-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "input.csv");
        var outPath = Path.Combine(tempDir, "output.json");
        var errPath = Path.Combine(tempDir, "errors.ndjson");
        var jobPath = Path.Combine(tempDir, "job.yaml");

        try
        {
            await File.WriteAllTextAsync(csvPath, """
                OrderId,Amount
                1,10
                2,-5
                """);

            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{csvPath}}
                validate:
                  onError: continue
                  badRowOutput: {{errPath}}
                  rules:
                    - field: Amount
                      min: 0
                sink:
                  type: json
                  path: {{outPath}}
                """);

            var result = await new JobRunner().RunAsync(jobPath);

            Assert.True(result.Success, result.ErrorMessage);
            var recon = result.Reconciliation;
            Assert.NotNull(recon);
            Assert.Equal(2, recon!.RowsRead);
            Assert.Equal(1, recon.RowsRejectedByValidation); // 按行计：Amount=-5 那行
            Assert.Equal(1, recon.RowsWritten);
            Assert.Equal(0, recon.Difference);
            Assert.True(recon.Reconciled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>JOIN 路径：RowsRead = 内连接结果行数。</summary>
    [Fact]
    public async Task RunAsync_Join_Reconciles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dataforge-recon-join-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "src.db");
        var dest = Path.Combine(dir, "out.db");
        var yamlPath = Path.Combine(dir, "join.yaml");

        try
        {
            await using (var connection = new SqliteConnection($"Data Source={db}"))
            {
                await connection.OpenAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE customers (Id TEXT, Name TEXT);
                    INSERT INTO customers VALUES ('1','Ada'),('2','Bob');
                    CREATE TABLE orders (Id TEXT, CustomerId TEXT, Amount TEXT);
                    INSERT INTO orders VALUES ('10','1','12'),('11','1','8'),('12','2','5');
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(yamlPath, $"""
                name: join-orders
                sources:
                  - type: sqlite
                    alias: c
                    connection: Data Source={db.Replace('\\', '/')}
                    table: customers
                  - type: sqlite
                    alias: o
                    connection: Data Source={db.Replace('\\', '/')}
                    table: orders
                    on: c_Id = o_CustomerId
                sink:
                  type: sqlite
                  connection: Data Source={dest.Replace('\\', '/')}
                  table: summary
                  createTable: true
                  mode: insert
                """);

            var result = await new JobRunner().RunAsync(yamlPath);

            Assert.True(result.Success, result.ErrorMessage);
            var recon = result.Reconciliation;
            Assert.NotNull(recon);
            Assert.Equal(3, recon!.RowsRead); // 内连接结果 3 行（orders 3 行，每个 customer 都能匹配）
            Assert.Equal(0, recon.RowsFiltered);
            Assert.Equal(3, recon.RowsWritten);
            Assert.Equal(0, recon.Difference);
            Assert.True(recon.Reconciled);
            Assert.Equal(0, recon.RowsRejectedByValidation); // 未配置 validate
        }
        finally
        {
            // sqlite 连接可能短暂占用文件；清理失败不掩盖断言结果
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>失败作业（源文件不存在）：不抛异常，Success=false，且没有对账数据。</summary>
    [Fact]
    public async Task RunAsync_MissingSource_FailsWithoutThrowing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dataforge-recon-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var missingCsv = Path.Combine(tempDir, "missing.csv");
        var jobPath = Path.Combine(tempDir, "job.yaml");
        var reportPath = Path.Combine(tempDir, "run.json");

        try
        {
            await File.WriteAllTextAsync(jobPath, $$"""
                source:
                  type: csv
                  path: {{missingCsv}}
                sink:
                  type: json
                  path: {{Path.Combine(tempDir, "out.json")}}
                """);

            var result = await new JobRunner().RunAsync(jobPath); // 失败也不应抛异常

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Reconciliation);

            await JobRunReportWriter.WriteAsync(result, reportPath);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            Assert.False(doc.RootElement.TryGetProperty("Reconciliation", out _));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>模型自身的差异/摘要计算（含 Reconciled=false 只是提示的语义）。</summary>
    [Fact]
    public void Reconciliation_DifferenceAndSummary()
    {
        var ok = new JobRunReconciliation
        {
            RowsRead = 3,
            RowsSkippedByWatermark = 0,
            RowsFiltered = 1,
            RowsRejectedByValidation = 0,
            RowsWritten = 2
        };
        Assert.Equal(0, ok.Difference);
        Assert.True(ok.Reconciled);
        Assert.Equal("Reconciled: read=3 skipped=0 filtered=1 rejected=0 written=2 difference=0", ok.ToSummary());

        // rejected=null（计数不可观测）按 0 处理，但摘要里如实显示 n/a
        var unknownRejected = ok with { RowsRejectedByValidation = null };
        Assert.Equal(0, unknownRejected.Difference);

        // 有差异：只是提示，不改变作业成败语义
        var mismatch = ok with { RowsWritten = 1 };
        Assert.Equal(1, mismatch.Difference);
        Assert.False(mismatch.Reconciled);
        Assert.Equal("Not reconciled: read=3 skipped=0 filtered=1 rejected=0 written=1 difference=1", mismatch.ToSummary());
    }
}
