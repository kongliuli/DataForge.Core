using DataForge.Core.Core.Models;
using System.Text.Json;

namespace DataForge.Core.Core.Pipeline;

internal static class BadRowExporter
{
    public static async Task WriteAsync(string filePath, IReadOnlyList<RowError> errors, CancellationToken cancellationToken = default)
    {
        await using var stream = File.Create(filePath);
        foreach (var error in errors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = JsonSerializer.Serialize(error);
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(line + "\n"), cancellationToken).ConfigureAwait(false);
        }
    }
}
