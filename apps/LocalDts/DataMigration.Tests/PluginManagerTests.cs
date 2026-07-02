using DataMigration.Core;
using DataMigration.Contracts;
using Moq;
using System.Reflection;

namespace DataMigration.Tests;

public class PluginManagerTests
{
    [Fact]
    public void LoadPlugins_ShouldLoadFromDirectory()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
        Directory.CreateDirectory(pluginsDirectory);

        try
        {
            // Act
            pluginManager.LoadPlugins(pluginsDirectory);

            // Assert
            var components = pluginManager.ListAllComponents();
            Assert.NotNull(components);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(pluginsDirectory))
            {
                Directory.Delete(pluginsDirectory, true);
            }
        }
    }

    [Fact]
    public void GetDataSource_ShouldThrowKeyNotFoundException_WhenDataSourceNotFound()
    {
        // Arrange
        var pluginManager = new PluginManager();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => pluginManager.GetDataSource("NonExistentDataSource"));
    }

    [Fact]
    public void GetTransformer_ShouldThrowKeyNotFoundException_WhenTransformerNotFound()
    {
        // Arrange
        var pluginManager = new PluginManager();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => pluginManager.GetTransformer("NonExistentTransformer"));
    }

    [Fact]
    public void GetTarget_ShouldThrowKeyNotFoundException_WhenTargetNotFound()
    {
        // Arrange
        var pluginManager = new PluginManager();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => pluginManager.GetTarget("NonExistentTarget"));
    }
}
