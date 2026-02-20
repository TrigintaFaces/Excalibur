# Google Cloud Functions Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** Google Cloud Functions (Serverless)
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Google Cloud project with Cloud Functions API enabled
- Google Cloud SDK (`gcloud`) installed locally
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

Deploy Excalibur applications to Google Cloud Functions for serverless, event-driven workloads with automatic scaling and Google Cloud integration.

**Use Google Cloud Functions when:**
- Event-driven processing (HTTP, Pub/Sub, Cloud Storage, Firestore)
- Integration with Google Cloud services (BigQuery, Cloud SQL, Firestore)
- Multi-region deployment with global load balancing
- Cost optimization for variable workloads

---

## Quick Start

### HTTP-Triggered Function

```csharp
// Function.cs
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using Excalibur.Dispatch.Abstractions;
using System.Text.Json;

public class DispatchFunction : IHttpFunction
{
    private readonly IDispatcher _dispatcher;

    public DispatchFunction(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task HandleAsync(HttpContext context)
    {
        try
        {
            var command = await JsonSerializer.DeserializeAsync<CreateOrderCommand>(
                context.Request.Body);

            var result = await _dispatcher.DispatchAsync(command, context.RequestAborted);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, result);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }
}
```

### Startup.cs (Dependency Injection)

```csharp
// Startup.cs
using Google.Cloud.Functions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

public class Startup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        // Add Dispatch
        services.AddDispatch(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        // Add handlers
        services.AddScoped<CreateOrderCommandHandler>();

        // Add Cloud SQL connection (optional)
        services.AddSqlServerOutboxStore(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
        });
    }
}
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Cloud.Functions.Hosting" Version="2.2.2" />
    <PackageReference Include="Google.Cloud.Functions.Framework" Version="2.2.0" />

    <PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
    <PackageReference Include="Excalibur.Outbox.SqlServer" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Deploy

```bash
# Install Google Cloud SDK
# https://cloud.google.com/sdk/docs/install

# Deploy HTTP function
gcloud functions deploy dispatch-function \
  --runtime dotnet9 \
  --trigger-http \
  --allow-unauthenticated \
  --entry-point DispatchFunction \
  --region us-central1 \
  --set-env-vars SQL_CONNECTION_STRING="..."

# Test
curl https://us-central1-PROJECT_ID.cloudfunctions.net/dispatch-function \
  -H "Content-Type: application/json" \
  -d '{"orderId":"123"}'
```

---

## Pub/Sub Event Processing

### Pub/Sub Triggered Function

```csharp
// PubSubFunction.cs
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using CloudNative.CloudEvents;
using Excalibur.Dispatch.Abstractions;

public class IntegrationEventProcessor : ICloudEventFunction<MessagePublishedData>
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<IntegrationEventProcessor> _logger;

    public IntegrationEventProcessor(
        IDispatcher dispatcher,
        ILogger<IntegrationEventProcessor> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task HandleAsync(
        CloudEvent cloudEvent,
        MessagePublishedData data,
        CancellationToken cancellationToken)
    {
        var messageData = data.Message?.Data?.ToStringUtf8();
        _logger.LogInformation("Processing Pub/Sub message: {MessageId}", data.Message?.MessageId);

        try
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(messageData);
            await _dispatcher.DispatchAsync(integrationEvent, cancellationToken);

            _logger.LogInformation("Integration event processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process integration event");
            throw;  // Triggers retry
        }
    }
}
```

**Package reference:**

```xml
<PackageReference Include="Google.Events.Protobuf" Version="1.4.0" />
```

**Deploy:**

```bash
# Create Pub/Sub topic
gcloud pubsub topics create integration-events

# Deploy function
gcloud functions deploy pubsub-processor \
  --runtime dotnet9 \
  --trigger-topic integration-events \
  --entry-point IntegrationEventProcessor \
  --region us-central1
```

---

## Cloud Scheduler for Periodic Processing

### Scheduled Outbox Processor

```csharp
// OutboxProcessorFunction.cs
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.Scheduler.V1;
using CloudNative.CloudEvents;
using Excalibur.Dispatch.Abstractions;

public class OutboxProcessorFunction : ICloudEventFunction<SchedulerJobData>
{
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly ILogger<OutboxProcessorFunction> _logger;

    public OutboxProcessorFunction(
        IOutboxProcessor outboxProcessor,
        ILogger<OutboxProcessorFunction> logger)
    {
        _outboxProcessor = outboxProcessor;
        _logger = logger;
    }

