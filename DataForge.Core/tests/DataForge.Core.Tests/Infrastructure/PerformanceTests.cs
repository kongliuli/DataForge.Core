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
        const int count = 10000;
        const int bytesPerArray = 1024;
        var testData = Enumerable.Range(0, count).Select(_ => new byte[bytesPerArray]).ToArray();

        var reported = MemoryOptimizer.GetManagedMemorySize();

        // Other tests and GC can reduce the process-wide total between samples.
        // The report must still account for our live payload, excluding object overhead.
        reported.Should().BeGreaterThanOrEqualTo((long)count * bytesPerArray);
        GC.KeepAlive(testData);
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
    public async System.Threading.Tasks.Task BatchOptimizer_FlushesAtBatchSize()
    {
        var flushCount = 0;
        var flushedItems = new System.Collections.Generic.List<int>();
        var optimizer = new BatchOptimizer<int>(3, (items, ct) =>
        {
            flushCount++;
            flushedItems.AddRange(items);
            return System.Threading.Tasks.Task.CompletedTask;
        });

        await optimizer.AddAsync(1);
        flushCount.Should().Be(0);

        await optimizer.AddAsync(2);
        flushCount.Should().Be(0);

        await optimizer.AddAsync(3);
        flushCount.Should().Be(1);
        flushedItems.Should().HaveCount(3);

        await optimizer.AddAsync(4);
        flushCount.Should().Be(1);

        await optimizer.FlushAsync();
        flushCount.Should().Be(2);
        flushedItems.Should().HaveCount(4);
    }
}
