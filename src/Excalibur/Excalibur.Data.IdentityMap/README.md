# Excalibur.Data.IdentityMap

Aggregate identity map abstractions for Excalibur, providing external-to-internal ID resolution for CDC ingestion and anti-corruption layers.

## Overview

The identity map store provides a write-side authoritative mapping between external system identifiers (e.g., legacy transaction IDs, ERP account numbers) and internal aggregate IDs. This enables:

- **Idempotent CDC ingestion** -- detect whether an external record has already been imported
- **Cross-aggregate reference resolution** -- resolve related entities (Account, Client, Branch) by their external keys
- **Anti-corruption layer** -- decouple domain model IDs from external system IDs

## Usage

```csharp
// Register with SQL Server provider
services.AddIdentityMap(identity =>
{
    identity.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("dbo")
           .TableName("IdentityMap");
    });
});

// Resolve an external ID to an aggregate ID
Guid? orderId = await identityMap.ResolveAsync<Guid>(
    "LegacyCore", legacyOrderId, "Order", cancellationToken);

// Bind a new mapping (idempotent)
var result = await identityMap.TryBindAsync(
    "LegacyCore", legacyOrderId, "Order",
    newOrderId.ToString(), cancellationToken);

if (result.WasCreated)
{
    // New aggregate -- proceed with creation
}
else
{
    // Existing aggregate -- route to update/reconcile
}
```

## Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Data.IdentityMap` | Core abstractions, builder, InMemory implementation |
| `Excalibur.Data.IdentityMap.SqlServer` | SQL Server provider using Dapper |
