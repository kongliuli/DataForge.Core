using DataForge.Core;
using DataForge.Core.Sqlite;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DataForge.Core.Tests.Sqlite;

public class SqliteRoundTripTests
{
    public sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task FromSqlite_ReadsRows_WrittenByTarget()
    {
        // Shared in-memory DB across connections.
        var cs = "Data Source=DataForgeTests;Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(cs);
        await keepAlive.OpenAsync();

        await using (var cmd = keepAlive.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE Products (Id INTEGER NOT NULL, Name TEXT NOT NULL);";
            await cmd.ExecuteNonQueryAsync();
        }

        var empty = await SqlitePipelineExtensions.FromSqlite<Product>(cs, "Products").ToListAsync();
        empty.Should().BeEmpty();

        var write = await DataForgePipeline.FromMemory(new[]
            {
                new Product { Id = 1, Name = "A" },
                new Product { Id = 2, Name = "B" }
            })
            .ToSqlite(cs, "Products");

        write.RecordsWritten.Should().Be(2);

        var rows = await SqlitePipelineExtensions.FromSqlite<Product>(cs, "Products").ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.Name).Should().BeEquivalentTo(new[] { "A", "B" });
    }
}
