---
title: Google Cloud Functions Deployment
description: Run Excalibur on Google Cloud Functions with HTTP and Pub/Sub triggers, Cloud Scheduler outbox runs, Firestore event sourcing and Secret Manager.
---
# Google Cloud Functions Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** Google Cloud Functions (Serverless)
**Last Updated:** 2026-01-01

---

The compiler-checked examples on this page use the optional dependency/compiler profile of the existing `samples/05-serverless/GoogleCloudFunctions/GoogleCloudFunctions.csproj` project, together with the normal Dispatch runtime references. They do not borrow its application types or demonstrate a deployed cloud host or successful NativeAOT publication. Use the Google Functions hosting/framework packages selected by that project; provider registrations additionally require their named Excalibur provider packages.

The order examples declare an application-owned `IOrderApplication`; register your real implementation in the host before startup. These snippets supply contracts, not a simulated business service.

## Before You Start

- **.NET 10.0**
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

## Invocation lifecycle and dependencies

`AddGoogleCloudFunctionsServerless()` registers the platform adapter, cold-start optimizer and validated hosting options; it does not compose messaging, persistence, health endpoints or trigger settlement. The `AddExcaliburGoogleCloudFunctionsServerless()` entry point forwards to those same services. Configure `AddDispatch(...)` and the domain/provider services your function uses explicitly. See the [registration boundary](https://github.com/TrigintaFaces/Excalibur/blob/main/src/Dispatch/Excalibur.Dispatch.Hosting.GoogleCloudFunctions/README.md).

| Concern | Application and host responsibility |
|---|---|
| Connection reuse | Keep the application container and thread-safe SDK clients for the host instance rather than rebuilding them per message. The selected transport owns its registered clients; do not dispose them after each invocation. Open and dispose database connections per operation so the driver's pool can reuse connections. Size each pool against maximum active instances and per-instance concurrency: scale-out creates more pools, not one shared application-wide limit. See [pool tuning](../operations/performance-tuning.md#connection-pool-tuning) and [Google connection-pooling guidance](https://cloud.google.com/sql/docs/sqlserver/manage-connections). |
| Health | Hosting registration does not register an endpoint or a dependency probe. With the corresponding transport registry configured, opt in to `AddHealthChecks().AddTransportHealthChecks()`; with a claim-check provider configured, `AddClaimCheckHealthCheck()` exercises a real store/retrieve/delete probe. Route protected health requests through the platform's HTTP surface or run explicit checks from monitoring. A platform process-health signal is not proof that the broker, database or claim-check storage is usable. |
| Invocation limits | Set timeout, memory and concurrency in the platform deployment. `ServerlessHostOptions.MemoryLimitMB` validates a supplied value; it does not allocate platform memory. `ExecutionTimeout` only caps the budget when invoking through `IServerlessHostProvider.ExecuteAsync`, which combines cancellation with the context's remaining time and cleanup reserve. Calling `IDispatcher` directly does not automatically use that wrapper. Propagate cancellation, await required work before returning, and make effects idempotent for trigger redelivery. |
| Freeze, resume and replacement | Treat in-memory state and timers as temporary. A host may stop scheduling your code or replace the instance; completing an invocation must not depend on a later in-process timer. Persist essential work before returning and drive durable outbox work from a platform trigger with background polling disabled. Reused clients may need their documented stale-connection recovery; initialization/warmup is not proof a connection is still live after an idle period. See [Google function lifecycle guidance](https://cloud.google.com/run/docs/tips/functions-best-practices). |
| Invocation scope and failures | Resolve scoped handler dependencies from the current invocation/message scope; do not cache them in a shared function object. Dispose application-owned scopes after awaited work, using async disposal when their services require it. The SDK trigger adapter owns acknowledgement, retries and batch responses. Check dispatch results before reporting success when using a result-returning path; a caught exception and a failed result are different observations. |

The repository's local adapter and sample tests demonstrate their selected DI, cancellation and handler paths; they do not simulate a cloud freeze, service settlement or scale-out. In the AWS SQS sample, partial failures are expressed with the SDK's `SQSBatchResponse`; Azure and Google trigger retry behavior comes from their host configuration. Application command/event types, handlers, business services and resource names below are illustrative application code, not extra services installed by the hosting registration.

## Quick Start

### HTTP-Triggered Function

```csharp
using Excalibur.Dispatch.Delivery;
// Function.cs
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Excalibur.Dispatch;
using System.Text.Json;

public class DispatchFunction : IHttpFunction
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<DispatchFunction> _logger;

    public DispatchFunction(IDispatcher dispatcher, ILogger<DispatchFunction> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        try
        {
            var command = await JsonSerializer.DeserializeAsync<CreateOrderCommand>(
                context.Request.Body)
                ?? throw new InvalidOperationException("An order command is required.");

            var result = await _dispatcher.DispatchAsync(command, context.RequestAborted);

            context.Response.StatusCode = result.Succeeded ? 200 : 500;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { succeeded = result.Succeeded });
        }
        catch (Exception ex)
        {
            // Log the detail; never return it. ex.Message can carry connection strings,
            // SQL or file paths, and this response reaches whoever called the function.
            _logger.LogError(ex, "Unhandled exception processing request");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
        }
    }
}

public sealed record CreateOrderCommand(Guid OrderId) : IDispatchAction;

// Application-owned contract: register your real implementation before starting the host.
public interface IOrderApplication
{
    Task CreateAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class CreateOrderCommandHandler(IOrderApplication orders) : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken) =>
        orders.CreateAsync(message.OrderId, cancellationToken);
}
```

### Startup.cs (Dependency Injection)

```csharp
// Startup.cs
using Google.Cloud.Functions.Hosting;
using Excalibur.Outbox.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

public class Startup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        // Add Dispatch
        services.AddDispatch(options =>
        {
            options.AddHandlersFromAssembly(typeof(CreateOrderCommandHandler).Assembly);
        });

        // Add handlers
        services.AddScoped<Excalibur.Dispatch.Delivery.IActionHandler<CreateOrderCommand>, CreateOrderCommandHandler>();

        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("SQL_CONNECTION_STRING is required.");

        // The outbox BUILDER is what registers IOutboxProcessor. AddSqlServerOutboxStore registers the
        // store only, so resolving the processor after it fails with "No service for type
        // IOutboxProcessor" -- the builder is not a stylistic preference here.
        //
        // The dead-letter queue is REQUIRED, not optional: the processor resolves IDeadLetterQueue and
        // throws when none is registered. To discard exhausted messages instead, register
        // NullDeadLetterQueue.Instance explicitly, so discard is a choice rather than an accident.
        services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
        {
            outbox.UseSqlServer(sql => sql.ConnectionString(connectionString));
            outbox.WithSqlServerDeadLetterQueue(dlq => dlq.ConnectionString = connectionString);
        }));
    }
}
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Google.Cloud.Functions.Hosting" Version="3.0.1" />
    <PackageReference Include="Google.Cloud.Functions.Framework" Version="3.0.1" />

    <PackageReference Include="Excalibur.Dispatch" Version="10.0.0" />
    <PackageReference Include="Excalibur.Outbox.SqlServer" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### Deploy

```bash
# Install Google Cloud SDK
# https://cloud.google.com/sdk/docs/install

# Deploy HTTP function
gcloud functions deploy dispatch-function \
  --runtime dotnet10 \
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

### Message size, and the claim check

Every broker here caps the size of a single message, and the cap has moved: Amazon raised the SQS maximum
payload from 256 KiB to 1 MiB in August 2025, and AWS's own quota pages have been reported lagging such
announcements. So **read the current service quota rather than any number written in a document**, this one
included — that is why no figure is quoted here.

What matters for composition is that the cap exists and that exceeding it fails the send, not the handler.
The framework ships the claim-check pattern for it: the payload goes to object storage and the message
carries a reference, so message size stops tracking payload size.

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddGcsClaimCheck(gcs => gcs
    .ProjectId("your-project")
    .BucketName("your-payload-bucket"));
```

Two consequences worth planning for rather than discovering: the handler's storage read is now on the
critical path of every oversized message, and the retention policy on that bucket becomes part of your
delivery guarantee — a payload collected by a lifecycle rule before its message is consumed is a message
that can no longer be processed. Set the bucket's retention longer than your queue's maximum retention plus
your dead-letter window.


### Pub/Sub Triggered Function

```csharp ignore
// PubSubFunction.cs
using Google.Cloud.Functions.Framework;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using CloudNative.CloudEvents;
using Excalibur.Dispatch;

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
            var result = await _dispatcher.DispatchAsync(integrationEvent, cancellationToken);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Integration event dispatch failed.");
            }

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
  --runtime dotnet10 \
  --trigger-topic integration-events \
  --entry-point IntegrationEventProcessor \
  --region us-central1
```

---

## Cloud Scheduler for Periodic Processing

### Scheduled Outbox Processor

```csharp ignore
// OutboxProcessorFunction.cs
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using Excalibur.Dispatch;

public class OutboxProcessorFunction : IHttpFunction
{
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly ILogger<OutboxProcessorFunction> _logger;

    public OutboxProcessorFunction(
        IOutboxProcessor outboxProcessor,
        ILogger<OutboxProcessorFunction> logger)
    {
        _outboxProcessor = outboxProcessor;
        _outboxProcessor.Init($"serverless-outbox-{Guid.NewGuid():N}");
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        _logger.LogInformation("Outbox processor triggered at: {Now}", DateTime.UtcNow);

        try
        {
            var processedCount = await _outboxProcessor.DispatchPendingMessagesAsync(
                context.RequestAborted);

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
  --runtime dotnet10 \
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

Install `Excalibur.EventSourcing.Firestore` and select the existing provider using the host's Application Default Credentials:

```csharp
using Excalibur.EventSourcing.Firestore;
using Microsoft.Extensions.DependencyInjection;

public static class FirestoreRegistration
{
    public static void Configure(IServiceCollection services, string projectId)
    {
        services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
            es.UseFirestore(firestore => firestore
                .ProjectId(projectId)
                .CollectionName("events"))));
    }
}
```

Follow [Firestore provider setup](../event-sourcing/providers.md#google-firestore) for database/collection configuration, key provisioning and repository registration. Use [projection composition](../event-sourcing/projections.md) for the framework event-store path. A Firestore document trigger is separate application/platform code; do not assume a hand-written `eventType`/`eventData` document reader matches the provider's stored envelope or implements the current `IEventStore` contract.

---

## Cloud SQL Integration

### SQL Server connection configuration

`AddSqlServerOutboxStore` selects SQL Server. Configure a SQL Server TCP connection string for the **same intended database** using Google's [SQL Server connection procedure](https://cloud.google.com/sql/docs/sqlserver/connect-instance-cloud-run) or a supported [Cloud SQL Auth Proxy TCP connection](https://cloud.google.com/sql/docs/sqlserver/connect-auth-proxy). Configure the required network access, credentials and TLS for that route. Do not pass a MySQL/PostgreSQL `/cloudsql/...` Unix-socket path to the SQL Server provider, or change database providers just to reuse such a path.

```csharp
using Excalibur.Outbox.SqlServer;
using Google.Cloud.Functions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

public sealed class EnvironmentOutboxStartup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("SQL_CONNECTION_STRING is required.");
        services.AddSqlServerOutboxStore(options => options.ConnectionString = connectionString);
    }
}
```

This call registers the store only. Retain the earlier `AddExcalibur(...AddOutbox(...))` processor/dead-letter composition when running the scheduled processor; use one consistent connection configuration for those registrations. Store secrets in Secret Manager or deployment secret configuration rather than putting credentials into deployment commands. Follow the connection procedure for the selected Functions generation/network profile; setting `INSTANCE_CONNECTION_NAME` alone is not a SQL Server connection.

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

Configure tracing through the [Google .NET diagnostics documentation](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Diagnostics.AspNetCore3/latest) for the SDK and hosting profile you deploy, including project identity, credentials, sampling and exporter flushing. `ServerlessTelemetryOptions.EnableDistributedTracing` and `EnableMetrics` describe platform-provisioned telemetry; `AddGoogleCloudFunctionsServerless()` does not register an in-process trace exporter for these flags. Do not rely on a background exporter flush after the function has returned.

---

## Performance Optimization

### Minimum Instances (Reduce Cold Starts)

```bash
# Set minimum instances
gcloud functions deploy your-function \
  --runtime dotnet10 \
  --trigger-http \
  --entry-point DispatchFunction \
  --region us-central1 \
  --min-instances 1 \
  --max-instances 10
```

**Earlier unverified illustration (not measured framework cloud results):** the previous estimate of ~$0.0000025 per GB-second and “near-zero cold starts for first instance” is historical, not current pricing or a latency guarantee. Minimum instances can reduce cold starts but do not guarantee a warm instance for every request. Check [current pricing](https://cloud.google.com/functions/pricing) for the generation and region you deploy, and measure startup latency for the actual workload.

#### Run the framework's cold-start optimizer

Everything above is platform configuration. The framework also ships a cold-start optimizer for Google Cloud Functions,
and **nothing invokes it for you** — registering it seats the service, it does not run the warmup. Left
uncalled, dependency injection, JIT and SDK initialization are all paid on the first invocation of each new
instance.

```csharp
using Excalibur.Dispatch.Hosting.Serverless;
using Microsoft.Extensions.DependencyInjection;

services.AddGoogleCloudFunctionsServerless();
```

Then drive it once from your function's construction or initialization path, not per invocation:

```csharp
var optimizer = provider.GetRequiredService<IColdStartOptimizer>();
await optimizer.OptimizeAsync();
```

`OptimizeAsync` is a no-op off-platform: the optimizer reports itself disabled unless both `FUNCTION_NAME` and `FUNCTION_TARGET` are set, which the Functions runtime sets for you, so the same
code is safe to run locally and in tests without a guard of your own.

Unlike AWS Lambda there is no snapshot-and-restore mechanism here to attach a warmup to, so this runs
during instance initialization rather than at image-capture time. That makes it worth less than SnapStart
and more than nothing: the work moves off the first request and onto instance startup, which is where
Google Cloud Functions's own prewarming and minimum-instance settings above are already paying.


### Memory and CPU Allocation

```bash
# Increase memory (also increases CPU)
gcloud functions deploy your-function \
  --runtime dotnet10 \
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
  - name: 'mcr.microsoft.com/dotnet/sdk:10.0'
    entrypoint: 'dotnet'
    args: ['restore']

  # Build
  - name: 'mcr.microsoft.com/dotnet/sdk:10.0'
    entrypoint: 'dotnet'
    args: ['build', '-c', 'Release']

  # Deploy function
  - name: 'gcr.io/google.com/cloudsdktool/cloud-sdk'
    entrypoint: 'gcloud'
    args:
      - 'functions'
      - 'deploy'
      - 'dispatch-function'
      - '--runtime=dotnet10'
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
          dotnet-version: '10.0.x'

      - name: Authenticate to Google Cloud
        uses: google-github-actions/auth@v2
        with:
          credentials_json: ${{ secrets.GCP_SA_KEY }}

      - name: Setup Cloud SDK
        uses: google-github-actions/setup-gcloud@v2

      - name: Deploy function
        run: |
          gcloud functions deploy dispatch-function \
            --runtime dotnet10 \
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
    --runtime dotnet10 \
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

Select a timeout within the [limits for your function generation and trigger](https://cloud.google.com/functions/quotas#time_limits); HTTP and event-driven functions do not share one universal maximum.

```bash
# Example timeout; check the limit for the deployed generation and trigger type
gcloud functions deploy dispatch-function \
  --timeout 540s
```

---

## Best Practices

### 1. Use Dependency Injection

```csharp
using Google.Cloud.Functions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

// GOOD: DI via Startup
public class MinimalDispatchStartup : FunctionsStartup
{
    public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
    {
        services.AddDispatch();
    }
}

// Resolve IDispatcher from DI; do not manually construct its dependency graph.
```

### 2. Handle Retries Gracefully

`IsAlreadyProcessed`, `ProcessMessage` and `MarkAsProcessed` below are application-provided methods. Persist the marker with the business effect under the required concurrency control; a separate check and later mark alone is not an atomic deduplication guarantee.

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
**Google Cloud Functions:** .NET 10 Runtime (2nd gen)
