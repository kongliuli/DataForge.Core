using DataMigration.Core;
using DataMigration.Contracts;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataMigration.Tests;

public class MigrationEngineTests
{
    [Fact]
    public async Task RunAsync_ShouldExecuteMigrationTask()
    {
        // Arrange
        var mockPluginManager = new Mock<IPluginManager>();
        var mockDataSource = new Mock<IDataSource>();
        var mockTransformer = new Mock<ITransformer>();
        var mockDataTarget = new Mock<IDataTarget>();

        // Setup mock data source
        var dataRecords = new List<DataRecord>
        {
            new DataRecord { { "Id", 1 }, { "Name", "Test" } }
        };
        async IAsyncEnumerable<DataRecord> GetDataFlow()
        {
            foreach (var record in dataRecords)
            {
                yield return record;
            }
        }
        mockDataSource.Setup(ds => ds.ExtractAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()))
            .Returns(GetDataFlow());
        mockDataSource.Setup(ds => ds.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockDataSource.Setup(ds => ds.ShutdownAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup mock transformer
        mockTransformer.Setup(t => t.TransformAsync(It.IsAny<IAsyncEnumerable<DataRecord>>(), It.IsAny<TransformConfig>(), It.IsAny<CancellationToken>()))
            .Returns((IAsyncEnumerable<DataRecord> input, TransformConfig config, CancellationToken ct) => input);
        mockTransformer.Setup(t => t.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockTransformer.Setup(t => t.ShutdownAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup mock data target
        mockDataTarget.Setup(dt => dt.LoadAsync(It.IsAny<IAsyncEnumerable<DataRecord>>(), It.IsAny<TargetConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockDataTarget.Setup(dt => dt.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockDataTarget.Setup(dt => dt.ShutdownAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup plugin manager
        mockPluginManager.Setup(pm => pm.GetDataSource(It.IsAny<string>()))
            .Returns(mockDataSource.Object);
        mockPluginManager.Setup(pm => pm.GetTransformer(It.IsAny<string>()))
            .Returns(mockTransformer.Object);
        mockPluginManager.Setup(pm => pm.GetTarget(It.IsAny<string>()))
            .Returns(mockDataTarget.Object);

        var migrationEngine = new MigrationEngine(mockPluginManager.Object);
        var migrationTask = new MigrationTask
        {
            Source = new SourceConfig { ComponentId = "TestDataSource" },
            Transforms = new List<TransformConfig> { new TransformConfig { ComponentId = "TestTransformer" } },
            Target = new TargetConfig { ComponentId = "TestDataTarget" },
            Options = new ExecutionOptions { MaxDegreeOfParallelism = 1, BatchSize = 1 }
        };

        // Act
        await migrationEngine.RunAsync(migrationTask, CancellationToken.None);

        // Assert
        mockDataSource.Verify(ds => ds.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDataSource.Verify(ds => ds.ExtractAsync(It.IsAny<SourceConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDataSource.Verify(ds => ds.ShutdownAsync(It.IsAny<CancellationToken>()), Times.Once);

        mockTransformer.Verify(t => t.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()), Times.Once);
        mockTransformer.Verify(t => t.TransformAsync(It.IsAny<IAsyncEnumerable<DataRecord>>(), It.IsAny<TransformConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        mockTransformer.Verify(t => t.ShutdownAsync(It.IsAny<CancellationToken>()), Times.Once);

        mockDataTarget.Verify(dt => dt.InitializeAsync(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDataTarget.Verify(dt => dt.LoadAsync(It.IsAny<IAsyncEnumerable<DataRecord>>(), It.IsAny<TargetConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDataTarget.Verify(dt => dt.ShutdownAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
