using DataForge.Jobs.Models;
using YamlDotNet.Serialization;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;

namespace DataForge.Jobs.Parsing;

public static class JobYamlParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static JobDefinition Parse(string yaml) => ParseMany(yaml)[0];

    public static JobFile ParseFile(string yaml)
    {
        var document = new YamlStream();
        document.Load(new StringReader(yaml));
        if (document.Documents.Count != 1 || document.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidOperationException("Job YAML requires exactly one mapping document.");
        if (root.Children.TryGetValue(new YamlScalarNode("jobs"), out var jobsNode))
        {
            if (jobsNode is not YamlSequenceNode sequence || sequence.Children.Count == 0)
                throw new InvalidOperationException("jobs must be a non-empty sequence.");
            var file = Deserializer.Deserialize<JobFile>(yaml)
                ?? throw new InvalidOperationException("Job YAML deserialized to null.");
            foreach (var job in file.Jobs)
                ValidateJob(job);
            return file;
        }

        var single = Deserializer.Deserialize<JobDefinition>(yaml)
            ?? throw new InvalidOperationException("Job YAML deserialized to null.");
        ValidateJob(single);
        return new JobFile { Jobs = [single], SourceJob = single };
    }

    public static IReadOnlyList<JobDefinition> ParseMany(string yaml) => ParseFile(yaml).Jobs;

    private static void ValidateJob(JobDefinition job)
    {
        if (job.Sources.Count > 0)
        {
            ValidateSource(job.Sources[0].ToSource());
            for (var i = 1; i < job.Sources.Count; i++)
            {
                ValidateSource(job.Sources[i].ToSource());
                if (string.IsNullOrWhiteSpace(job.Sources[i].On))
                    throw new InvalidOperationException($"Job sources[{i}].on is required for joins.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(job.Source.Type))
                throw new InvalidOperationException("Job source.type is required.");
            ValidateSource(job.Source);
        }

        ValidateSink(job.Sink);
        if (job.Watermark is not null)
        {
            if (string.IsNullOrWhiteSpace(job.Watermark.Column))
                throw new InvalidOperationException("Job watermark.column is required.");
            if (string.IsNullOrWhiteSpace(job.Watermark.StatePath))
                throw new InvalidOperationException("Job watermark.statePath is required.");
        }
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
            case "sqlite":
            case "postgresql":
            case "postgres":
            case "mysql":
                if (string.IsNullOrWhiteSpace(source.Connection))
                    throw new InvalidOperationException($"Job source.connection is required for {source.Type}.");
                if (string.IsNullOrWhiteSpace(source.Table))
                    throw new InvalidOperationException($"Job source.table is required for {source.Type}.");
                break;
            case "excel":
            case "http":
            case "rest":
                if (string.IsNullOrWhiteSpace(source.Path))
                    throw new InvalidOperationException($"Job source.path is required for {source.Type}.");
                break;
            case "parquet":
                if (string.IsNullOrWhiteSpace(source.Path))
                    throw new InvalidOperationException("Job source.path is required for parquet.");
                break;
            case "duckdb":
                if (string.IsNullOrWhiteSpace(source.Path))
                    throw new InvalidOperationException("Job source.path is required for duckdb.");
                if (string.IsNullOrWhiteSpace(source.Query))
                    throw new InvalidOperationException("Job source.query is required for duckdb.");
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
            case "sqlite":
            case "postgresql":
            case "postgres":
            case "mysql":
                if (string.IsNullOrWhiteSpace(sink.Connection))
                    throw new InvalidOperationException($"Job sink.connection is required for {sink.Type}.");
                if (string.IsNullOrWhiteSpace(sink.Table))
                    throw new InvalidOperationException($"Job sink.table is required for {sink.Type}.");
                if (sink.Mode.Equals("upsert", StringComparison.OrdinalIgnoreCase) && sink.Keys.Count == 0)
                    throw new InvalidOperationException("Job sink.keys is required when mode is upsert.");
                break;
            case "parquet":
                if (string.IsNullOrWhiteSpace(sink.Path))
                    throw new InvalidOperationException("Job sink.path is required for parquet.");
                break;
            case "duckdb":
                if (string.IsNullOrWhiteSpace(sink.Path))
                    throw new InvalidOperationException("Job sink.path is required for duckdb (database file).");
                if (string.IsNullOrWhiteSpace(sink.Table))
                    throw new InvalidOperationException("Job sink.table is required for duckdb.");
                break;
            case "excel":
            case "http":
            case "rest":
                if (string.IsNullOrWhiteSpace(sink.Path))
                    throw new InvalidOperationException($"Job sink.path is required for {sink.Type}.");
                break;
            default:
                throw new InvalidOperationException($"Unsupported sink.type '{sink.Type}'.");
        }
    }
}
