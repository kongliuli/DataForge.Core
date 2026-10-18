using DataForge.Jobs.Execution;
using DataForge.Jobs.Models;
using Xunit;

namespace DataForge.Sync.Tests;

public class JoinVariableResolutionTests
{
    [Fact]
    public void Apply_ResolvesQueryForEveryJoinSource()
    {
        var job = new JobDefinition
        {
            Sources =
            [
                new() { Type = "duckdb", Path = "a.db", Query = "select * from ${TABLE}" },
                new() { Type = "duckdb", Path = "b.db", Query = "select * from ${TABLE}", On = "Id = Id" }
            ]
        };
        VariableResolver.Apply(job, new Dictionary<string, string> { ["TABLE"] = "orders" });
        Assert.All(job.Sources, source =>
        {
            Assert.Equal("select * from orders", source.Query);
            Assert.Equal("select * from orders", source.ToSource().Query);
        });
    }
}
