# Excalibur.Compliance.Postgres

Postgres implementation of GDPR compliance stores for the Excalibur framework.

## Features

- **Erasure Store** - Track and manage GDPR erasure requests with certificate persistence
- **Legal Hold Store** - Manage legal holds that block erasure under Article 17(3)
- **Data Inventory Store** - Maintain data location registrations and discovered personal data

## Quick Start

```csharp
services.AddPostgresErasureStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});

services.AddPostgresLegalHoldStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});

services.AddPostgresDataInventoryStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});
```

## Requirements

- Postgres 12+
- Npgsql driver
