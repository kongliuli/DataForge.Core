namespace DataForge.Sync.Models;

public sealed class JobDefinition
{
    public string? Name { get; set; }

    public SourceDefinition Source { get; set; } = new();

    public List<TransformStep> Transforms { get; set; } = [];

    public ValidateDefinition? Validate { get; set; }

    public SinkDefinition Sink { get; set; } = new();
}

public sealed class SourceDefinition
{
    public string Type { get; set; } = "csv";

    public string Path { get; set; } = "";

    public SourceOptionsDefinition? Options { get; set; }
}

public sealed class SourceOptionsDefinition
{
    public bool? HasHeader { get; set; }

    public char? Delimiter { get; set; }
}

public sealed class TransformStep
{
    public string? Where { get; set; }

    public Dictionary<string, string>? Select { get; set; }
}

public sealed class ValidateDefinition
{
    public string OnError { get; set; } = "fail";

    public string? BadRowOutput { get; set; }
}

public sealed class SinkDefinition
{
    public string Type { get; set; } = "json";

    public string Path { get; set; } = "";

    public SinkOptionsDefinition? Options { get; set; }
}

public sealed class SinkOptionsDefinition
{
    public bool? IncludeHeader { get; set; }

    public bool? Indented { get; set; }
}
