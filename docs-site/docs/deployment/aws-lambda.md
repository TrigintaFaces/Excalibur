---
title: AWS Lambda Deployment
description: Run Excalibur on AWS Lambda with API Gateway and SQS handlers, Native AOT, scheduled outbox processing, and Step Functions sagas.
---
# AWS Lambda Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** AWS Lambda (Serverless)
**Last Updated:** 2026-01-01

---

The compiler-checked examples on this page use the optional dependency/compiler profile of the existing `samples/05-serverless/AwsLambda/AwsLambda.csproj` project, together with the normal Dispatch runtime references. They do not borrow its application types or demonstrate a deployed cloud host or successful NativeAOT publication. The scheduled outbox entry point also requires `Amazon.Lambda.CloudWatchEvents`; tracing below uses `Excalibur.Dispatch.Observability`, not the legacy X-Ray recorder packages.

The order examples declare an application-owned `IOrderApplication`; register your real implementation in the host before startup. These snippets supply contracts, not a simulated business service.

## Before You Start

- **.NET 10.0**
- An AWS account with Lambda access
- AWS CLI and SAM CLI installed locally
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

Deploy Excalibur applications to AWS Lambda for serverless, event-driven workloads with automatic scaling and pay-per-execution pricing.

**Use AWS Lambda when:**
- Event-driven processing (API Gateway, SQS, SNS, EventBridge, S3)
- Variable or unpredictable load
- Cost optimization for low-traffic scenarios
- Multi-region deployment; measure latency to callers and dependencies

---

## Invocation lifecycle and dependencies

