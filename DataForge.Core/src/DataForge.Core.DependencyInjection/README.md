# DataForge.Core.DependencyInjection

Microsoft.Extensions.DependencyInjection integration for `DataForge.Core`.

```csharp
using DataForge.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDataForge();
var provider = services.BuildServiceProvider();
```

## Notes

- `AddDataForge` registers the core DataForge services used by host applications.
- Adapter packages (`DataForge.Core.Excel`, `DataForge.Jobs.*`, etc.) still need to be referenced and registered separately.
