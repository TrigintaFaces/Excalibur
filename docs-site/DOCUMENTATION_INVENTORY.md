# Documentation Inventory Report

> **Generated:** December 9, 2025
> **Purpose:** Complete API surface analysis for professional documentation
> **Methodology:** File-by-file code exploration of all public types

---

## Executive Summary

This inventory catalogs the **complete public API surface** of both Dispatch and Excalibur frameworks based on actual code analysis. Use this as the authoritative reference when writing documentation.

### Framework Scope

| Framework | Focus | Package Count |
|-----------|-------|---------------|
| **Dispatch** | Message routing, pipelines, transports | **40 packages** |
| **Excalibur** | Domain modeling, persistence, event sourcing | 37+ packages |

---

## Part 1: Excalibur framework

### 1.1 Core Packages

#### Excalibur.Dispatch.Abstractions

The foundation of the messaging system. Every consumer needs these interfaces.

**Message Types:**

| Interface | Purpose | Key Properties |
|-----------|---------|----------------|
| `IDispatchAction` | Action without return value | Marker interface |
| `IDispatchAction<TResult>` | Action with typed return | Generic marker |
| `IDispatchEvent` | Domain event contract | Inherits `IDomainEvent` |
| `IDispatchDocument` | Document message | Data-focused messages |
| `IDomainEvent` | Base event interface | `EventId`, `AggregateId`, `Version`, `OccurredAt`, `EventType`, `Metadata` |
| `IIntegrationEvent` | Cross-boundary events | For external system communication |

**MessageKinds Enum (Flags):**
```csharp
[Flags]
public enum MessageKinds
{
    None = 0,
    Action = 1,      // Commands/queries
    Event = 2,       // Domain events
    Document = 4     // Data documents
}
```
> **Note:** There is NO `MessageKinds.Command`. Use `MessageKinds.Action`.

**Handler Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IActionHandler<TAction>` | Handles actions without return |
| `IActionHandler<TAction, TResult>` | Handles actions with return |
| `IEventHandler<TEvent>` | Handles events (multiple handlers per event) |

**Result Types:**

| Type | Purpose |
|------|---------|
| `IMessageResult` | Base result contract |
| `IMessageResult<TValue>` | Result with typed value |
| `MessageResult` | Static factory for creating results |
| `BasicMessageResult` | Default implementation |

**Creating Results:**
```csharp
// Success
return MessageResult.Success();
return MessageResult.Success(value);

// Failure
return MessageResult.Failed("Error message");
return MessageResult.Failed(new ValidationError("Field", "Message"));

// Check result
if (result.IsSuccess) { }
if (result.IsFailed) { }
var error = result.ErrorMessage;
var value = result.ReturnValue;
```

**Context Types:**

| Type | Purpose |
|------|---------|
| `IMessageContext` | Per-message context container |
| `CorrelationId` | Tracks request chain |
| `CausationId` | Links cause to effect |
| `TenantId` | Multi-tenancy support |

**Serialization:**

| Interface | Purpose |
|-----------|---------|
| `IEventSerializer` | Event serialization contract |
| `IMessageSerializer` | Message serialization contract |

---

#### Dispatch (Core)

The dispatcher implementation and pipeline infrastructure.

**Core Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IDispatcher` | Main entry point for dispatching |
| `IDispatcherBuilder` | Fluent builder for configuration |

**Pipeline Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IDispatchMiddleware` | Middleware component contract |
| `IDispatchMiddleware<TMessage>` | Typed middleware |
| `IPipelineProfile` | Pipeline configuration profile |
| `IPipelineBuilder` | Fluent pipeline configuration |

**IPipelineProfile Properties:**
```csharp
public interface IPipelineProfile
{
    string Name { get; }
    MessageKinds AppliesTo { get; }           // Filter by message type
    int Order { get; }                         // Execution priority
    bool IsEnabled { get; }
    IReadOnlyList<Type> Middleware { get; }   // Middleware types in order
    Func<IMessageContext, bool>? Predicate { get; }  // Conditional execution
}
```

**Service Registration:**
```csharp
// Namespace: Microsoft.Extensions.DependencyInjection

// Recommended: Register with the builder
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Or multiple assemblies
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(OrderHandler).Assembly);
    dispatch.AddHandlersFromAssembly(typeof(PaymentHandler).Assembly);
});

