# CDC Anti-Corruption Layer Example

This example demonstrates the **Anti-Corruption Layer (ACL)** pattern for Change Data Capture (CDC) integration using the Excalibur framework.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         CDC Anti-Corruption Layer                        │
└─────────────────────────────────────────────────────────────────────────┘

   Legacy Database                    Your Domain
   ┌──────────────┐                   ┌─────────────┐
   │ LegacyCustomers │                │   Domain    │
   │   (CDC enabled) │                │   Model     │
   └───────┬────────┘                 └──────▲──────┘
           │                                  │
           │ CDC Events                       │ Commands
           ▼                                  │
   ┌──────────────────────────────────────────┴────────────────────┐
   │                  Anti-Corruption Layer (ACL)                   │
   │                                                                │
   │  ┌────────────────────┐      ┌────────────────────────────┐   │
   │  │  DataChangeEvent   │──────▶│  LegacySchemaAdapter       │   │
   │  │  (raw CDC)         │      │  • Column name mapping     │   │
   │  └────────────────────┘      │  • Type conversion         │   │
   │                              │  • Default values          │   │
   │                              └─────────────┬──────────────┘   │
   │                                            │                   │
   │                                            ▼                   │
   │                              ┌────────────────────────────┐   │
   │                              │  CustomerSyncHandler       │   │
   │                              │  • Boundary validation     │   │
   │                              │  • Command translation     │   │
   │                              │  • CDC op → Domain op      │   │
   │                              └─────────────┬──────────────┘   │
   └────────────────────────────────────────────┼──────────────────┘
                                                │
                                                ▼
   ┌────────────────────────────────────────────────────────────────┐
   │                       Dispatcher                               │
   │  SyncCustomerCommand │ UpdateCustomerCommand │ DeactivateCmd   │
   └────────────────────────────────────────────────────────────────┘

   Optional Gap-Recovery Lane (when CDC history is incomplete)
   ┌────────────────────────────────────────────────────────────────┐
   │ Excalibur.Data.DataProcessing                                 │
   │  CustomerHistoryBackfillProcessor + CustomerHistoryRecordHandler│
   │  replay historical snapshots through the same domain commands │
   └────────────────────────────────────────────────────────────────┘
```

## Why Anti-Corruption Layer?

When integrating with external systems via CDC, you face several challenges:

1. **Schema Evolution**: Legacy systems change column names over time
2. **Data Format Differences**: External data may use different types/formats
3. **Domain Isolation**: Your domain shouldn't know about legacy quirks
4. **Validation Boundary**: External data needs validation before entering domain

The ACL pattern solves these by placing a translation layer between external data and your domain.

## Key Components

### 1. Schema Adapter (`LegacyCustomerSchemaAdapter`)

Handles schema evolution by mapping legacy column names:

```csharp
// Supports multiple schema versions
private static readonly Dictionary<string, string> ColumnMappings = new()
{
    ["CustomerName"] = "Name",      // V1 → Current
    ["CustId"] = "ExternalId",      // V1 → Current
    ["CustomerEmail"] = "Email",    // V1 → Current
};
```

### 2. CDC Handler (`CustomerSyncHandler`)

Translates CDC operations to domain commands:

| CDC Operation | Domain Command |
|--------------|----------------|
| INSERT | `SyncCustomerCommand` |
| UPDATE | `UpdateCustomerCommand` |
| DELETE | `DeactivateCustomerCommand` |

```csharp
public async Task HandleAsync(DataChangeEvent changeEvent, CancellationToken ct)
{
    // 1. Adapt schema (handle evolution)
    var adaptedData = _schemaAdapter.Adapt(changeEvent);

    // 2. Validate at boundary
    if (!IsValid(adaptedData)) return;

    // 3. Create command (anti-corruption translation)
    var command = changeEvent.ChangeType switch
    {
        DataChangeType.Insert => new SyncCustomerCommand(adaptedData),
        DataChangeType.Update => new UpdateCustomerCommand(adaptedData),
        DataChangeType.Delete => new DeactivateCustomerCommand(adaptedData),
        _ => null,
    };

    // 4. Dispatch through pipeline
    if (command is not null)
    {
        await _dispatcher.DispatchAsync(command, ct);
    }
}
```

### 3. Domain Commands

Clean, domain-focused commands that don't know about CDC:

- `SyncCustomerCommand` - Create/sync a new customer
- `UpdateCustomerCommand` - Update existing customer
- `DeactivateCustomerCommand` - Soft-delete (not hard delete)

### 4. Historical Replay for Missing CDC History (`Excalibur.Data.DataProcessing`)

When CDC retention/windows are insufficient, this sample also shows a replay lane:

1. Fetch historical snapshots from a legacy source.
2. Process in batches using `DataProcessor<TRecord>`.
3. Re-dispatch through `SyncCustomerCommand` so business rules remain consistent.

```csharp
builder.Services.AddSingleton<IOptions<DataProcessingConfiguration>>(
    Options.Create(new DataProcessingConfiguration
    {
        QueueSize = 128,
        ProducerBatchSize = 50,
        ConsumerBatchSize = 20
    }));
