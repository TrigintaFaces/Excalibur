# Excalibur.Data.SqlServer — A3 Abstractions Usage (Examples)

Examples only — adjust to your host’s composition.

Register services

```
using Excalibur.Data.SqlServer;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Abstractions.Authorization;

services.AddExcaliburSqlServices();

// IGrantRequestProvider and IActivityGroupGrantService are registered via ServiceCollectionExtensions
```

Consume abstractions

```
public sealed class GrantsController(IGrantRequestProvider grants)
{
    public async Task<IReadOnlyList<Grant>> GetUserGrants(string userId, CancellationToken ct)
        => await grants.GetAllGrantsAsync(userId, ct);
}
```

Notes
- Providers depend on `Excalibur.A3.Abstractions` only. No references to `Excalibur.A3` implementation remain.
- Adapters internally execute existing IDataRequest request objects through the configured pipeline.