// With full configuration
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseMiddleware<LoggingMiddleware>();
    dispatch.UseMiddleware<ValidationMiddleware>();
});
```

---

### 1.2 Transport Packages

All transports follow a consistent pattern with provider-specific options.

#### Excalibur.Dispatch.Transport.Abstractions

**Core Types:**

| Type | Purpose |
|------|---------|
| `CloudMessagingOptions` | Global transport configuration |
| `ICloudProvider` | Provider abstraction |
| `IMessageBus` | Message bus interface |
| `ITransportSender` | Minimal send interface (3 methods) |
| `ITransportReceiver` | Minimal receive interface (3 methods) |
| `TransportMessage` | Slim message type (9 properties) |
| `ReceivedMessage` | Received message envelope (14 properties) |
| `IDeadLetterQueueManager` | DLQ handling |

---

#### Excalibur.Dispatch.Transport.Kafka

**Registration:**
```csharp
services.AddKafkaTransport(options => {
    options.BootstrapServers = "localhost:9092";
    options.ConsumerGroupId = "my-group";
    options.EnableCloudEvents = true;
});
```

**KafkaMessageBusOptions:**

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `BootstrapServers` | string? | - | Kafka servers |
| `ProducerClientId` | string? | "dispatch-producer" | Producer identity |
| `ConsumerGroupId` | string? | "dispatch-consumer" | Consumer group |
| `EnableCloudEvents` | bool | true | CloudEvents format |
| `CompressionType` | KafkaCompressionType | None | Message compression |
| `AckLevel` | KafkaAckLevel | All | Acknowledgment mode |
| `PartitioningStrategy` | KafkaPartitioningStrategy | RoundRobin | Partition selection |

**Detailed KafkaOptions (Protocol-level):**

| Property | Default | Purpose |
|----------|---------|---------|
| `Topic` | - | Topic name (required) |
| `MaxBatchSize` | 100 | Batch processing size |
| `EnableAutoCommit` | false | Manual commit preferred |
| `AutoOffsetReset` | "latest" | Start position for new consumers |
| `SessionTimeoutMs` | 30000 | Consumer session timeout |
| `MaxPollIntervalMs` | 300000 | Max poll interval |

---

#### Excalibur.Dispatch.Transport.RabbitMQ

**Registration:**
```csharp
services.AddRabbitMqMessageBus(options => {
    options.ConnectionString = "amqp://guest:guest@localhost:5672";
    options.ExchangeName = "dispatch.events";
    options.ExchangeType = RabbitMqExchangeType.Topic;
});
```

**RabbitMqMessageBusOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `ConnectionString` | - | AMQP connection URI |
| `ExchangeName` | "dispatch.events" | Exchange name |
| `ExchangeType` | Topic | Exchange type (Direct, Topic, Fanout, Headers) |
| `Persistence` | Persistent | Message durability |
| `RoutingStrategy` | EventType | How messages are routed |
| `EnableCloudEvents` | true | CloudEvents format |
| `AutoDelete` | false | Auto-delete exchange |

**Dead Letter Exchange:**
```csharp
options.EnableDeadLetterExchange = true;
options.DeadLetterExchange = "dispatch.dlx";
options.DeadLetterRoutingKey = "failed";
options.RequeueOnReject = false;
```

---

#### Excalibur.Dispatch.Transport.AzureServiceBus

**Three Distinct Services:**

1. **Service Bus** (Queues/Topics)
2. **Event Hubs** (Streaming)
3. **Storage Queues** (Simple queuing)

**Registration:**
```csharp
// Service Bus
services.AddAzureServiceBus(
    configureProvider => { /* AzureProviderOptions */ },
    configureServiceBus => { /* AzureServiceBusOptions */ }
);

// Event Hubs
services.AddAzureEventHub(
    configureProvider => { /* AzureProviderOptions */ },
    configureEventHub => { /* AzureEventHubOptions */ }
);

// Storage Queues
services.AddAzureStorageQueue(
    configureProvider => { /* AzureProviderOptions */ },
    configureStorageQueue => { /* AzureStorageQueueOptions */ }
);

