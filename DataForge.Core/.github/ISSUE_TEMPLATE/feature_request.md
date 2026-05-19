## Feature Request

### Description

[Please include a clear and concise description of the feature]

### Problem Statement

[Describe the problem this feature would solve]

### Proposed Solution

[Describe your proposed solution]

### Use Case

[Describe a specific use case for this feature]

```csharp
// If applicable, show an example of how the feature would be used
var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    // New feature would be used here
    .ToCsv("output.csv");
```

### Expected API

[Describe the expected API design]

```csharp
// Example API signature
public static IDataPipeline<TIn, TOut> NewFeature<T>(this IDataPipeline pipeline, ...);
```

### Alternatives Considered

[Describe any alternative solutions you've considered]

### Additional Context

[Add any other context about the feature request here]

### Related Issues

[Link to any related issues]

---

## Labels

- `enhancement` - New feature or request
- `help-wanted` - Extra attention is needed
- `needs-design` - Needs API design discussion
- `priority-high` - High priority feature
- `priority-medium` - Medium priority feature
- `priority-low` - Low priority feature