`AddAwsLambdaServerless()` registers the platform adapter, cold-start optimizer and validated hosting options; it does not compose messaging, persistence, health endpoints or trigger settlement. The `AddExcaliburAwsLambdaServerless()` entry point forwards to those same services. Configure `AddDispatch(...)` and the domain/provider services your function uses explicitly. See the [registration boundary](https://github.com/TrigintaFaces/Excalibur/blob/main/src/Dispatch/Excalibur.Dispatch.Hosting.AwsLambda/README.md).

| Concern | Application and host responsibility |
|---|---|
| Connection reuse | Keep the application container and thread-safe SDK clients for the host instance rather than rebuilding them per message. The selected transport owns its registered clients; do not dispose them after each invocation. Open and dispose database connections per operation so the driver's pool can reuse connections. Size each pool against maximum active instances and per-instance concurrency: scale-out creates more pools, not one shared application-wide limit. See [pool tuning](../operations/performance-tuning.md#connection-pool-tuning) and [AWS connection-reuse guidance](https://docs.aws.amazon.com/lambda/latest/dg/best-practices.html). |
| Health | Hosting registration does not register an endpoint or a dependency probe. With the corresponding transport registry configured, opt in to `AddHealthChecks().AddTransportHealthChecks()`; with a claim-check provider configured, `AddClaimCheckHealthCheck()` exercises a real store/retrieve/delete probe. Route protected health requests through the platform's HTTP surface or run explicit checks from monitoring. A platform process-health signal is not proof that the broker, database or claim-check storage is usable. |
| Invocation limits | Set timeout, memory and concurrency in the platform deployment. `ServerlessHostOptions.MemoryLimitMB` validates a supplied value; it does not allocate platform memory. `ExecutionTimeout` only caps the budget when invoking through `IServerlessHostProvider.ExecuteAsync`, which combines cancellation with the context's remaining time and cleanup reserve. Calling `IDispatcher` directly does not automatically use that wrapper. Propagate cancellation, await required work before returning, and make effects idempotent for trigger redelivery. |
| Freeze, resume and replacement | Treat in-memory state and timers as temporary. A host may stop scheduling your code or replace the instance; completing an invocation must not depend on a later in-process timer. Persist essential work before returning and drive durable outbox work from a platform trigger with background polling disabled. Reused clients may need their documented stale-connection recovery; initialization/warmup is not proof a connection is still live after an idle period. See [AWS execution-environment guidance](https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtime-environment.html). |
| Invocation scope and failures | Resolve scoped handler dependencies from the current invocation/message scope; do not cache them in a shared function object. Dispose application-owned scopes after awaited work, using async disposal when their services require it. The SDK trigger adapter owns acknowledgement, retries and batch responses. Check dispatch results before reporting success when using a result-returning path; a caught exception and a failed result are different observations. |

The repository's local adapter and sample tests demonstrate their selected DI, cancellation and handler paths; they do not simulate a cloud freeze, service settlement or scale-out. In the AWS SQS sample, partial failures are expressed with the SDK's `SQSBatchResponse`; Azure and Google trigger retry behavior comes from their host configuration. Application command/event types, handlers, business services and resource names below are illustrative application code, not extra services installed by the hosting registration.

## Quick Start

### HTTP API with Lambda

```csharp ignore
// Function.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Excalibur.Dispatch;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

public class Function
{
    private readonly IDispatcher _dispatcher;

    public Function()
    {
        // Initialize DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.ValidateStartupGates();

        _dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddDispatch(options =>
        {
            options.AddHandlersFromAssembly(typeof(CreateOrderCommandHandler).Assembly);
        });

        services.AddScoped<Excalibur.Dispatch.Delivery.IActionHandler<CreateOrderCommand>, CreateOrderCommandHandler>();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing request: {request.RequestContext.RequestId}");

        try
        {
            var command = JsonSerializer.Deserialize<CreateOrderCommand>(request.Body);
            var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

            return new APIGatewayProxyResponse
            {
                StatusCode = result.Succeeded ? 200 : 500,
                Body = JsonSerializer.Serialize(new { succeeded = result.Succeeded }),
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }
            };
        }
        catch (Exception ex)
        {
            // Log the detail; never return it. An exception message can carry connection
            // strings, SQL or file paths.
            context.Logger.LogError(ex.ToString());
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "An internal error occurred." })
            };
        }
    }
}
```

:::note Fail-fast on a host-less container
This function builds the container with `BuildServiceProvider()` and never starts an `IHost`, so the framework's fail-fast startup gates (missing-provider prerequisite checks and the durability gates registered with `ValidateOnStart()`) do not fire on their own. Calling `ValidateStartupGates()` immediately after building the provider runs them once — surfacing a misconfiguration at cold start instead of at first message. See [Startup Prerequisite Validation](../core-concepts/dependency-injection.md#host-less-containers-must-trigger-the-gates-explicitly). On the isolated-worker model (which builds an `IHost` and calls `StartAsync`), the gates already run and this call is unnecessary.
:::

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>

    <!-- Managed Lambda class library; Native AOT needs an executable bootstrap (see below). -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.CloudWatchEvents" Version="4.4.1" />
    <PackageReference Include="Amazon.Lambda.Core" Version="2.3.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.3" />
    <PackageReference Include="Amazon.Lambda.APIGatewayEvents" Version="2.7.1" />
    <PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.301" />

    <PackageReference Include="Excalibur.Dispatch" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### Deploy with AWS CLI

```bash
# Build and package
dotnet lambda package -c Release -o function.zip

# Create Lambda function
aws lambda create-function \
  --function-name your-function \
  --runtime dotnet10 \
  --role arn:aws:iam::ACCOUNT_ID:role/lambda-execution-role \
  --handler YourAssembly::YourNamespace.Function::FunctionHandler \
  --zip-file fileb://function.zip \
  --environment Variables={SqlConnectionString=...}

# Invoke test
aws lambda invoke \
  --function-name your-function \
  --payload '{"body":"{\"orderId\":\"123\"}"}' \
  response.json
```

---

## Native AOT Compilation

### Earlier Unverified Illustration

The following estimates are not measured framework cloud results. Cold-start time, package size and memory use depend on the workload and deployment; measure the deployed function before relying on them.

- **90% faster cold starts**: ~100ms vs 1-3 seconds
- **70% smaller package**: ~10MB vs 35MB
- **Lower memory usage**: ~50MB vs 150MB

### Executable Lambda bootstrap and deployment

The managed class-library quickstart above is not an executable Native AOT Lambda application. Setting `PublishAot` and zipping that library's publish directory does not supply the Lambda Runtime API bootstrap.

Start from AWS's [Native AOT Lambda template and deployment procedure](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html) for the supported runtime and architecture you select. Its executable `Main` creates `LambdaBootstrapBuilder`, binds the handler and runs the runtime client. Configure `SourceGeneratorLambdaJsonSerializer<TContext>` with generated metadata for every input/output type. Build using the matching Amazon Linux environment and architecture described by AWS, and use the template's Lambda tooling/package procedure. Preserve those bootstrap and serialization requirements when adding the framework's generated registration path; the managed assembly-scanning examples on this page are not Native AOT certification.

Measure and test the actual deployed workload. Local managed tests and a successful generic `dotnet publish` alone do not prove a Lambda bootstrap, cloud invocation, trimming compatibility or the illustrative timing figures below.

**Earlier unverified cold-start illustration (not measured framework cloud results):**

| Runtime | Package Size | Cold Start | Memory |
|---------|--------------|------------|--------|
| .NET 10 (JIT) | 35 MB | 1-3 seconds | 150 MB |
| .NET 10 (AOT) | 10 MB | 100-300 ms | 50 MB |

---

## SQS Queue Processing

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

services.AddAwsS3ClaimCheck(options =>
{
    options.BucketName = "your-payload-bucket";
});
```

Two consequences worth planning for rather than discovering: the handler's storage read is now on the
critical path of every oversized message, and the retention policy on that bucket becomes part of your
delivery guarantee — a payload collected by a lifecycle rule before its message is consumed is a message
that can no longer be processed. Set the bucket's retention longer than your queue's maximum retention plus
your dead-letter window.


### SQS Event Handler

```csharp ignore
// SqsFunction.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Excalibur.Dispatch;

public class SqsFunction
{
    private readonly IDispatcher _dispatcher;

    public SqsFunction()
    {
        var services = new ServiceCollection();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(SqsFunction).Assembly);
        });

        var serviceProvider = services.BuildServiceProvider();
        _dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
    }

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            context.Logger.LogInformation($"Processing message: {record.MessageId}");

            try
            {
                var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                    record.Body);

                var result = await _dispatcher.DispatchAsync(integrationEvent, CancellationToken.None);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Integration event dispatch failed.");
                }

                context.Logger.LogInformation($"Message processed: {record.MessageId}");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing message {record.MessageId}: {ex.Message}");
                throw;  // Message returns to queue for retry
            }
        }
    }
}
```

**Package reference:**

```xml
<PackageReference Include="Amazon.Lambda.SQSEvents" Version="2.2.0" />
```

**Event source mapping:**

```bash
aws lambda create-event-source-mapping \
  --function-name sqs-processor \
  --event-source-arn arn:aws:sqs:us-east-1:ACCOUNT_ID:integration-events \
  --batch-size 10 \
  --maximum-batching-window-in-seconds 5
