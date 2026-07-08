using DataForge.Sync.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DataForge.Sync.Parsing;

public static class JobYamlParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static JobDefinition Parse(string yaml)
    {
        var job = Deserializer.Deserialize<JobDefinition>(yaml)
            ?? throw new InvalidOperationException("Job YAML deserialized to null.");

        if (string.IsNullOrWhiteSpace(job.Source.Type))
            throw new InvalidOperationException("Job source.type is required.");

        ValidateSource(job.Source);
        ValidateSink(job.Sink);

        return job;
    }

    private static void ValidateSource(SourceDefinition source)
    {
        switch (source.Type.ToLowerInvariant())
        {
            case "csv":
            case "json":
                if (string.IsNullOrWhiteSpace(source.Path))
                    throw new InvalidOperationException("Job source.path is required for file sources.");
                break;
            case "sqlserver":
                if (string.IsNullOrWhiteSpace(source.Connection))
                    throw new InvalidOperationException("Job source.connection is required for sqlserver.");
                if (string.IsNullOrWhiteSpace(source.Table))
                    throw new InvalidOperationException("Job source.table is required for sqlserver.");
                break;
            default:
                throw new InvalidOperationException($"Unsupported source.type '{source.Type}'.");
        }
    }

    private static void ValidateSink(SinkDefinition sink)
    {
        switch (sink.Type.ToLowerInvariant())
        {
            case "json":
            case "csv":
                if (string.IsNullOrWhiteSpace(sink.Path))
                    throw new InvalidOperationException("Job sink.path is required for file sinks.");
                break;
            case "sqlserver":
                if (string.IsNullOrWhiteSpace(sink.Connection))
                    throw new InvalidOperationException("Job sink.connection is required for sqlserver.");
                if (string.IsNullOrWhiteSpace(sink.Table))
                    throw new InvalidOperationException("Job sink.table is required for sqlserver.");
                if (sink.Mode.Equals("upsert", StringComparison.OrdinalIgnoreCase) && sink.Keys.Count == 0)
                    throw new InvalidOperationException("Job sink.keys is required when mode is upsert.");
                break;
            default:
                throw new InvalidOperationException($"Unsupported sink.type '{sink.Type}'.");
        }
    }
}