builder.Services.AddSingleton<ILegacyCustomerSnapshotSource, InMemoryLegacyCustomerSnapshotSource>();
builder.Services.AddDataProcessor<CustomerHistoryBackfillProcessor>();
builder.Services.AddRecordHandler<CustomerHistoryRecordHandler, LegacyCustomerSnapshot>();
```

## Schema Evolution Support

The adapter handles multiple schema versions automatically:

| Schema Version | Columns | Status |
|---------------|---------|--------|
| V1 (Legacy) | CustomerName, CustId, Email | Supported |
| V2 | Name, ExternalId, Email, Phone | Supported |
| V3 (Current) | Name, ExternalId, Email, Phone, IsActive | Current |

```csharp
// Adapter tries multiple column names in order
var name = GetValue<string>(changeEvent, "Name", "CustomerName");
var id = GetValue<string>(changeEvent, "ExternalId", "CustId", "Id");
```

## Running the Example

```bash
# Build the example
dotnet build examples/CdcAntiCorruption

# Run the example
dotnet run --project examples/CdcAntiCorruption
```

Expected output:

```
CDC Anti-Corruption Layer Example
==================================

Simulating CDC events...

1. INSERT event (Legacy V1 schema: CustomerName, CustId)
   → SyncCustomerCommand: Created customer {guid} for external ID CUST-001

2. UPDATE event (Current schema: Name, ExternalId)
   → UpdateCustomerCommand: Updated customer CUST-001, Name: John D. Smith

3. DELETE event (Soft delete → Deactivate)
   → DeactivateCustomerCommand: Soft-deleted customer CUST-001

4. CDC gap recovery (historical replay via Excalibur.Data.DataProcessing)
   → Backfill SyncCustomerCommand: Replayed historical customer CUST-000
   → Backfill SyncCustomerCommand: Replayed historical customer CUST-001
   → Backfill SyncCustomerCommand: Replayed historical customer CUST-002
   → Replayed 3 historical records to close missing CDC history
```

## Configuration

Register the ACL services in your DI container:

```csharp
// Program.cs or Startup.cs
services.AddDispatch();
services.AddCdcAntiCorruptionLayer();
```

`AddCdcAntiCorruptionLayer()` registers:
- `ILegacyCustomerSchemaAdapter` (singleton)
- `IDataChangeHandler` (scoped)

Sample-specific replay registration adds:
- `CustomerHistoryBackfillProcessor` (data processor for replay)
- `CustomerHistoryRecordHandler` (maps historical records to domain commands)

## Best Practices

1. **Always use commands, not domain events**: CDC INSERT isn't the same as a domain "CustomerCreated" event. The ACL translates to commands that go through your business logic.

2. **Soft-delete on CDC DELETE**: Don't hard-delete in your domain just because the legacy system did. Use deactivation to maintain audit trails.

3. **Validate at the boundary**: The ACL should reject invalid data before it reaches your domain.

4. **Log schema adaptation failures**: Unknown schema versions should be logged for investigation.

5. **Handle missing fields gracefully**: Provide sensible defaults for fields that didn't exist in older schemas.

## Related Patterns

- **Functional Tests**: See `tests/functional/Excalibur.Dispatch.Tests.Functional/Workflows/CdcMessageTranslation/`
- **Schema Evolution Tests**: See `tests/functional/Excalibur.Dispatch.Tests.Functional/Workflows/CdcSchemaEvolution/`
- **Error Recovery Tests**: See `tests/functional/Excalibur.Dispatch.Tests.Functional/Workflows/CdcErrorRecovery/`

## License

See LICENSE files in the repository root.