// All three
services.AddAzureCloudProviders(configureProvider);
```

**AzureProviderOptions (Shared):**

| Property | Default | Purpose |
|----------|---------|---------|
| `UseManagedIdentity` | false | Use Azure AD identity |
| `FullyQualifiedNamespace` | - | For managed identity auth |
| `EnableSessions` | false | Session-based processing |
| `PrefetchCount` | 10 | Prefetch count |
| `MaxMessageSizeBytes` | 256KB | Max message size |

**AzureServiceBusOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `Namespace` | - | Service Bus namespace (required) |
| `QueueName` | - | Queue name (required) |
| `MaxConcurrentCalls` | 10 | Parallel processing |
| `CloudEventsMode` | Structured | CloudEvents format |
| `DeadLetterOnRejection` | false | DLQ on failure |

---

#### Excalibur.Dispatch.Transport.AwsSqs

**Three Distinct Services:**

1. **SQS** (Queues)
2. **SNS** (Pub/Sub)
3. **EventBridge** (Event routing)

**Registration:**
```csharp
services.AddAwsSqs(options => {
    options.Region = "us-east-1";
    options.QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/...");
});

services.AddAwsSns(options => {
    options.TopicArn = "arn:aws:sns:us-east-1:...";
});

services.AddAwsEventBridge(options => {
    options.EventBusName = "default";
    options.Source = "Excalibur.Dispatch.Transport";
});
```

**AwsSqsOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `QueueUrl` | - | SQS queue URL |
| `MaxNumberOfMessages` | 10 | Messages per poll |
| `WaitTimeSeconds` | 20 | Long polling duration |
| `VisibilityTimeout` | 30 | Processing timeout |
| `UseFifoQueue` | false | FIFO queue support |
| `ContentBasedDeduplication` | false | Deduplication mode |
| `KmsMasterKeyId` | - | Encryption key |

---

#### Excalibur.Dispatch.Transport.GooglePubSub

**Registration:**
```csharp
services.AddGooglePubSub(options => {
    options.ProjectId = "my-project";
    options.TopicId = "my-topic";
    options.SubscriptionId = "my-subscription";
});

// With flow control
services.AddGooglePubSubWithFlowControl(
    configurePubSub => { /* GooglePubSubOptions */ },
    configureFlowControl => { /* FlowControlSettings */ }
);

// With telemetry
services.AddGooglePubSubTelemetry(options => {
    options.EnableOpenTelemetry = true;
    options.ExportToCloudMonitoring = false;
});
```

**GooglePubSubOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `ProjectId` | - | GCP project (required) |
| `TopicId` | - | Topic ID (required) |
| `SubscriptionId` | - | Subscription ID (required) |
| `MaxPullMessages` | 100 | Messages per pull |
| `AckDeadlineSeconds` | 60 | Ack deadline |
| `EnableAutoAckExtension` | true | Auto-extend deadline |
| `EnableExactlyOnceDelivery` | false | Exactly-once semantics |
| `EnableMessageOrdering` | false | Ordered delivery |
| `UseEmulator` | false | Local emulator mode |
| `EmulatorHost` | "localhost:8085" | Emulator address |

---

### 1.3 Validation Package

#### Excalibur.Dispatch.Validation.FluentValidation

**Registration:**
```csharp
// Standard integration
services.AddDispatch(dispatch => {
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithFluentValidation();
});

// AOT-compatible
services.AddDispatch(dispatch => {
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithAotFluentValidation();
});
```

**Key Classes:**

| Class | Purpose |
|-------|---------|
| `FluentValidatorResolver` | Resolves validators from DI |
| `AotFluentValidatorResolver` | AOT-compatible resolver |

**Usage Pattern:**
```csharp
// 1. Define validator
public class CreateOrderValidator : AbstractValidator<CreateOrderAction>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
    }
}

// 2. Register validators
services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// 3. Enable in Dispatch
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.WithFluentValidation();
});
```

---

### 1.4 Compliance Packages

#### Excalibur.Dispatch.Compliance (Core)

**Encryption Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IEncryptionProvider` | Field-level encryption |
| `IKeyManagementProvider` | Key operations |
| `IFipsDetector` | FIPS 140-2 detection |
| `IKeyRotationScheduler` | Automated rotation |

