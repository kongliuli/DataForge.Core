using DataForge.Core;
using DataForge.Core.PostgreSql;
using DataForge.Core.Sqlite;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DataForge.Core.IntegrationTests;

/// <summary>
/// Three cross-database transfer scenarios: SQLite ↔ PostgreSQL.
/// Requires Docker container <c>dataforge-pg</c> on localhost:5433
/// (user/password/db = dataforge).
/// </summary>
public class SqlitePostgreSqlTransferScenariosTests : IAsyncLifetime
{
    private const string PgConn =
        "Host=127.0.0.1;Port=5433;Database=dataforge;Username=dataforge;Password=dataforge";

    private readonly string _workDir;
    private readonly string _sqliteConn;

    public SqlitePostgreSqlTransferScenariosTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"DataForgeXfer_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _sqliteConn = $"Data Source={Path.Combine(_workDir, "source.db")}";
    }

    public async Task InitializeAsync()
    {
        await EnsurePostgresReachableAsync();
        await ResetPostgresSchemaAsync();
        await ResetSqliteSchemaAsync();
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_workDir))
        {
            try { Directory.Delete(_workDir, true); } catch { /* best effort */ }
        }
        return Task.CompletedTask;
    }

    /// <summary>场景1：同构表跨库 — SQLite orders → PostgreSQL orders</summary>
    [Fact]
    public async Task Scenario1_Homogeneous_SqliteOrders_To_PostgreSqlOrders()
    {
        await SeedSqliteOrdersAsync();

        var export = await SqlitePipelineExtensions
            .FromSqlite<OrderRow>(_sqliteConn, "orders")
            .ToPostgreSqlAsync(PgConn, "orders");

        export.RecordsWritten.Should().Be(3);

        var rows = await PostgreSqlPipelineExtensions
            .FromPostgreSql<OrderRow>(PgConn, "orders")
            .ToListAsync();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        rows.Single(r => r.Id == 2).Amount.Should().Be(250.50m);
    }

    /// <summary>场景2：单表拆多表 — SQLite sales_flat → PG order_headers + order_lines</summary>
    [Fact]
    public async Task Scenario2_SplitOneToMany_SqliteFlat_To_PgHeadersAndLines()
    {
        await SeedSqliteSalesFlatAsync();

        var flat = await SqlitePipelineExtensions
            .FromSqlite<SalesFlatRow>(_sqliteConn, "sales_flat")
            .ToListAsync();

        var headers = flat
            .GroupBy(r => r.OrderId)
            .Select(g => new OrderHeaderRow
            {
                OrderId = g.Key,
                CustomerName = g.First().CustomerName,
                OrderDate = g.First().OrderDate
            })
            .ToList();

        var lines = flat
            .Select(r => new OrderLineRow
            {
                OrderId = r.OrderId,
                LineNo = r.LineNo,
                Sku = r.Sku,
                Qty = r.Qty,
                UnitPrice = r.UnitPrice
            })
            .ToList();

        var h = await DataForgePipeline.FromMemory(headers).ToPostgreSqlAsync(PgConn, "order_headers");
        var l = await DataForgePipeline.FromMemory(lines).ToPostgreSqlAsync(PgConn, "order_lines");

        h.RecordsWritten.Should().Be(2);
        l.RecordsWritten.Should().Be(4);

        var pgHeaders = await PostgreSqlPipelineExtensions
            .FromPostgreSql<OrderHeaderRow>(PgConn, "order_headers")
            .ToListAsync();
        var pgLines = await PostgreSqlPipelineExtensions
            .FromPostgreSql<OrderLineRow>(PgConn, "order_lines")
            .ToListAsync();

        pgHeaders.Should().HaveCount(2);
        pgLines.Should().HaveCount(4);
        pgLines.Where(x => x.OrderId == 100).Should().HaveCount(2);
    }

    /// <summary>
    /// 场景3：多表合单表 + 异构映射 — PG customers + orders → SQLite customer_order_summary
    /// （字段名/形态与源表不同）
    /// </summary>
    [Fact]
    public async Task Scenario3_MergeManyToOne_Heterogeneous_Pg_To_Sqlite()
    {
        await SeedPostgresCustomersAndOrdersAsync();

        var customers = await PostgreSqlPipelineExtensions
            .FromPostgreSql<CustomerRow>(PgConn, "customers")
            .ToListAsync();
        var orders = await PostgreSqlPipelineExtensions
            .FromPostgreSql<OrderRow>(PgConn, "orders")
            .ToListAsync();

        var summary = (
            from o in orders
            join c in customers on o.CustomerId equals c.Id
            select new CustomerOrderSummary
            {
                SummaryId = o.Id,
                Buyer = c.Name,
                RegionCode = c.Region,
                OrderTotal = o.Amount,
                PlacedOn = o.CreatedAt
            }).ToList();

        var export = await DataForgePipeline
            .FromMemory(summary)
            .ToSqliteAsync(_sqliteConn, "customer_order_summary");

        export.RecordsWritten.Should().Be(3);

        var back = await SqlitePipelineExtensions
            .FromSqlite<CustomerOrderSummary>(_sqliteConn, "customer_order_summary")
            .ToListAsync();

        back.Should().HaveCount(3);
        back.Sum(x => x.OrderTotal).Should().Be(orders.Sum(o => o.Amount));
        back.Select(x => x.Buyer).Should().Contain(new[] { "Alice", "Bob" });
    }

    private static async Task EnsurePostgresReachableAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(PgConn);
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PostgreSQL 未就绪。请先启动: docker start dataforge-pg （映射 5433→5432，库/用户/密码 dataforge）。",
                ex);
        }
    }

    private async Task ResetPostgresSchemaAsync()
    {
        await using var conn = new NpgsqlConnection(PgConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS "order_lines";
            DROP TABLE IF EXISTS "order_headers";
            DROP TABLE IF EXISTS "orders";
            DROP TABLE IF EXISTS "customers";
            DROP TABLE IF EXISTS "customer_order_summary";

            CREATE TABLE "customers" (
                "Id" INTEGER PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Region" TEXT NOT NULL
            );

            CREATE TABLE "orders" (
                "Id" INTEGER PRIMARY KEY,
                "CustomerId" INTEGER NOT NULL,
                "Amount" NUMERIC(18,2) NOT NULL,
                "CreatedAt" TIMESTAMP NOT NULL
            );

            CREATE TABLE "order_headers" (
                "OrderId" INTEGER PRIMARY KEY,
                "CustomerName" TEXT NOT NULL,
                "OrderDate" TIMESTAMP NOT NULL
            );

            CREATE TABLE "order_lines" (
                "OrderId" INTEGER NOT NULL,
                "LineNo" INTEGER NOT NULL,
                "Sku" TEXT NOT NULL,
                "Qty" INTEGER NOT NULL,
                "UnitPrice" NUMERIC(18,2) NOT NULL,
                PRIMARY KEY ("OrderId", "LineNo")
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ResetSqliteSchemaAsync()
    {
        await using var conn = new SqliteConnection(_sqliteConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS orders;
            DROP TABLE IF EXISTS sales_flat;
            DROP TABLE IF EXISTS customer_order_summary;

            CREATE TABLE orders (
                Id INTEGER PRIMARY KEY,
                CustomerId INTEGER NOT NULL,
                Amount TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE sales_flat (
                OrderId INTEGER NOT NULL,
                LineNo INTEGER NOT NULL,
                CustomerName TEXT NOT NULL,
                OrderDate TEXT NOT NULL,
                Sku TEXT NOT NULL,
                Qty INTEGER NOT NULL,
                UnitPrice TEXT NOT NULL
            );

            CREATE TABLE customer_order_summary (
                SummaryId INTEGER PRIMARY KEY,
                Buyer TEXT NOT NULL,
                RegionCode TEXT NOT NULL,
                OrderTotal TEXT NOT NULL,
                PlacedOn TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedSqliteOrdersAsync()
    {
        var seed = new[]
        {
            new OrderRow { Id = 1, CustomerId = 10, Amount = 100m, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new OrderRow { Id = 2, CustomerId = 20, Amount = 250.50m, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new OrderRow { Id = 3, CustomerId = 10, Amount = 75m, CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc) }
        };

        await DataForgePipeline.FromMemory(seed).ToSqliteAsync(_sqliteConn, "orders");
    }

    private async Task SeedSqliteSalesFlatAsync()
    {
        var seed = new[]
        {
            new SalesFlatRow { OrderId = 100, LineNo = 1, CustomerName = "Alice", OrderDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), Sku = "SKU-A", Qty = 2, UnitPrice = 10m },
            new SalesFlatRow { OrderId = 100, LineNo = 2, CustomerName = "Alice", OrderDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), Sku = "SKU-B", Qty = 1, UnitPrice = 30m },
            new SalesFlatRow { OrderId = 200, LineNo = 1, CustomerName = "Bob", OrderDate = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), Sku = "SKU-C", Qty = 5, UnitPrice = 8m },
            new SalesFlatRow { OrderId = 200, LineNo = 2, CustomerName = "Bob", OrderDate = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc), Sku = "SKU-D", Qty = 3, UnitPrice = 12m }
        };

        await DataForgePipeline.FromMemory(seed).ToSqliteAsync(_sqliteConn, "sales_flat");
    }

    private async Task SeedPostgresCustomersAndOrdersAsync()
    {
        var customers = new[]
        {
            new CustomerRow { Id = 10, Name = "Alice", Region = "east" },
            new CustomerRow { Id = 20, Name = "Bob", Region = "west" }
        };
        var orders = new[]
        {
            new OrderRow { Id = 1, CustomerId = 10, Amount = 100m, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new OrderRow { Id = 2, CustomerId = 20, Amount = 250.50m, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new OrderRow { Id = 3, CustomerId = 10, Amount = 75m, CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc) }
        };

        await DataForgePipeline.FromMemory(customers).ToPostgreSqlAsync(PgConn, "customers");
        await DataForgePipeline.FromMemory(orders).ToPostgreSqlAsync(PgConn, "orders");
    }

    private sealed class OrderRow
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class CustomerRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Region { get; set; } = "";
    }

    private sealed class SalesFlatRow
    {
        public int OrderId { get; set; }
        public int LineNo { get; set; }
        public string CustomerName { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Sku { get; set; } = "";
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private sealed class OrderHeaderRow
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = "";
        public DateTime OrderDate { get; set; }
    }

    private sealed class OrderLineRow
    {
        public int OrderId { get; set; }
        public int LineNo { get; set; }
        public string Sku { get; set; } = "";
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private sealed class CustomerOrderSummary
    {
        public int SummaryId { get; set; }
        public string Buyer { get; set; } = "";
        public string RegionCode { get; set; } = "";
        public decimal OrderTotal { get; set; }
        public DateTime PlacedOn { get; set; }
    }
}