    public async Task HandleAsync(
        CloudEvent cloudEvent,
        SchedulerJobData data,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Outbox processor triggered at: {Now}", DateTime.UtcNow);

        try
        {
            var processedCount = await _outboxProcessor.DispatchPendingMessagesAsync(
                cancellationToken);

            _logger.LogInformation("Processed {Count} outbox messages", processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbox processing failed");
            throw;
        }
    }
}
```

**Deploy and schedule:**

```bash
# Deploy function
gcloud functions deploy outbox-processor \
  --runtime dotnet9 \
  --trigger-http \
  --entry-point OutboxProcessorFunction \
  --region us-central1

# Create scheduler job (every 1 minute)
gcloud scheduler jobs create http outbox-processor-schedule \
  --schedule "*/1 * * * *" \
  --uri "https://us-central1-PROJECT_ID.cloudfunctions.net/outbox-processor" \
  --http-method POST \
  --location us-central1
```

---

## Cloud Firestore Event Sourcing

### Firestore Event Store

```csharp
// FirestoreEventStore.cs
using Google.Cloud.Firestore;
using Excalibur.EventSourcing;

public class FirestoreEventStore : IEventStore
{
    private readonly FirestoreDb _db;

    public FirestoreEventStore(string projectId, string databaseId = "(default)")
    {
        _db = FirestoreDb.Create(projectId, databaseId);
    }

    public async Task AppendAsync(
        string aggregateId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var batch = _db.StartBatch();
        var eventsCollection = _db.Collection("events").Document(aggregateId).Collection("events");

        foreach (var @event in events)
        {
            var eventDoc = eventsCollection.Document(@event.EventId.ToString());
            var eventData = new Dictionary<string, object>
            {
                ["aggregateId"] = aggregateId,
                ["eventType"] = @event.EventType,
                ["eventData"] = JsonSerializer.Serialize(@event),
                ["version"] = @event.Version,
                ["occurredAt"] = Timestamp.FromDateTimeOffset(@event.OccurredAt)
            };

            batch.Set(eventDoc, eventData);
        }

        try
        {
            await batch.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            throw new ConcurrencyException($"Concurrency conflict for aggregate {aggregateId}");
        }
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(
        string aggregateId,
        CancellationToken cancellationToken)
    {
        var eventsCollection = _db.Collection("events")
            .Document(aggregateId)
            .Collection("events")
            .OrderBy("version");

        var snapshot = await eventsCollection.GetSnapshotAsync(cancellationToken);

        var events = new List<IDomainEvent>();
        foreach (var document in snapshot.Documents)
        {
            var eventType = Type.GetType(document.GetValue<string>("eventType"));
            var eventData = document.GetValue<string>("eventData");
            var @event = JsonSerializer.Deserialize(eventData, eventType) as IDomainEvent;
            events.Add(@event);
        }

        return events;
    }
}
```

**Startup configuration:**

```csharp
public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
{
    var projectId = Environment.GetEnvironmentVariable("GCP_PROJECT");
    services.AddSingleton<IEventStore>(new FirestoreEventStore(projectId));
}
```

### Firestore Triggers for Projections

```csharp
// ProjectionFunction.cs
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.Firestore.V1;
using CloudNative.CloudEvents;

public class ProjectionFunction : ICloudEventFunction<DocumentEventData>
{
    private readonly IProjectionService _projectionService;

    public ProjectionFunction(IProjectionService projectionService)
    {
        _projectionService = projectionService;
    }

