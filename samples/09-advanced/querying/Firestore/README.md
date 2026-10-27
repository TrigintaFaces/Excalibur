# Firestore Data Provider Sample

Demonstrates the full capabilities of `Excalibur.Data.Firestore`, the Google Cloud Firestore persistence provider for the Excalibur framework.

## Capabilities Demonstrated

| Capability | Description |
|---|---|
| **DI Registration** | `AddExcaliburFirestore(Action<IFirestoreDataBuilder>)` builder pattern |
| **Emulator Support** | Local development via `EmulatorHost` without GCP credentials |
| **CRUD Operations** | Create, Read, Update, Delete through `ICloudNativePersistenceProvider` |
| **Query** | Collection-level queries via `QueryAsync` |
| **Real-time Listeners** | Change feed subscriptions using Firestore snapshot listeners |
| **Health Check** | `FirestoreHealthCheck` for readiness/liveness probes |
| **Credentials** | Application Default Credentials, JSON file path, inline JSON |
| **Provider Metadata** | `ICloudNativeProviderInfo`, supported operation types |

## Prerequisites

### Option A: Firestore Emulator via gcloud CLI

1. Install the [Google Cloud SDK](https://cloud.google.com/sdk/docs/install).
2. Install the Firestore emulator component:

   ```bash
   gcloud components install cloud-firestore-emulator
   ```

3. Start the emulator on the port configured in `appsettings.json`:

   ```bash
   gcloud emulators firestore start --host-port=localhost:8086
   ```

### Option B: Firestore Emulator via Docker

```bash
docker run -d \
  --name firestore-emulator \
  -p 8086:8086 \
  google/cloud-sdk:latest \
  gcloud emulators firestore start --host-port=0.0.0.0:8086
```

## Running the Sample

```bash
cd samples/09-advanced/querying/Firestore
dotnet run
```

The sample will:

1. Initialize the Firestore provider (connecting to the emulator).
2. Run a health check.
3. Create, read, update, query, and delete a document.
4. Create a real-time listener subscription on the `items` collection.
5. Insert a document and observe the change event through the listener.
6. Clean up and dispose resources.

## Configuration

Edit `appsettings.json` to change the project ID, default collection, or emulator host:

```json
{
  "Firestore": {
    "ProjectId": "excalibur-sample",
    "DefaultCollection": "items",
    "EmulatorHost": "localhost:8086"
  }
}
```

### Production Configuration

For production use, remove `EmulatorHost` and configure credentials:

```csharp
builder.Services.AddExcaliburFirestore(firestore =>
{
    firestore.ProjectId("my-gcp-project");
    // Option 1: Application Default Credentials (recommended)
    // No extra config -- uses GOOGLE_APPLICATION_CREDENTIALS or metadata server.

    // Option 2: Service account key file
    firestore.CredentialsPath("/secrets/service-account.json");

    // Option 3: Inline JSON (container/secret manager scenarios)
    firestore.CredentialsJson(Environment.GetEnvironmentVariable("GCP_CREDENTIALS_JSON")!);
});
```

## Key Types

| Type | Package | Purpose |
|---|---|---|
| `FirestoreOptions` | `Excalibur.Data.Firestore` | Configuration (project, credentials, emulator, timeouts) |
| `FirestorePersistenceProvider` | `Excalibur.Data.Firestore` | Cloud-native persistence provider implementation |
| `FirestoreHealthCheck` | `Excalibur.Data.Firestore` | Health check for connectivity verification |
| `FirestoreListenerSubscription<T>` | `Excalibur.Data.Firestore` | Real-time change feed via Firestore snapshot listeners |
| `ICloudNativePersistenceProvider` | `Excalibur.Data.Abstractions` | Provider abstraction for CRUD, query, batch, change feed |
| `PartitionKey` | `Excalibur.Data.Abstractions` | Partition/collection routing key |