**Registration:**
```csharp
// Basic encryption with in-memory keys (dev only)
services.AddComplianceEncryption();

// With key rotation
services.AddComplianceEncryptionWithRotation();

// With custom key provider
services.AddComplianceEncryption<AzureKeyVaultProvider>();

// Multi-region disaster recovery
services.AddMultiRegionKeyManagement<AzureKeyVaultProvider, AwsKmsProvider>();

// Key rotation background service
services.AddKeyRotation();
```

**EncryptionOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `Purpose` | - | Encryption purpose identifier |
| `RequireFips` | false | Enforce FIPS compliance |
| `TenantId` | - | Multi-tenant key isolation |

---

#### Excalibur.Dispatch.Compliance.Azure

**Registration:**
```csharp
services.AddAzureKeyVaultKeyManagement(options => {
    options.VaultUri = new Uri("https://myvault.vault.azure.net/");
    options.RequirePremiumTier = true;  // For FIPS compliance
    options.KeyNamePrefix = "dispatch-";
    options.MetadataCacheDuration = TimeSpan.FromMinutes(5);
});
```

---

#### Excalibur.Dispatch.Compliance.Aws

**Registration:**
```csharp
services.AddAwsKmsKeyManagement(options => {
    options.Region = "us-east-1";
    options.UseFipsEndpoint = true;
    options.EnableAutoRotation = true;
});

// LocalStack for testing
services.AddAwsKmsKeyManagementLocalStack("http://localhost:4566");

// Multi-region
services.AddAwsKmsKeyManagementMultiRegion("us-east-1", ["eu-west-1", "ap-southeast-1"]);
```

---

#### Excalibur.Dispatch.Compliance.Vault

**Registration:**
```csharp
services.AddVaultKeyManagement(options => {
    options.VaultUri = new Uri("https://vault.example.com");
    options.AuthMethod = VaultAuthMethod.AppRole;
    options.AppRoleId = "role-id";
    options.AppRoleSecretId = "secret-id";
    options.TransitMountPath = "transit";
});
```

---

### 1.5 Security Package

#### Excalibur.Dispatch.Security

**Comprehensive security features:**

| Feature | Classes |
|---------|---------|
| JWT Authentication | `JwtAuthenticationMiddleware`, `JwtAuthenticationOptions` |
| Input Validation | `SqlInjectionValidator`, `XssValidator`, `PathTraversalValidator`, `CommandInjectionValidator` |
| Message Encryption | `DataProtectionMessageEncryptionService`, `MessageEncryptionMiddleware` |
| Message Signing | `HmacMessageSigningService`, `MessageSigningMiddleware` |
| Rate Limiting | `RateLimitingMiddleware`, `RateLimitingOptions` |
| Credential Management | `ICredentialStore`, `ISecureCredentialProvider` |

**Registration:**
```csharp
services.AddDispatchSecurity(config);
services.AddSecureCredentialManagement(config);
services.AddInputValidation(config);
services.AddSecurityAuditing(config);

// In pipeline
builder.UseSecurityMiddleware();
```

---

### 1.6 Audit Logging Packages

#### Excalibur.Dispatch.AuditLogging

**Registration:**
```csharp
// Default in-memory
services.AddAuditLogging();

// Custom store
services.AddAuditLogging<SqlServerAuditStore>();

// With RBAC
services.AddRbacAuditStore();
```

#### Excalibur.Dispatch.AuditLogging.SqlServer

```csharp
services.AddSqlServerAuditStore(options => {
    options.ConnectionString = connectionString;
    options.SchemaName = "audit";
    options.TableName = "AuditLogs";
    options.RetentionPeriod = TimeSpan.FromDays(90);
    options.EnableHashChain = true;  // Tamper detection
});
```

#### Cloud Exporters

```csharp
// Splunk
services.AddSplunkAuditExporter(options => {
    options.HecUrl = new Uri("https://splunk.example.com:8088");
    options.HecToken = "token";
});

// Microsoft Sentinel
services.AddSentinelAuditExporter(options => {
    options.WorkspaceId = "workspace-id";
    options.SharedKey = "shared-key";
});

// Datadog
services.AddDatadogAuditExporter(options => {
    options.ApiKey = "api-key";
    options.Site = "datadoghq.com";
});
```

