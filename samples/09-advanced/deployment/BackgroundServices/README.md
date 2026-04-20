# Background Services Samples

This directory contains code examples for configuring outbox delivery guarantees.

## Examples

| Example | Description |
|---------|-------------|
| [AtLeastOnceWithInbox](./AtLeastOnceWithInbox/) | Default guarantee with inbox deduplication |
| [MinimizedWindow](./MinimizedWindow/) | Per-message completion for smaller failure window |
| [TransactionalWhenApplicable](./TransactionalWhenApplicable/) | Exactly-once delivery with same-database transactions |
| [PerformanceComparison](./PerformanceComparison/) | Performance trade-offs between guarantee levels |

## Quick Start

Each example is a standalone .NET 9 console application:

```bash
cd AtLeastOnceWithInbox
dotnet run
```

## Prerequisites

- .NET 9 SDK
- SQL Server (for outbox/inbox stores)




