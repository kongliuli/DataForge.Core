namespace DataForge.Sync.Models;

public sealed class JobDefinition
{
    public string? Name { get; set; }

    public string? Schedule { get; set; }

    public SourceDefinition Source { get; set; } = new();

    public List<TransformStep> Transforms { get; set; } = [];

    public ValidateDefinition? Validate { get; set; }

    public SinkDefinition Sink { get; set; } = new();
}

public sealed class SourceDefinition
{
    public string Type { get; set; } = "csv";

    public string Path { get; set; } = "";

    public string? Connection { get; set; }

    public string? Table { get; set; }

    public string? Query { get; set; }

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

    public List<YamlValidationRule> Rules { get; set; } = [];
}

public sealed class YamlValidationRule
{
    public string Field { get; set; } = "";

    public bool? Required { get; set; }

    public decimal? Min { get; set; }

    public decimal? Max { get; set; }

    public string? Pattern { get; set; }
}

public sealed class SinkDefinition
{
    public string Type { get; set; } = "json";

    public string Path { get; set; } = "";

    public string? Connection { get; set; }

    public string? Table { get; set; }

    public string Mode { get; set; } = "insert";

    public List<string> Keys { get; set; } = [];

    public SinkOptionsDefinition? Options { get; set; }
}

public sealed class SinkOptionsDefinition
{
    public bool? IncludeHeader { get; set; }

    public bool? Indented { get; set; }

    public int? BatchSize { get; set; }
}
