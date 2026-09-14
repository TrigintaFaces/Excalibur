# Excalibur.Data.DataProcessing

Data processing utilities for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Data.DataProcessing
```

## Features

- Batch processing
- ETL operations
- Data transformation
- Bulk operations

## Tenancy

The data task queue is a global, type-wide sweep: it runs outside any tenant scope, and its enqueue and
drain surfaces (`IDataOrchestrationManager.AddDataTaskForRecordTypeAsync`, the pending-task drain) accept
no tenant identity. If your `IRecordFetcher` records are tenant-owned, your fetcher must apply the tenant
predicate itself -- the queue applies none.

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