---

### 1.7 Hosting Packages

#### Excalibur.Dispatch.Hosting.AspNetCore

**Minimal API Integration:**
```csharp
// Configure Dispatch on WebApplicationBuilder
builder.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Map endpoints
app.MapPost("/orders", async (CreateOrderRequest request, IDispatcher dispatcher) =>
{
    var action = new CreateOrderAction(request.CustomerId, request.Items);
    var result = await dispatcher.DispatchAsync(action);
    return result.ToHttpResult();
});

// Or use typed endpoint builders
app.DispatchPostAction<CreateOrderRequest, CreateOrderAction>("/orders");
app.DispatchGetAction<GetOrderRequest, GetOrderAction, Order>("/orders/{id}");
```

**MVC Integration:**
```csharp
public class OrderController : ControllerBase
{
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var result = await this.DispatchMessageAsync(new CreateOrderAction(...));
        return result.ToHttpActionResult();
    }
}
```

**Context Extraction:**
```csharp
// Automatic from HTTP context
var correlationId = httpContext.CorrelationId();  // Header or generated
var tenantId = httpContext.TenantId();            // Header, route, query, claims, or subdomain
```

---

#### Excalibur.Dispatch.Hosting.Serverless.Abstractions

**Unified Serverless Interface:**

| Interface | Purpose |
|-----------|---------|
| `IServerlessHostProvider` | Platform abstraction |
| `IServerlessContext` | Execution context |
| `IColdStartOptimizer` | Warmup optimization |

**ServerlessHostOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `EnableColdStartOptimization` | true | Warmup services |
| `EnableDistributedTracing` | true | Trace propagation |
| `EnableMetrics` | true | Metrics collection |
| `ExecutionTimeout` | - | Override platform timeout |

---

#### Excalibur.Dispatch.Hosting.AwsLambda

```csharp
services.AddAwsLambdaServerless(options => {
    options.AwsLambda.EnableProvisionedConcurrency = true;
    options.AwsLambda.Runtime = "dotnet8";
});
```

---

#### Excalibur.Dispatch.Hosting.AzureFunctions

```csharp
services.AddAzureFunctionsServerless(options => {
    options.AzureFunctions.HostingPlan = "Consumption";
    options.AzureFunctions.EnableDurableFunctions = true;
});
```

---

#### Excalibur.Dispatch.Hosting.GoogleCloudFunctions

```csharp
services.AddGoogleCloudFunctionsServerless(options => {
    options.GoogleCloudFunctions.Runtime = "dotnet6";
    options.GoogleCloudFunctions.MinInstances = 1;
});
```

---

### 1.8 Observability Package

#### Excalibur.Dispatch.Observability

**Activity Source:** `"Dispatch"`

**Operations:**
- `message.process` - Message processing span
- `message.publish` - Publishing span
- `message.handle` - Handler execution span
- `middleware.*` - Middleware spans

**Meters:**
- `Excalibur.Dispatch` - Core metrics
  - `messages_processed` (counter)
  - `messages_published` (counter)
  - `messages_failed` (counter)
  - `messages_duration` (histogram)
  - `sessions_active` (gauge)

**Registration:**
```csharp
services.AddDispatchObservability(options => {
    options.Enabled = true;
    options.ServiceName = "my-service";
    options.OtlpEndpoint = "http://collector:4317";
    options.ExportToPrometheus = true;
    options.PrometheusScrapePath = "/metrics";
});
```

**ContextObservabilityOptions:**

| Property | Default | Purpose |
|----------|---------|---------|
| `ValidateContextIntegrity` | false | Validate context state |
| `MaxContextSizeBytes` | 102400 | Size limit (100KB) |
| `MaxSnapshotsPerLineage` | 100 | History limit |
| `SnapshotRetentionPeriod` | 1 hour | Retention time |

---

### 1.9 Resilience Package

#### Excalibur.Dispatch.Resilience.Polly

**Core Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IBulkheadPolicy` | Resource isolation |
| `IGracefulDegradationService` | Service degradation |
| `ITimeoutManager` | Centralized timeouts |
| `IDistributedCircuitBreaker` | Distributed state |

**Registration:**
```csharp
services.AddPollyResilience(configuration);

