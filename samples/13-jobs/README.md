# 13 - Jobs

Demonstrates the Excalibur Jobs framework for background processing, scheduled tasks, and CDC (Change Data Capture) pipelines.

## Projects

| Project | Description |
|---------|-------------|
| [CdcJobQuartz](CdcJobQuartz/) | CDC pipeline from SQL Server using Quartz.NET job scheduling, with anti-corruption layer, domain projections, and Docker Compose infrastructure |

## What You'll Learn

- Setting up CDC change capture from SQL Server
- Using Quartz.NET with `Excalibur.Jobs` for scheduled processing
- Implementing the anti-corruption layer pattern for legacy data integration
- Building materialized view projections from CDC events
- Docker Compose setup for SQL Server + application

## Prerequisites

- .NET 9.0+ SDK
- Docker (for SQL Server via `docker-compose.yml`)

## Quick Start

```bash
cd CdcJobQuartz
docker compose up -d   # Start SQL Server
dotnet run
```

## Related Docs

- [CDC Troubleshooting](../../docs-site/docs/operations/cdc-troubleshooting.md)
- [Data Processing](../../docs-site/docs/patterns/data-processing.md)
