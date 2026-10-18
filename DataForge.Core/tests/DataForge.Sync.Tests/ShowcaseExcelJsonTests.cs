using ClosedXML.Excel;
using DataForge.Jobs.Excel;
using DataForge.Jobs.Execution;
using System.Text.Json;
using Xunit;

namespace DataForge.Sync.Tests;

/// <summary>
/// Runs the four Excel↔JSON jobs shipped in the dotnet-new showcase template
/// (jobs/10-homo, 11-split, 12-merge, 13-hetero) from a pristine copy, so the
/// documented examples stay executable. The copy deliberately omits the
/// <c>output/</c> directory to prove sinks create their target directory.
/// </summary>
public class ShowcaseExcelJsonTests
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterProviders() => JobExcelRegistration.AddDataForgeJobsExcel();

    [Fact]
    public async Task Showcase_ExcelJsonJobs_RunFromCleanCopy()
    {
        var showcase = ShowcaseRoot();
        var dir = Path.Combine(Path.GetTempPath(), "df-showcase-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "jobs", "samples" })
                CopyDirectory(Path.Combine(showcase, name), Path.Combine(dir, name));
            File.Copy(Path.Combine(showcase, "sample.csv"), Path.Combine(dir, "sample.csv"));

            // 1) homogeneous Excel → JSON
            var homo = await new JobRunner().RunAsync(Path.Combine(dir, "jobs", "10-homo-excel-json.yaml"));
            Assert.True(homo.Success, homo.ErrorMessage);
            Assert.Equal(5, homo.ExportResults!.RecordsWritten);

            // 2) one Excel flat table → two JSON files
            var split = await JobBatch.RunFileAsync(Path.Combine(dir, "jobs", "11-split-excel-json.yaml"));
            Assert.Equal(2, split.Count);
            Assert.All(split, result => Assert.True(result.Success, result.ErrorMessage));

            // 3) two JSON tables joined → Excel
            var merge = await new JobRunner().RunAsync(Path.Combine(dir, "jobs", "12-merge-json-excel.yaml"));
            Assert.True(merge.Success, merge.ErrorMessage);
            Assert.Equal(4, merge.ExportResults!.RecordsWritten);

            // 4) heterogeneous clean: filter + rename Excel → JSON
            var hetero = await new JobRunner().RunAsync(Path.Combine(dir, "jobs", "13-hetero-excel-json.yaml"));
            Assert.True(hetero.Success, hetero.ErrorMessage);
            Assert.Equal(4, hetero.ExportResults!.RecordsWritten);

            // The output directory did not exist in the pristine copy.
            var outDir = Path.Combine(dir, "output", "excel-json");
            Assert.True(Directory.Exists(outDir), $"Expected {outDir} to be created by the sinks.");

            var cleanJson = await File.ReadAllTextAsync(Path.Combine(outDir, "sales-clean.json"));
            Assert.Equal(4, JsonDocument.Parse(cleanJson).RootElement.GetArrayLength());
            Assert.DoesNotContain("cancelled", cleanJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Buyer\"", cleanJson, StringComparison.Ordinal);

            var headers = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outDir, "order-headers.json")));
            Assert.Equal(5, headers.RootElement.GetArrayLength());
            Assert.True(headers.RootElement[0].TryGetProperty("Region", out _));

            using var book = new XLWorkbook(Path.Combine(outDir, "customer-orders.xlsx"));
            var sheet = book.Worksheet(1);
            Assert.Equal("Buyer", sheet.Cell(1, 1).GetString());
            Assert.Equal("OrderId", sheet.Cell(1, 2).GetString());
            Assert.Equal("OrderTotal", sheet.Cell(1, 3).GetString());
            Assert.Equal(5, sheet.RangeUsed()!.RowCount()); // header + 4 joined rows
            Assert.Equal("Ada", sheet.Cell(2, 1).GetString());
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // best effort cleanup
            }
        }
    }

    private static string ShowcaseRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DataForge.Core.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "templates", "DataForge.Templates", "content", "dataforge-showcase");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var sub in Directory.GetDirectories(source))
            CopyDirectory(sub, Path.Combine(destination, Path.GetFileName(sub)));
    }
}
