using Xunit;

namespace DataForge.SchemaInfer.Tests;

public class SchemaInferEngineTests
{
    [Fact]
    public void InferFromCsv_GeneratesNumericRules()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "schema-infer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var csvPath = Path.Combine(tempDir, "sample.csv");
            File.WriteAllText(csvPath, """
                Amount,OrderId
                10,1
                20,2
                """);

            var rules = SchemaInferEngine.InferFromCsv(csvPath, 100);

            Assert.Contains(rules, r => r.Field == "Amount" && r.Min == 10 && r.Max == 20);
            Assert.Contains(rules, r => r.Field == "OrderId" && r.Required);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