// Named policies
services.AddPollyRetryPolicy("database", options => {
    options.MaxRetries = 3;
    options.BaseDelay = TimeSpan.FromSeconds(1);
    options.BackoffStrategy = BackoffStrategy.Exponential;
    options.UseJitter = true;
});

services.AddPollyCircuitBreaker("external-api", options => {
    options.FailureRatio = 0.5;
    options.MinimumThroughput = 10;
    options.SamplingDuration = TimeSpan.FromSeconds(30);
    options.BreakDuration = TimeSpan.FromSeconds(30);
});

services.AddBulkhead("heavy-operation", options => {
    options.MaxConcurrency = 10;
    options.MaxQueueLength = 50;
});
```

**Graceful Degradation Levels:**
```csharp
public enum DegradationLevel
{
    None,      // Full functionality
    Minor,     // Non-critical features disabled
    Moderate,  // Optional features disabled
    Major,     // Core features only
    Severe,    // Minimal functionality
    Emergency  // Read-only / maintenance mode
}
```

---

### 1.10 Caching Package

#### Excalibur.Dispatch.Caching

**Cache Modes:**

| Mode | Backend | Use Case |
|------|---------|----------|
| `Memory` | IMemoryCache | Single server |
| `Distributed` | IDistributedCache | Multi-server |
| `Hybrid` | HybridCache | Fast local + shared fallback |

**Registration:**
```csharp
services.AddDispatchCaching(options => {
    options.Enabled = true;
    options.CacheMode = CacheMode.Hybrid;
    options.DefaultExpiration = TimeSpan.FromMinutes(10);
    options.UseSlidingExpiration = true;
});

// Redis backend
services.AddDispatchRedisCaching(
    configureRedis => { /* RedisCacheOptions */ },
    configureCaching => { /* CacheOptions */ }
);
```

**Attribute-Based Caching:**
```csharp
[CacheResult(ExpirationSeconds = 300, Tags = ["orders", "customer"])]
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;

[InvalidateCache(Tags = ["orders"])]
public record CreateOrderAction(...) : IDispatchAction;
```

---

## Part 2: Excalibur Framework

### 2.1 Domain Package

#### Excalibur.Domain

**Base Classes:**

| Class | Purpose |
|-------|---------|
| `AggregateRoot<TId>` | Event-sourced aggregate root |
| `Entity<TId>` | Entity base class |
| `ValueObject` | Value object base |

**AggregateRoot<TId> Pattern:**
```csharp
public class Order : AggregateRoot<OrderId>
{
    // Private constructor for rehydration
    private Order() { }

    // Factory method
    public static Order Create(string customerId)
    {
        var order = new Order();
        order.RaiseEvent(new OrderCreated(new OrderId(Guid.NewGuid()), customerId));
        return order;
    }

    // Business methods raise events
    public void AddItem(string sku, int quantity)
    {
        // Business rules first
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify submitted order");

        RaiseEvent(new OrderItemAdded(Id, sku, quantity));
    }

    // Pattern matching for event application (NO reflection)
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreated e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                Status = OrderStatus.Draft;
                break;

            case OrderItemAdded e:
                _items.Add(new OrderItem(e.Sku, e.Quantity));
                break;
        }
    }
}
```

**Key Properties from AggregateRoot:**

| Property | Type | Purpose |
|----------|------|---------|
| `Id` | TId | Aggregate identifier |
| `Version` | int | Current version for concurrency |
| `UncommittedEvents` | IReadOnlyList<IDomainEvent> | Events not yet persisted |

---

### 2.2 Data Access Package

#### Excalibur.Data.Abstractions

> **Important:** NO Entity Framework Core. Uses Dapper exclusively.

**Core Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IDb` | Database connection factory |
| `IDataRequest` | Query execution contract |
| `IUnitOfWork` | Transaction management |

**Exceptions:**

| Exception | Purpose |
|-----------|---------|
| `ResourceException` | Base data exception |
| `ConcurrencyException` | Optimistic concurrency violation |
| `NotFoundException` | Resource not found |

**IDb Pattern:**
```csharp
public interface IDb
{
    IDbConnection CreateConnection();
    Task<T> ExecuteAsync<T>(IDataRequest<T> request, CancellationToken ct);
}
```

---

