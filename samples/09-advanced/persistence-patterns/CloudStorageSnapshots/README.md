# Cloud Storage Snapshots (Cold Event Store)

**Location:** `samples/09-advanced/persistence-patterns/CloudStorageSnapshots/`

The manual `POST /archive-cycle` endpoint uses one-shot `GetArchiveCandidatesAsync` discovery. Its batch limit counts eligible candidates and it retains no scan continuation. It does not inherit the hosted worker's fair paging behavior.


> **Canonical hot→cold flow :**
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
>   -> IEventStoreArchiveReader.LoadArchiveEventsAsync (candidate tenant and version ceiling)
>   -> IColdEventStore.WriteAsync          (moves old events to S3/Blob/GCS)
>                                          RETURNS the durable low-water mark
>   -> IEventStoreArchive.TombstoneArchivedEventsUpToVersionAsync
>                                          bounded BY that watermark, never by
>                                          the version we asked to archive
>
> GET  /orders/{id}                       -> IEventSourcedRepository.GetByIdAsync
>   -> TieredEventStoreDecorator stitches cold + hot reads
>   -> aggregate rehydrates across the boundary
> ```
>
> The background `EventArchiveService` still runs when tiered storage is
> enabled; `POST /archive-cycle` gives the demo a synchronous trigger so the
> hot→cold boundary can be exercised without waiting for the background timer.

> **Clearing hot payloads: bound it by the watermark.**
>
> `IColdEventStore.WriteAsync` returns the **durable low-water mark** — the highest version
> in the contiguous, durable prefix starting at version zero. It returns `-1` when no
> such prefix exists, even if a suffix was successfully stored. Empty writes return
> `-1` after layout/archive validation; validation failures throw.
>
> Clear hot payloads only up to that returned value, preserving rows and metadata. Clearing up to the version you
> *requested* destroys any event the cold tier did not durably store, and the hot copy was
> the only other one. When the watermark is `-1`, clear nothing and let the next cycle
> retry. `ManualArchiveRunner` shows the safe shape; copy it rather than the shorter version
> that ignores the result.

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

# Let this batch pass the configured one-minute age threshold.
sleep 65

# Add the five fresh events that must remain hot. Run the next step promptly.
curl -X POST "http://localhost:5000/orders/$ORDER_ID/events?count=5"

# Force one archive cycle — old events move from hot (SQL Server) to cold
# (S3 / Blob / GCS). Sample returns { aggregatesArchived, eventsMoved }.
curl -X POST "http://localhost:5000/archive-cycle?batchSize=10"

# Rehydrate — the TieredEventStoreDecorator stitches cold + hot reads
curl "http://localhost:5000/orders/$ORDER_ID"

# Health probe (always available)
curl http://localhost:5000/health
```

Verify `eventsMoved > 0` in the archive response. The background worker may have
archived first; in that case verify its result in storage instead of treating a
zero manual count as proof. In SQL Server, inspect this order's rows in
`dbo.EventStoreEvents`: older versions should have `ArchivedAt` set and
`EventData` cleared, while the five fresh versions retain their payloads.
Then verify the GET response contains all 25 notes. The cleared hot payloads
together with the restored notes establish cold read-through; a full response
without those storage checks does not. The newest five events remain protected even if every event has aged.

## Archive policy

The sample reads `TieredStorage` from configuration, with these demo defaults:

```csharp
// samples/09-advanced/persistence-patterns/CloudStorageSnapshots/Program.cs
es.UseTieredStorage(policy =>
{
    policy.MaxAge            = TimeSpan.FromMinutes(1);  // eligible age threshold
    policy.MaxPosition       = 10_000_000;
    policy.RetainRecentCount = 5;                        // require a newer five-version tail
});
```

Example production policy (choose values for your workload):

```csharp
es.UseTieredStorage(policy =>
{
    policy.MaxAge            = TimeSpan.FromDays(90);   // archive after 90 days
    policy.MaxPosition       = 10_000_000;              // OR global position strictly below this ceiling
    policy.RetainRecentCount = 1000;                    // always keep the last 1000
});
```

Age and global-position limits are alternative triggers. Retention always protects
the newest configured number of events by aggregate version, including when all
events are old. An ineligible earlier payload stops the archive prefix even if a
later event has an older timestamp. Previously archived markers do not consume
the batch limit. With this sample's generous position threshold, waiting is optional;
the walkthrough also works with an age-only policy.

The archive process is idempotent and resumable; events are first copied to the
cold store, verified, and only then have their hot payloads cleared. Rows and
ordering metadata remain in the hot store. The background
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

## Archive layout and operating costs

`ColdStorage:Layout` defaults to `Legacy`. The sample passes it to the selected provider's real `Layout` builder method. After completing the administrative cutover, set `ColdStorage__Layout=TypedV2` to consume the typed namespace. Invalid values fail startup. The sample does not activate or migrate archives automatically while its worker is running.

Follow the [typed archive cutover guidance](../../../../docs-site/docs/event-sourcing/providers.md#moving-an-archive-namespace-to-typedv2): drain and externally fence all Legacy operations, explicitly activate the namespace, migrate each occupied legacy slot through the captured cold-store instance, and retain all original archives, receipts and markers. Even a fresh typed namespace requires activation. A Legacy restart refuses an activated namespace rather than hiding its typed streams.

Providers read, merge and conditionally replace archive objects; they do not physically append bytes to an immutable object. Larger streams therefore increase transfer, serialization and validation costs. Typed migration additionally validates retained history. Keep archives and protocol records readable by normal requests: moving them to a tier requiring an asynchronous restore can make replay unavailable. Lifecycle policies must not remove the retained objects required by migration validation.

The hot event rows retain global membership and archive markers; cold storage restores their payloads. Aggregate snapshots are an optimization, not a substitute for preserving replayable event history.