```

---

## EventBridge Scheduled Events

### Timer-Triggered Outbox Processing

```csharp
// OutboxProcessorFunction.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.CloudWatchEvents;
using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;
using Microsoft.Extensions.DependencyInjection;

public class OutboxProcessorFunction
{
    private readonly IOutboxProcessor _outboxProcessor;

    public OutboxProcessorFunction()
    {
        var services = new ServiceCollection();

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
            ?? throw new InvalidOperationException("SqlConnectionString is required.");

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

        services.AddLogging();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(OutboxProcessorFunction).Assembly);
            // Configure the outbound transport required by your messages here.
        });

        var serviceProvider = services.BuildServiceProvider();
        _outboxProcessor = serviceProvider.GetRequiredService<IOutboxProcessor>();
        _outboxProcessor.Init($"serverless-outbox-{Guid.NewGuid():N}");
    }

    public async Task FunctionHandler(CloudWatchEvent<object> cloudWatchEvent, ILambdaContext context)
    {
        context.Logger.LogInformation("Outbox processor triggered");

        try
        {
            var processedCount = await _outboxProcessor.DispatchPendingMessagesAsync(
                CancellationToken.None);

            context.Logger.LogInformation($"Processed {processedCount} outbox messages");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Outbox processing failed: {ex.Message}");
            throw;
        }
    }
}
```

**EventBridge rule:**

```bash
# Create rule (every 1 minute)
aws events put-rule \
  --name outbox-processor-schedule \
  --schedule-expression "rate(1 minute)"

# Add Lambda target
aws events put-targets \
  --rule outbox-processor-schedule \
  --targets "Id"="1","Arn"="arn:aws:lambda:us-east-1:ACCOUNT_ID:function:outbox-processor"

# Grant EventBridge permission
aws lambda add-permission \
  --function-name outbox-processor \
  --statement-id eventbridge-invoke \
  --action lambda:InvokeFunction \
  --principal events.amazonaws.com \
  --source-arn arn:aws:events:us-east-1:ACCOUNT_ID:rule/outbox-processor-schedule
