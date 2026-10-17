using System.Text.Json;

namespace DataForge.Jobs.Scheduling;

/// <summary>JSON registration storage. Files can contain secrets; the host controls access permissions.</summary>
public static class ScheduleFileStore
{
    public static async Task SaveAsync(string path, IReadOnlyList<ScheduleRegistration> registrations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, registrations, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>Missing files represent an empty store; malformed files raise JsonException.</summary>
    public static async Task<IReadOnlyList<ScheduleRegistration>> LoadAsync(string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileStream stream;
        try { stream = File.OpenRead(path); }
        catch (FileNotFoundException) { return Array.Empty<ScheduleRegistration>(); }
        catch (DirectoryNotFoundException) { return Array.Empty<ScheduleRegistration>(); }
        await using (stream)
        {
            return await JsonSerializer.DeserializeAsync<ScheduleRegistration[]>(stream,
                cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("Schedule store must contain an array, not null.");
        }
    }
}