### 2.3 Event Sourcing Packages

#### Excalibur.EventSourcing

**Core Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IEventStore` | Event persistence |
| `IEventSourcedRepository<TAggregate, TId>` | Aggregate repository |
| `ISnapshotStore` | Snapshot persistence |
| `IEventUpcaster` | Schema evolution |

**IEventStore Methods:**
```csharp
public interface IEventStore
{
    Task<IReadOnlyList<PersistedEvent>> LoadAsync(
        string streamId,
        int fromVersion,
        CancellationToken ct);

    Task AppendAsync(
        string streamId,
        IReadOnlyList<IDomainEvent> events,
        int expectedVersion,
        CancellationToken ct);

    Task<IReadOnlyList<PersistedEvent>> GetUndispatchedEventsAsync(
        int batchSize,
        CancellationToken ct);

    Task MarkAsDispatchedAsync(
        IReadOnlyList<Guid> eventIds,
        CancellationToken ct);
}
```

**Repository Pattern:**
```csharp
// Load aggregate
var order = await _repository.GetAsync(orderId, ct);

// Make changes
order.AddItem("SKU-001", 2);

// Save (persists events, handles concurrency)
await _repository.SaveAsync(order, ct);
```

---

#### Provider Packages

All providers implement the same interfaces with provider-specific optimizations.

**Excalibur.EventSourcing.SqlServer:**
```csharp
services.AddSqlServerEventSourcing(options => {
    options.ConnectionString = connectionString;
    options.Schema = "es";
    options.EventsTableName = "Events";
    options.SnapshotsTableName = "Snapshots";
});
```

**Excalibur.EventSourcing.Postgres:**
```csharp
services.AddPostgresEventStore(options => {
    options.ConnectionString = connectionString;
    options.Schema = "event_store";
});
```

**Excalibur.EventSourcing.MongoDB:**
```csharp
services.AddMongoDbEventStore(options => {
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "event_store";
});
```

**Excalibur.EventSourcing.CosmosDb:**
```csharp
services.AddCosmosDbEventStore(options => {
    options.EndpointUri = new Uri("https://account.documents.azure.com");
    options.DatabaseName = "event_store";
    options.ContainerName = "events";
});
```

---

### 2.4 Saga Package

#### Excalibur.Saga

**Two Saga Patterns:**

| Pattern | Interface | Use Case |
|---------|-----------|----------|
| **Orchestration** | `ISaga<TSagaData>` | Process Manager - central coordinator |
| **Choreography** | `ISaga` | Event-driven - decentralized |

**Orchestration (Process Manager):**
```csharp
public class OrderSagaState : SagaState
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Step { get; set; } = string.Empty;
}

public class OrderSaga : Saga<OrderSagaState>
{
    public OrderSaga(OrderSagaState state, IDispatcher dispatcher, ILogger<OrderSaga> logger)
        : base(state, dispatcher, logger) { }

    public override bool HandlesEvent(object eventMessage)
        => eventMessage is OrderCreated or InventoryReserved;

    public override async Task HandleAsync(object eventMessage, CancellationToken ct)
    {
        switch (eventMessage)
        {
            case OrderCreated created:
                State.OrderId = created.OrderId;
                State.Step = "Created";
                // Coordinate next step using the Dispatcher
                await Dispatcher.DispatchAsync(new ReserveInventoryCommand(State.OrderId), ct);
                break;

            case InventoryReserved:
                State.Step = "InventoryReserved";
                await Dispatcher.DispatchAsync(new ProcessPaymentCommand(State.OrderId, State.Amount), ct);
                break;
        }
    }
}
```

**Choreography (Event-Driven):**
```csharp
public class InventorySaga : ISaga
{
    private readonly IInventoryService _inventory;
    private readonly IDispatcher _dispatcher;

    public Guid Id { get; } = Guid.NewGuid();
    public bool IsCompleted { get; private set; }

    public bool HandlesEvent(object eventMessage) => eventMessage is OrderCreated;

