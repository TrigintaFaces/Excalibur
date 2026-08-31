# Excalibur.Compliance.MongoDb

MongoDB storage for the GDPR compliance store — consent records, erasure logs, and subject-access
request tracking. `Excalibur.Compliance` provides the compliance services themselves; install this
package only when you want those records persisted in MongoDB.

## Why this is a separate package

This is the **only** package that brings `MongoDB.Driver` into your dependency graph. A consumer who
stores compliance records in SQL Server, Postgres, or memory never resolves the MongoDB driver.

## Installation

```bash
dotnet add package Excalibur.Compliance.MongoDb
```

## Quick Start

```csharp
services.AddMongoDbComplianceStore(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "compliance";
    options.CollectionPrefix = "dispatch_";
});
```

Or bind from configuration:

```csharp
services.AddMongoDbComplianceStore(configuration.GetSection("MongoDbCompliance"));
```

Either overload registers `IComplianceStore`. Collections are created on first use as
`{prefix}consent_records`, `{prefix}erasure_logs`, and `{prefix}subject_access_requests`.

## Multi-tenancy

The store requires an `ITenantContext` and partitions every document by tenant: the tenant participates
in the document key, which is the upsert conflict target, so two tenants recording data for the same
subject cannot collapse onto one document. A single-tenant host receives the framework's default
context and operates as the one canonical tenant. Registration emits the tenant-scoping capability
marker from the same act that injects the context, so an unwired store cannot advertise scoping it
does not perform.

## Documentation

See the [main documentation](https://github.com/TrigintaFaces/Excalibur) for detailed guides and API reference.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.
