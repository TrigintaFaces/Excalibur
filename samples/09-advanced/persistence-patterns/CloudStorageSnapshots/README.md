# Cloud Storage Snapshots (Cold Event Store)

**Beads:** `bd-h9hr4c` (P2, S789 provider reference) + `bd-jacsb2` (P1, S790 hot/cold flow)
**Location:** `samples/09-advanced/persistence-patterns/CloudStorageSnapshots/`

> **Canonical hot→cold flow (Sprint 790):**
>
> ```
> POST /orders                            -> CreateOrderCommand
>   -> CreateOrderHandler
>   -> IEventSourcedRepository.SaveAsync  (hot store = SQL Server)
>
> POST /orders/{id}/events?count=N        -> AppendOrderNotesCommand
>   -> AppendOrderNotesHandler            (appends N notes; aggregate grows)
>
> POST /archive-cycle                     -> ManualArchiveRunner
>   -> IEventStoreArchive.GetArchiveCandidatesAsync(ArchivePolicy)
>   -> IColdEventStore.WriteAsync          (moves old events to S3/Blob/GCS)
>   -> IEventStoreArchive.DeleteEventsUpToVersionAsync
>
> GET  /orders/{id}                       -> IEventSourcedRepository.GetByIdAsync
>   -> TieredEventStoreDecorator stitches cold + hot reads
>   -> aggregate rehydrates across the boundary
> ```
>
> The background `EventArchiveService` still runs when tiered storage is
> enabled; `POST /archive-cycle` gives the demo a synchronous trigger so the
> hot→cold boundary can be exercised without waiting for the background timer.

Demonstrates tiered event-sourcing storage: hot events live in a SQL Server
event store; once they age past the archive policy they roll into an object
storage cold store. Three providers are wired side-by-side so you can compare
configuration surface:

| Provider | Package | Builder method |
|----------|---------|----------------|
| **AWS S3** | `Excalibur.EventSourcing.AwsS3` | `es.UseAwsS3ColdEventStore(s3 => ...)` |
| **Azure Blob** | `Excalibur.EventSourcing.AzureBlob` | `es.UseAzureBlobColdEventStore(blob => ...)` |
| **Google Cloud Storage** | `Excalibur.EventSourcing.Gcs` | `es.UseGcsColdEventStore(gcs => ...)` |

## Architecture

```
┌────────────────────────────────────────────────────────┐
│  AggregateRoot -> IEventSourcedRepository              │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│  TieredEventStore                                       │
│  ┌──────────────────┐        ┌──────────────────────┐   │
│  │ Hot Store        │        │ Archive Policy        │   │
│  │  SqlServer       │        │  MaxAge               │   │
│  │  (recent events) │ -----> │  MaxPosition          │   │
│  │                  │  move  │  RetainRecentCount    │   │
│  └──────────────────┘        └──────────┬───────────┘   │
│                                          │               │
│                                          ▼               │
│  ┌──────────────────────────────────────────────────┐    │
│  │  Cold Store (pick one)                            │    │
│  │     AwsS3ColdEventStore                          │    │
│  │   | AzureBlobColdEventStore                      │    │
│  │   | GcsColdEventStore                            │    │
│  └──────────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────┘
```

## Run the sample

### 1. Pick a cold-store provider

```bash
# AWS S3 cold store  (uses default AWS credentials from env / ~/.aws)
PROVIDER=aws   dotnet run

# Azure Blob cold store  (uses Azurite by default: UseDevelopmentStorage=true)
PROVIDER=azure dotnet run

# Google Cloud Storage cold store  (requires GCS credentials)
PROVIDER=gcs   dotnet run
```

### 2. Exercise the hot→cold flow

Once the host is up:

```bash
# Create an order (initial OrderCreated event lands in the hot store)
ORDER_ID=$(curl -s -X POST http://localhost:5000/orders | jq -r .orderId)

# Append many note events so the aggregate grows beyond RetainRecentCount
curl -X POST "http://localhost:5000/orders/$ORDER_ID/events?count=20"

# Force one archive cycle — old events move from hot (SQL Server) to cold
# (S3 / Blob / GCS). Sample returns { aggregatesArchived, eventsMoved }.
curl -X POST "http://localhost:5000/archive-cycle?batchSize=10"

# Rehydrate — the TieredEventStoreDecorator stitches cold + hot reads
curl "http://localhost:5000/orders/$ORDER_ID"

# Health probe (always available)
curl http://localhost:5000/health
```

The rehydrate response is the full note list — the fact that you see notes
older than the archive cutoff proves the cold store was read through and
merged with the hot store.

## Archive policy

The sample uses an aggressive policy so one demo run exercises the boundary
without waiting:

```csharp
// samples/09-advanced/persistence-patterns/CloudStorageSnapshots/Program.cs
es.UseTieredStorage(policy =>
{
    policy.MaxAge            = TimeSpan.FromMinutes(1);  // sample: move anything >1 minute
    policy.MaxPosition       = 10_000_000;
    policy.RetainRecentCount = 5;                        // keep only the last 5 events hot
});
```

Production-style defaults (90d / 1M retained / position-based rollover):

```csharp
es.UseTieredStorage(policy =>
{
    policy.MaxAge            = TimeSpan.FromDays(90);   // archive after 90 days
    policy.MaxPosition       = 10_000_000;              // or after 10M events
    policy.RetainRecentCount = 1000;                    // always keep the last 1000
});
```

The archive process is idempotent and resumable; events are first copied to the
cold store, verified, and only then removed from the hot store. The background
`EventArchiveService` runs on a timer; the sample's `POST /archive-cycle` uses
the same `IEventStoreArchive` + `IColdEventStore` primitives to force a cycle
synchronously for demo visibility.

## Provider-specific surface

### AWS S3

```csharp
es.UseAwsS3ColdEventStore(s3 =>
{
    s3.BucketName("excalibur-cold-events")
      .KeyPrefix("events/")
      .Region("us-east-1")
      // Optional: use LocalStack or MinIO
      .ServiceUrl("http://localhost:4566");
});
```

### Azure Blob Storage

```csharp
es.UseAzureBlobColdEventStore(blob =>
{
    blob.ConnectionString("DefaultEndpointsProtocol=https;AccountName=...")
        .ContainerName("cold-events")
        .CreateContainerIfNotExists();
});
```

### Google Cloud Storage

```csharp
es.UseGcsColdEventStore(gcs =>
{
    gcs.ProjectId("my-gcp-project")
       .BucketName("excalibur-cold-events")
       .ObjectPrefix("events/")
       .CredentialsPath("/secrets/gcs-sa.json");
});
```

## Cost / performance notes

| Provider | Typical use | Notes |
|----------|-------------|-------|
| AWS S3 | Highest durability (11 9s), tiered pricing (Standard / IA / Glacier) | Use lifecycle rules to move objects to IA/Glacier after 30/90 days |
| Azure Blob | Integrated with Entra ID, Hot/Cool/Archive tiers | Cheapest when your compute already runs on Azure |
| GCS | Strong consistency on object creation, regional / multi-regional | Nearline/Coldline/Archive tiers mirror S3 |

All three providers are **append-only**: archived events are never mutated.
Snapshots in the hot store remain the source of truth for aggregate
rehydration; the cold store serves catch-up reads and compliance archives.
