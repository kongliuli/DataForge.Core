## Question

### Description

[Please include a clear and concise description of your question]

### Context

[Provide context about what you're trying to achieve]

### Scenario

[Describe your specific use case or scenario]

```csharp
// Include your current code
var result = await DataForgePipeline
    .FromCsv<Order>("orders.csv")
    .Where(o => o.Amount > 1000)
    // Your question is about...
    .ToCsv("output.csv");
```

### What I've Tried

[Describe any solutions you've already tried]

### Additional Information

- **.NET Version**: [e.g., 8.0.100]
- **DataForge.Core Version**: [e.g., 0.1.0]

---

## Labels

- `question` - Usage question or how-to inquiry
- `documentation` - Needs documentation clarification
- `needs-response` - Awaiting community response
