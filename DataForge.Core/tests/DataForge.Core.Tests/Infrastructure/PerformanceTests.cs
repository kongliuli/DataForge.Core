using DataForge.Core.Core.Infrastructure;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace DataForge.Core.Tests.Infrastructure;

public class PerformanceTests
{
    [Fact]
    public void PerformanceCounter_CalculatesItemsPerSecond()
    {
        var counter = new PerformanceCounter(1000);
        counter.Start();

        // 模拟处理
        for (var i = 0; i < 100; i++)
        {
            counter.IncrementProcessed();
        }

        counter.ItemsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PerformanceCounter_CalculatesProgressPercentage()
    {
        var counter = new PerformanceCounter(100);
        counter.Start();
        counter.IncrementProcessed(50);

        counter.ProgressPercentage.Should().Be(50);
    }

    [Fact]
    public void MemoryOptimizer_ReportsManagedMemorySize()
    {
        var memoryBefore = MemoryOptimizer.GetManagedMemorySize();

        // 分配一些内存
        var testData = Enumerable.Range(0, 10000).Select(i => new byte[1024]).ToArray();

        var memoryAfter = MemoryOptimizer.GetManagedMemorySize();

        memoryAfter.Should().BeGreaterThan(memoryBefore);

        // 清理
        testData.Should().NotBeNull();
    }

    [Fact]
    public void ProgressReporter_ReportsCorrectly()
    {
        var reportedCount = 0;
        var reporter = new ProgressReporter<int>(
            report => reportedCount++,
            reportInterval: 10
        );

        for (var i = 0; i < 100; i++)
        {
            reporter.Report(i, i);
        }

        reportedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BatchOptimizer_FlushesAtBatchSize()
    {
        var flushCount = 0;
        var flushedItems = new System.Collections.Generic.List<int>();
        var optimizer = new BatchOptimizer<int>(3, (items, ct) =>
        {
            flushCount++;
            flushedItems.AddRange(items);
            return System.Threading.Tasks.Task.CompletedTask;
        });

        optimizer.AddAsync(1).Wait();
        flushCount.Should().Be(0);

        optimizer.AddAsync(2).Wait();
        flushCount.Should().Be(0);

        optimizer.AddAsync(3).Wait();
        flushCount.Should().Be(1);
        flushedItems.Should().HaveCount(3);

        optimizer.AddAsync(4).Wait();
        flushCount.Should().Be(1);

        optimizer.FlushAsync().Wait();
        flushCount.Should().Be(2);
        flushedItems.Should().HaveCount(4);
    }
}
