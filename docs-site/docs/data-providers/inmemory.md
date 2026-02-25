---
sidebar_position: 11
title: In-Memory
description: In-memory data provider for unit testing and local development.
---

# In-Memory Provider

The In-Memory provider implements `IPersistenceProvider` for unit testing and local development. It stores data in memory with optional transaction support, allowing tests to run without external database dependencies.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [data access](../data-access/index.md) and [testing](../testing/test-harness.md)

## Installation

```bash
dotnet add package Excalibur.Data.InMemory
```

**Dependencies:** `Excalibur.Data.Abstractions`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

// Configure options and register in-memory provider for testing
services.Configure<InMemoryProviderOptions>(options => options.Name = "test");
services.AddSingleton<IPersistenceProvider, InMemoryPersistenceProvider>();
```

## Use Cases

| Scenario | Benefit |
|----------|---------|
| Unit tests | No database infrastructure required |
| Integration tests | Fast, isolated test execution |
| Local development | Quick startup without Docker |
| CI/CD pipelines | No external service dependencies |

## Testing Pattern

```csharp
public class OrderServiceTests
{
    private readonly IServiceProvider _services;

    public OrderServiceTests()
    {
        var collection = new ServiceCollection();
        collection.Configure<InMemoryProviderOptions>(options => options.Name = "test");
        collection.AddSingleton<IPersistenceProvider, InMemoryPersistenceProvider>();
        collection.AddTransient<OrderService>();
        _services = collection.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateOrder_ShouldPersist()
    {
        var service = _services.GetRequiredService<OrderService>();
        var order = await service.CreateOrderAsync(new CreateOrderCommand("item-1"), CancellationToken.None);
        order.ShouldNotBeNull();
    }
}
```

## Limitations

- Data is lost when the process exits
- No query optimization or indexing
- Single-process only (no distributed scenarios)
- Not suitable for performance testing

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [SQL Server Provider](./sqlserver.md) — Production SQL provider
- [Testing Patterns](../advanced/index.md) — Testing best practices