```

---

## Step Functions for Sagas

### Order Saga State Machine

```json
{
  "Comment": "Order processing saga with compensation",
  "StartAt": "ReserveInventory",
  "States": {
    "ReserveInventory": {
      "Type": "Task",
      "Resource": "arn:aws:lambda:us-east-1:ACCOUNT_ID:function:reserve-inventory",
      "Catch": [
        {
          "ErrorEquals": ["States.ALL"],
          "ResultPath": "$.error",
          "Next": "ReservationFailed"
        }
      ],
      "Next": "ProcessPayment"
    },
    "ProcessPayment": {
      "Type": "Task",
      "Resource": "arn:aws:lambda:us-east-1:ACCOUNT_ID:function:process-payment",
      "Catch": [
        {
          "ErrorEquals": ["States.ALL"],
          "ResultPath": "$.error",
          "Next": "ReleaseInventory"
        }
      ],
      "Next": "CreateShipment"
    },
    "CreateShipment": {
      "Type": "Task",
      "Resource": "arn:aws:lambda:us-east-1:ACCOUNT_ID:function:create-shipment",
      "End": true
    },
    "ReleaseInventory": {
      "Type": "Task",
      "Resource": "arn:aws:lambda:us-east-1:ACCOUNT_ID:function:release-inventory",
      "Next": "PaymentFailed"
    },
    "ReservationFailed": {
      "Type": "Fail",
      "Cause": "Inventory reservation failed"
    },
    "PaymentFailed": {
      "Type": "Fail",
      "Cause": "Payment processing failed"
    }
  }
}
```

### Lambda Activity Functions

```csharp
// ReserveInventoryFunction.cs
using Amazon.Lambda.Core;

public class ReserveInventoryFunction
{
    private readonly IInventoryService _inventoryService;

    public async Task<InventoryReservation> FunctionHandler(
        OrderInput input,
        ILambdaContext context)
    {
        context.Logger.LogInformation($"Reserving inventory for order {input.OrderId}");

        var reservation = await _inventoryService.ReserveAsync(input.Items);

        if (!reservation.Success)
        {
            throw new InsufficientInventoryException(
                $"Insufficient inventory for order {input.OrderId}");
        }

        return reservation;
    }
}

// ProcessPaymentFunction.cs
public class ProcessPaymentFunction
{
    private readonly IPaymentGateway _paymentGateway;

    public async Task<PaymentResult> FunctionHandler(
        PaymentInput input,
        ILambdaContext context)
    {
        context.Logger.LogInformation($"Processing payment for order {input.OrderId}");

        var result = await _paymentGateway.ChargeAsync(input.Amount, input.Token);

        if (!result.Success)
        {
            throw new PaymentDeclinedException(
                $"Payment declined for order {input.OrderId}");
        }

        return result;
    }
}
```

**Start execution:**

```bash
aws stepfunctions start-execution \
  --state-machine-arn arn:aws:states:us-east-1:ACCOUNT_ID:stateMachine:OrderSaga \
  --input '{"orderId":"123","items":[...],"payment":{...}}'
```

---

## DynamoDB Event Sourcing

Install `Excalibur.EventSourcing.DynamoDb` and use the existing event-sourcing builder alongside the function's `AddDispatch(...)` composition:

```csharp
using Amazon;
using Excalibur.EventSourcing.DynamoDb;
using Microsoft.Extensions.DependencyInjection;

public static class DynamoDbRegistration
{
    public static void Configure(IServiceCollection services) =>
        services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
            es.UseDynamoDb(dynamo => dynamo
                .Region(RegionEndpoint.USEast1)
                .TableName("event-store"))));
}
```

This selects the framework's DynamoDB event store; it does not create a custom `IEventStore` or adapt arbitrary DynamoDB documents. Follow [DynamoDB provider setup](../event-sourcing/providers.md#amazon-dynamodb) for credentials, table/key provisioning and repository registration. Use the provider's documented change-feed path for DynamoDB Streams and the [projection guide](../event-sourcing/projections.md) for projection composition. The serverless hosting registration does not install a Streams consumer or convert its records into domain events for you.

---

## Secrets Management

### AWS Secrets Manager Integration

```csharp
// Program.cs or Function constructor
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

var client = new AmazonSecretsManagerClient();
var request = new GetSecretValueRequest
{
    SecretId = "prod/your-app/connection-string"
};

var response = await client.GetSecretValueAsync(request);
var connectionString = response.SecretString;

// Use in service configuration
services.AddSqlServerOutboxStore(options =>
{
    options.ConnectionString = connectionString;
});
```

**IAM policy for Lambda:**

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:prod/your-app/*"
    }
  ]
}
```