    public async Task HandleAsync(
        CloudEvent cloudEvent,
        DocumentEventData data,
        CancellationToken cancellationToken)
    {
        if (data.Value == null) return;  // Document deleted

        var eventType = data.Value.Fields["eventType"].StringValue;
        var eventData = data.Value.Fields["eventData"].StringValue;

        var type = Type.GetType(eventType);
        var @event = JsonSerializer.Deserialize(eventData, type) as IDomainEvent;

        await _projectionService.ProjectAsync(@event, cancellationToken);
    }
}
```

**Deploy:**

```bash
gcloud functions deploy projection-processor \
  --runtime dotnet9 \
  --trigger-event providers/cloud.firestore/eventTypes/document.create \
  --trigger-resource "projects/PROJECT_ID/databases/(default)/documents/events/{aggregateId}/events/{eventId}" \
  --region us-central1
```

---

## Cloud SQL Integration

### Connection String Configuration

```csharp
// Startup.cs
public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
{
    var connectionString = BuildCloudSqlConnectionString();

    services.AddSqlServerOutboxStore(options =>
    {
        options.ConnectionString = connectionString;
    });
}

private string BuildCloudSqlConnectionString()
{
    var instanceConnectionName = Environment.GetEnvironmentVariable("INSTANCE_CONNECTION_NAME");

    if (!string.IsNullOrEmpty(instanceConnectionName))
    {
        // Cloud SQL Proxy connection (for Cloud Functions)
        return $"Server=/cloudsql/{instanceConnectionName};Database=AppDb;User=sqlserver;Password=...";
    }

    // Direct connection (for local development)
    return Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
}
```

**Deploy with Cloud SQL:**

```bash
gcloud functions deploy your-function \
  --runtime dotnet9 \
  --trigger-http \
  --entry-point DispatchFunction \
  --region us-central1 \
  --set-env-vars INSTANCE_CONNECTION_NAME=PROJECT_ID:REGION:INSTANCE_NAME \
  --set-cloudsql-instances PROJECT_ID:REGION:INSTANCE_NAME
```

---

## Secrets Management

### Secret Manager Integration

```csharp
// Startup.cs
using Google.Cloud.SecretManager.V1;

public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
{
    var projectId = Environment.GetEnvironmentVariable("GCP_PROJECT");
    var connectionString = GetSecret(projectId, "sql-connection-string");

    services.AddSqlServerOutboxStore(options =>
    {
        options.ConnectionString = connectionString;
    });
}

private string GetSecret(string projectId, string secretId)
{
    var client = SecretManagerServiceClient.Create();
    var secretVersionName = new SecretVersionName(projectId, secretId, "latest");

    var response = client.AccessSecretVersion(secretVersionName);
    return response.Payload.Data.ToStringUtf8();
}
```

**Grant access:**

```bash
# Create secret
echo -n "Server=..." | gcloud secrets create sql-connection-string \
  --data-file=-

# Grant function access
gcloud secrets add-iam-policy-binding sql-connection-string \
  --member serviceAccount:PROJECT_ID@appspot.gserviceaccount.com \
  --role roles/secretmanager.secretAccessor
```

---

## Monitoring and Logging

### Cloud Logging Integration

```csharp
public async Task HandleAsync(HttpContext context)
{
    var logger = context.RequestServices.GetRequiredService<ILogger<DispatchFunction>>();

    // Structured logging (appears in Cloud Logging)
    logger.LogInformation("Processing request {RequestId}", context.TraceIdentifier);

    // Error logging with exception details
    try
    {
        await ProcessRequest(context);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Request processing failed for {RequestId}", context.TraceIdentifier);
        throw;
    }
}
```

### Cloud Trace Integration

```csharp
// Startup.cs
using Google.Cloud.Diagnostics.AspNetCore3;

public override void Configure(WebHostBuilder webHostBuilder)
{
    webHostBuilder.ConfigureServices(services =>
    {
        var projectId = Environment.GetEnvironmentVariable("GCP_PROJECT");

        services.AddGoogleDiagnosticsForAspNetCore(projectId, serviceName: "dispatch-function");
    });
}
```

**Package reference:**

```xml
<PackageReference Include="Google.Cloud.Diagnostics.AspNetCore3" Version="5.1.3" />
```

---

## Performance Optimization

### Minimum Instances (Reduce Cold Starts)

```bash
# Set minimum instances
gcloud functions deploy your-function \
  --runtime dotnet9 \
  --trigger-http \
  --entry-point DispatchFunction \
  --region us-central1 \
  --min-instances 1 \
  --max-instances 10
```

**Cost:** ~$0.0000025 per GB-second (always running)
**Benefit:** Near-zero cold starts for first instance

### Memory and CPU Allocation

```bash
# Increase memory (also increases CPU)
gcloud functions deploy your-function \
  --runtime dotnet9 \
  --trigger-http \
  --entry-point DispatchFunction \
  --region us-central1 \
  --memory 512MB \
  --timeout 60s
```

**Memory options:** 128MB, 256MB, 512MB, 1GB, 2GB, 4GB, 8GB

---

## Deployment Automation

### gcloud Configuration File

```yaml
# .gcloudignore
bin/
obj/
.git/
.vs/
*.user
```

```yaml
# cloudbuild.yaml (Cloud Build)
steps:
  # Restore dependencies
  - name: 'mcr.microsoft.com/dotnet/sdk:9.0'
    entrypoint: 'dotnet'
    args: ['restore']

  # Build
  - name: 'mcr.microsoft.com/dotnet/sdk:9.0'
    entrypoint: 'dotnet'
    args: ['build', '-c', 'Release']

  # Deploy function
  - name: 'gcr.io/google.com/cloudsdktool/cloud-sdk'
    entrypoint: 'gcloud'
    args:
      - 'functions'
      - 'deploy'
      - 'dispatch-function'
      - '--runtime=dotnet9'
      - '--trigger-http'
      - '--entry-point=DispatchFunction'
      - '--region=us-central1'
      - '--allow-unauthenticated'

timeout: '600s'
```

**Trigger build:**

```bash
gcloud builds submit --config cloudbuild.yaml
```

### GitHub Actions CI/CD

```yaml
name: Deploy to Google Cloud Functions

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Authenticate to Google Cloud
        uses: google-github-actions/auth@v2
        with:
          credentials_json: ${{ secrets.GCP_SA_KEY }}

