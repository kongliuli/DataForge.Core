using DataForge.Web;

namespace DataForge.Web.Tests;

public class QueuedProgressTests
{
    [Fact]
    public void ConcurrentReports_AreAvailableWithoutCallbackDrainAndAppearExactlyOnce()
    {
        var progress = new QueuedProgress<int>();
        Parallel.For(0, 10000, value => progress.Report(value));
        var received = new HashSet<int>();
        while (progress.TryDequeue(out var value))
            Assert.True(received.Add(value));
        Assert.Equal(10000, received.Count);
    }
}