### Environment Variables (Simple Secrets)

```bash
aws lambda update-function-configuration \
  --function-name your-function \
  --environment Variables={SqlConnectionString=...,ApiKey=...}
```

---

## Monitoring and Logging

### CloudWatch Logs

```csharp
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

public sealed class StructuredLoggingFunction
{
    public Task FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation($"Request: {request.RequestContext.RequestId}");
        try
        {
            // Illustrative structured JSON log; Lambda sends stdout to its configured log destination.
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                level = "INFO",
                message = "Request received",
                requestId = request.RequestContext.RequestId
            }));
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.ToString());
            throw;
        }
    }
}
```

### Dispatch tracing with OpenTelemetry

```xml
<PackageReference Include="Excalibur.Dispatch.Observability" Version="10.0.0" />
```

This configures Dispatch activity sources and meters for an OTLP collector. It does not register AWS SDK-wide X-Ray auto-instrumentation. Configure the collector, credentials and exporter lifecycle for your Lambda host separately; see [AWS OpenTelemetry guidance](https://docs.aws.amazon.com/xray/latest/devguide/xray-services-adot.html).

```csharp
using Excalibur.Dispatch.Observability.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

public static class LambdaDispatchTelemetry
{
    public static void Configure(IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .AddDispatchInstrumentation()
            .WithTracing(tracing => tracing.AddOtlpExporter());
    }
}
```

**Enable X-Ray:**

```bash
aws lambda update-function-configuration \
  --function-name your-function \
  --tracing-config Mode=Active
```

---

## Performance Optimization

### Provisioned Concurrency

```bash
# Configure provisioned concurrency
aws lambda put-provisioned-concurrency-config \
  --function-name your-function \
  --provisioned-concurrent-executions 5 \
  --qualifier your-published-version-or-alias
```

**Earlier illustrative cost:** ~$0.0000041667 per GB-second (always running); not a current price quote.
**Benefit:** Pre-initialized environments for the configured capacity. Spillover invocations and environment resets can still experience cold starts. Use a published version or alias for the qualifier. See [AWS concurrency guidance](https://docs.aws.amazon.com/lambda/latest/dg/lambda-concurrency.html).

### Lambda SnapStart

```bash
# Enable SnapStart for a supported runtime and deployment
aws lambda update-function-configuration \
  --function-name your-function \
  --snap-start ApplyOn=PublishedVersions
```

**Earlier unverified estimate:** ~90% cold start reduction (100ms vs 1-3s). This is not a measured framework cloud result or a promise for this function. Check [SnapStart runtime and deployment requirements](https://docs.aws.amazon.com/lambda/latest/dg/snapstart.html) and measure the actual workload.

#### The framework side of SnapStart

The AWS CLI call above enables SnapStart on the function. It does not put anything of this framework's
into the snapshot. Two steps do that, and without them a restored environment still pays dependency
injection, JIT and SDK initialization on its first invocation — the cost SnapStart exists to remove.

Register the Lambda hosting services, which seat the Lambda host provider and the cold-start optimizer:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddAwsLambdaServerless();
```

Then register the optimizer's warmup so it runs while the snapshot is being taken, from your function's
construction or initialization path — a static initializer or the handler type's constructor — rather than
from a first invocation:

```csharp
using Excalibur.Dispatch.Hosting.AwsLambda;
using Excalibur.Dispatch.Hosting.Serverless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class Function
{
    private readonly IServiceProvider _services;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _ = services.AddAwsLambdaServerless();
        // ... your own registrations

        _services = services.BuildServiceProvider();

        AwsLambdaSnapStartHooks.RegisterWarmup(
            _services.GetRequiredService<IColdStartOptimizer>(),
            _services.GetRequiredService<ILoggerFactory>().CreateLogger<Function>());
    }
}
```

`RegisterWarmup` is **fail-open**: on a runtime without snapshot-hook support it logs a warning and
continues, and the warmup then runs on the first invocation exactly as it would have anyway. So adding the
call is safe on a function that is not using SnapStart.

What stays yours either way: enabling SnapStart on the function, restoring network state after a restore,
and re-deriving anything that must be unique per environment — a random seed captured into a snapshot is
shared by every environment restored from it.

Cold-start optimization is enabled from the `AWS_LAMBDA_FUNCTION_NAME` environment variable, which Lambda
sets on every invocation, so it switches itself on when running on Lambda and off elsewhere. Nothing needs
configuring for that.


---

## Deployment Automation

### SAM Template

```yaml
# template.yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31

Globals:
  Function:
    Runtime: dotnet10
    Timeout: 30
    MemorySize: 512
    Environment:
      Variables:
        SqlConnectionString: !Sub '{{resolve:secretsmanager:prod/app/db:SecretString:connectionString}}'

Resources:
  ProcessOrderFunction:
    Type: AWS::Serverless::Function
    Properties:
      Handler: YourAssembly::YourNamespace.Function::FunctionHandler
      CodeUri: ./bin/Release/net10.0/publish/
      Events:
        ApiEvent:
          Type: Api
          Properties:
            Path: /orders
            Method: post
      Policies:
        - AWSSecretsManagerGetSecretValuePolicy:
            SecretArn: !Sub 'arn:aws:secretsmanager:${AWS::Region}:${AWS::AccountId}:secret:prod/app/*'

  SqsProcessorFunction:
    Type: AWS::Serverless::Function
    Properties:
      Handler: YourAssembly::YourNamespace.SqsFunction::FunctionHandler
      CodeUri: ./bin/Release/net10.0/publish/
      Events:
        SqsEvent:
          Type: SQS
          Properties:
            Queue: !GetAtt IntegrationEventsQueue.Arn
            BatchSize: 10

  IntegrationEventsQueue:
    Type: AWS::SQS::Queue
    Properties:
      QueueName: integration-events
      VisibilityTimeout: 60

Outputs:
  ApiUrl:
    Description: "API Gateway endpoint URL"
    Value: !Sub "https://${ServerlessRestApi}.execute-api.${AWS::Region}.amazonaws.com/Prod/orders"
```

**Deploy:**

```bash
# Build
dotnet publish -c Release

# Deploy with SAM
sam deploy --guided
```

### GitHub Actions CI/CD

```yaml
name: Deploy to AWS Lambda

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

      - name: Install Lambda tools
        run: dotnet tool install -g Amazon.Lambda.Tools

      - name: Deploy to Lambda
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          AWS_REGION: us-east-1
        run: |
          dotnet lambda deploy-function \
            --function-name your-function \
            --function-role lambda-execution-role
```

---

## Troubleshooting

### Cold Start Issues

```bash
# Check execution duration
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Duration \
  --dimensions Name=FunctionName,Value=your-function \
  --start-time 2026-01-01T00:00:00Z \
  --end-time 2026-01-01T23:59:59Z \
  --period 3600 \
  --statistics Average,Maximum

# Enable Provisioned Concurrency or use Native AOT
```

### Timeout Errors

```bash
# Increase timeout
aws lambda update-function-configuration \
  --function-name your-function \
  --timeout 60  # seconds (max 900)
```

:::info Fail-closed timeout boundary
The serverless host derives the per-invocation handler timeout from the remaining Lambda execution
time **minus a small cleanup reserve** (so logging, flushing, and disposal can complete before the
hard Lambda kill). When the remaining time is **at or below** that reserve, the host **cancels the
handler immediately** rather than running it unbounded — the timeout is *fail-closed*. This same
guarantee applies uniformly across the AWS Lambda, Azure Functions, and Google Cloud Functions hosts.
If you see handlers cancelled near the end of a near-exhausted invocation, increase the function
timeout above so each invocation has adequate headroom.
:::

### Memory Issues

```bash
# Increase memory (also increases CPU)
aws lambda update-function-configuration \
  --function-name your-function \
  --memory-size 1024  # MB (128-10240)
```

---

## Next Steps

- **Azure Functions:** [Azure Functions](azure-functions.md) for multi-cloud
- **Container:** [Deploy as container](docker.md) with Lambda Container Image
- **Monitoring:** [CloudWatch integration](../observability/aws-cloudwatch.md)
- **Security:** [IAM best practices](security-best-practices.md)

---

## See Also

- [Azure Functions Deployment](azure-functions.md) - Deploy to Azure Functions for multi-cloud serverless workloads
- [Google Cloud Functions Deployment](google-cloud-functions.md) - Deploy to Google Cloud Functions for GCP-based serverless workloads
- [AWS SQS Transport](../transports/aws-sqs.md) - Configure the AWS SQS transport for message publishing and consumption

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**AWS Lambda:** .NET 10 Runtime with Native AOT support