      - name: Setup Cloud SDK
        uses: google-github-actions/setup-gcloud@v2

      - name: Deploy function
        run: |
          gcloud functions deploy dispatch-function \
            --runtime dotnet9 \
            --trigger-http \
            --allow-unauthenticated \
            --entry-point DispatchFunction \
            --region us-central1
```

---

## Multi-Region Deployment

### Global Load Balancing

```bash
# Deploy to multiple regions
REGIONS=("us-central1" "europe-west1" "asia-northeast1")

for region in "${REGIONS[@]}"; do
  gcloud functions deploy dispatch-function-$region \
    --runtime dotnet9 \
    --trigger-http \
    --entry-point DispatchFunction \
    --region $region
done

# Create serverless NEGs
for region in "${REGIONS[@]}"; do
  gcloud compute network-endpoint-groups create dispatch-neg-$region \
    --region $region \
    --network-endpoint-type serverless \
    --cloud-function-name dispatch-function-$region
done

# Create backend service
gcloud compute backend-services create dispatch-backend \
  --global \
  --load-balancing-scheme EXTERNAL_MANAGED

# Add backends
for region in "${REGIONS[@]}"; do
  gcloud compute backend-services add-backend dispatch-backend \
    --global \
    --network-endpoint-group dispatch-neg-$region \
    --network-endpoint-group-region $region
done

# Create URL map and forwarding rule
gcloud compute url-maps create dispatch-lb \
  --default-service dispatch-backend

gcloud compute target-http-proxies create dispatch-proxy \
  --url-map dispatch-lb

gcloud compute forwarding-rules create dispatch-forwarding-rule \
  --global \
  --target-http-proxy dispatch-proxy \
  --ports 80
```

---

## Troubleshooting

### Cold Start Issues

```bash
# Check execution metrics
gcloud logging read "resource.type=cloud_function AND resource.labels.function_name=dispatch-function" \
  --limit 50 \
  --format json

# Enable minimum instances
gcloud functions deploy dispatch-function \
  --min-instances 1
```

### Memory Limits

```bash
# Increase memory allocation
gcloud functions deploy dispatch-function \
  --memory 1GB

# Check memory usage in logs
gcloud logging read "resource.type=cloud_function AND jsonPayload.message:memory" \
  --limit 10
```

### Timeout Errors

```bash
# Increase timeout (max 540s)
gcloud functions deploy dispatch-function \
  --timeout 540s
```

---

## Best Practices

### 1. Use Dependency Injection

```csharp
// GOOD: DI via Startup
public class Startup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddDispatch();
    }
}

// BAD: Manual instantiation in function
public class Function : IHttpFunction
{
    public Task HandleAsync(HttpContext context)
    {
        var dispatcher = new Dispatcher();  // Don't do this
    }
}
```

### 2. Handle Retries Gracefully

```csharp
public async Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken ct)
{
    // Check for duplicate delivery
    var messageId = data.Message?.MessageId;
    if (await IsAlreadyProcessed(messageId))
    {
        return;  // Idempotent - already processed
    }

    try
    {
        await ProcessMessage(data, ct);
        await MarkAsProcessed(messageId);
    }
    catch (Exception ex)
    {
        // Log and rethrow for retry
        _logger.LogError(ex, "Processing failed, will retry");
        throw;
    }
}
```

### 3. Use Regional Endpoints

```bash
# GOOD: Regional deployment
gcloud functions deploy dispatch-function \
  --region us-central1

# AVOID: Default region may be far from users
```

---

## Next Steps

- **Azure Functions:** [Azure Functions deployment](azure-functions.md) for multi-cloud
- **AWS Lambda:** [AWS Lambda deployment](aws-lambda.md) for multi-cloud
- **Monitoring:** [Cloud Monitoring integration](../observability/google-cloud-monitoring.md)
- **Security:** [IAM best practices](security-best-practices.md)

---

## See Also

- [AWS Lambda Deployment](aws-lambda.md) - Deploy to AWS Lambda for multi-cloud serverless workloads
- [Azure Functions Deployment](azure-functions.md) - Deploy to Azure Functions for multi-cloud serverless workloads
- [Google Pub/Sub Transport](../transports/google-pubsub.md) - Configure the Google Pub/Sub transport for message publishing and consumption

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**Google Cloud Functions:** .NET 9 Runtime (2nd gen)