    public async Task HandleAsync(object eventMessage, CancellationToken ct)
    {
        if (eventMessage is OrderCreated created)
        {
            // React to event, dispatch new event
            await _inventory.ReserveAsync(created.Items, ct);
            await _dispatcher.DispatchAsync(new InventoryReserved(created.OrderId), ct);
            IsCompleted = true;
        }
    }
}
```

**Key Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `ISagaCoordinator` | Saga lifecycle management |
| `ISagaStore` | Saga state persistence |
| `ISagaContext` | Execution context |

> **Note:** There is NO `ISagaFinder`. Use `ISagaCoordinator` + `ISagaStore`.

---

### 2.5 Outbox Package

#### Excalibur.Outbox

**Purpose:** Reliable event publishing with transactional guarantees.

**Core Interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IOutboxStore` | Outbox message persistence |
| `IOutboxProcessor` | Message processing |
| `IOutboxPublisher` | Transport integration |

**Pattern:**
```csharp
// In transaction with aggregate save
await _eventStore.AppendAsync(streamId, events, version, ct);
await _outboxStore.AddAsync(events, ct);
await _transaction.CommitAsync(ct);

// Background processor publishes
await _outboxProcessor.DispatchPendingMessagesAsync(ct);
```

---

### 2.6 Leader Election Packages

#### Excalibur.LeaderElection

**Purpose:** Distributed coordination for single-active scenarios.

**Core Interface:**
```csharp
public interface ILeaderElection
{
    Task<bool> TryAcquireLeadershipAsync(string resourceId, CancellationToken ct);
    Task ReleaseLeadershipAsync(string resourceId, CancellationToken ct);
    Task<bool> IsLeaderAsync(string resourceId, CancellationToken ct);
}
```

**Providers:**

| Package | Backend |
|---------|---------|
| `Excalibur.LeaderElection.SqlServer` | SQL Server |
| `Excalibur.LeaderElection.Redis` | Redis |
| `Excalibur.LeaderElection.Consul` | HashiCorp Consul |

---

## Part 3: Documentation Gaps Analysis

### Currently Documented (Existing Docs)

| Document | Status | Accuracy |
|----------|--------|----------|
| `intro.md` | Complete | Accurate |
| `getting-started.md` | Complete | Needs updates |
| `handlers.md` | Complete | Accurate |
| `pipeline.md` | Complete | Needs expansion |
| `actions-and-handlers.md` | Complete | Accurate |
| `message-context.md` | Complete | Accurate |
| `messagecontext-design.md` | Complete | Accurate |
| `excalibur/intro.md` | Complete | Accurate |
| `excalibur/domain-modeling.md` | Complete | Needs expansion |
| `excalibur/event-sourcing.md` | Complete | Needs expansion |

### Missing Documentation (Priority Order)

#### Critical (Must Have)

1. **Pipeline Profiles** - How to configure multiple pipelines with middleware ordering
2. **Result Types** - Complete `MessageResult` API with examples
3. **Transport Configuration** - All 6 transports with real options
4. **Saga Patterns** - Orchestration vs Choreography with examples
5. **Data Access** - `IDb` and `IDataRequest` patterns

#### Important (Should Have)

6. **Compliance & Encryption** - Key management providers and configuration
7. **Security Features** - Authentication, validation, rate limiting
8. **Audit Logging** - Stores and cloud exporters
9. **Observability** - OpenTelemetry integration
10. **Resilience** - Polly policies and patterns

#### Nice to Have

11. **Hosting** - Serverless platforms
12. **Caching** - Cache modes and configuration
13. **Leader Election** - Distributed coordination

---

## Part 4: Documentation Standards

### Code Examples Must

1. **Use actual API names** - `MessageKinds.Action` not `MessageKinds.Command`
2. **Show real options** - Property names from actual code
3. **Include registration** - Show `services.AddDispatch()` patterns
4. **Demonstrate results** - Show `MessageResult.Success()` and `.Failed()`

### Each Document Should Include

1. **Purpose** - Why this feature exists
2. **When to Use** - Appropriate scenarios
3. **Installation** - NuGet package names
4. **Configuration** - All options with defaults
5. **Examples** - Working code samples
6. **Best Practices** - Recommendations from code patterns

### Avoid

1. References to internal sprint numbers
2. Placeholder URLs (your-org)
3. Assumed API that doesn't exist
4. Incomplete configuration examples

---

*This inventory is the authoritative source for documentation accuracy. All new documentation must reference the actual types and properties listed here.*

