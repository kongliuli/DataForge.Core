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

        if (string.IsNullOrWhiteSpace(job.Source.Path))
            throw new InvalidOperationException("Job source.path is required.");

        if (string.IsNullOrWhiteSpace(job.Sink.Type))
            throw new InvalidOperationException("Job sink.type is required.");

        if (string.IsNullOrWhiteSpace(job.Sink.Path))
            throw new InvalidOperationException("Job sink.path is required.");

        return job;
    }
}
