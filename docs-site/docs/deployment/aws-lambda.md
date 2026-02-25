# AWS Lambda Deployment

**Framework:** Excalibur.Dispatch
**Deployment Target:** AWS Lambda (Serverless)
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An AWS account with Lambda access
- AWS CLI and SAM CLI installed locally
- Familiarity with [ASP.NET Core deployment](./aspnet-core.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Overview

Deploy Excalibur applications to AWS Lambda for serverless, event-driven workloads with automatic scaling and pay-per-execution pricing.

**Use AWS Lambda when:**
- Event-driven processing (API Gateway, SQS, SNS, EventBridge, S3)
- Variable or unpredictable load
- Cost optimization for low-traffic scenarios
- Multi-region deployment with low latency

---

## Quick Start

### HTTP API with Lambda

```csharp
// Function.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Excalibur.Dispatch.Abstractions;
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

        _dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddDispatch(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        services.AddScoped<CreateOrderCommandHandler>();
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
                StatusCode = 200,
                Body = JsonSerializer.Serialize(result),
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json"
                }
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
    }
}
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>

    <!-- Enable Native AOT (optional, significantly reduces cold start) -->
    <PublishAot>true</PublishAot>
    <StripSymbols>true</StripSymbols>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="2.3.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.3" />
    <PackageReference Include="Amazon.Lambda.APIGatewayEvents" Version="2.7.1" />
    <PackageReference Include="AWSSDK.Extensions.NETCore.Setup" Version="3.7.301" />

    <PackageReference Include="Excalibur.Dispatch" Version="1.0.0" />
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
  --runtime dotnet9 \
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

### Benefits

- **90% faster cold starts**: ~100ms vs 1-3 seconds
- **70% smaller package**: ~10MB vs 35MB
- **Lower memory usage**: ~50MB vs 150MB

### Configuration

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <StripSymbols>true</StripSymbols>
  <InvariantGlobalization>true</InvariantGlobalization>
  <IlcOptimizationPreference>Speed</IlcOptimizationPreference>
  <IlcGenerateDgmlFile>false</IlcGenerateDgmlFile>
</PropertyGroup>
```

### Publish

```bash
# Publish with Native AOT
dotnet publish -c Release -r linux-x64 /p:PublishAot=true

# Create deployment package
cd bin/Release/net9.0/linux-x64/publish
zip -r ../../../../function.zip .

# Deploy
aws lambda update-function-code \
  --function-name your-function \
  --zip-file fileb://function.zip
```

**Cold start comparison:**

| Runtime | Package Size | Cold Start | Memory |
|---------|--------------|------------|--------|
| .NET 9 (JIT) | 35 MB | 1-3 seconds | 150 MB |
| .NET 9 (AOT) | 10 MB | 100-300 ms | 50 MB |

---

## SQS Queue Processing

### SQS Event Handler

```csharp
// SqsFunction.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Excalibur.Dispatch.Abstractions;

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

                await _dispatcher.DispatchAsync(integrationEvent, CancellationToken.None);

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
using Excalibur.Outbox;

public class OutboxProcessorFunction
{
    private readonly IOutboxProcessor _outboxProcessor;

    public OutboxProcessorFunction()
    {
        var services = new ServiceCollection();

        // Configure SQL Server Outbox
        services.AddSqlServerOutboxStore(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        });

        services.AddSingleton<IOutboxPublisher, MessageBusOutboxPublisher>();

        var serviceProvider = services.BuildServiceProvider();
        _outboxProcessor = serviceProvider.GetRequiredService<IOutboxProcessor>();
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

### DynamoDB Event Store

```csharp
// DynamoDbEventStore.cs
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

public class DynamoDbEventStore : IEventStore
{
    private readonly Table _table;

    public DynamoDbEventStore(IAmazonDynamoDB client, string tableName)
    {
        _table = Table.LoadTable(client, tableName);
    }

    public async Task AppendAsync(
        string aggregateId,
        IEnumerable<IDomainEvent> events,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var transactItems = new List<TransactWriteItem>();

        foreach (var @event in events)
        {
            var document = new Document
            {
                ["AggregateId"] = aggregateId,
                ["Version"] = @event.Version,
                ["EventType"] = @event.EventType,
                ["EventData"] = JsonSerializer.Serialize(@event),
                ["OccurredAt"] = @event.OccurredAt.ToString("o")
            };

            transactItems.Add(new TransactWriteItem
            {
                Put = new Put
                {
                    TableName = _table.TableName,
                    Item = Document.ToAttributeMap(document),
                    ConditionExpression = "attribute_not_exists(AggregateId) AND attribute_not_exists(Version)"
                }
            });
        }

        try
        {
            var request = new TransactWriteItemsRequest { TransactItems = transactItems };
            await _table.DynamoDBContext.Client.TransactWriteItemsAsync(request, cancellationToken);
        }
        catch (TransactionCanceledException)
        {
            throw new ConcurrencyException($"Concurrency conflict for aggregate {aggregateId}");
        }
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(
        string aggregateId,
        CancellationToken cancellationToken)
    {
        var search = _table.Query(aggregateId, new QueryFilter());
        var documents = await search.GetRemainingAsync(cancellationToken);

        var events = new List<IDomainEvent>();
        foreach (var doc in documents.OrderBy(d => (int)d["Version"]))
        {
            var eventType = Type.GetType((string)doc["EventType"]);
            var eventData = (string)doc["EventData"];
            var @event = JsonSerializer.Deserialize(eventData, eventType) as IDomainEvent;
            events.Add(@event);
        }

        return events;
    }
}
```

**DynamoDB table definition:**

```bash
aws dynamodb create-table \
  --table-name EventStore \
  --attribute-definitions \
    AttributeName=AggregateId,AttributeType=S \
    AttributeName=Version,AttributeType=N \
  --key-schema \
    AttributeName=AggregateId,KeyType=HASH \
    AttributeName=Version,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST
```

### DynamoDB Streams for Projections

```csharp
// ProjectionFunction.cs
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

public class ProjectionFunction
{
    private readonly IProjectionService _projectionService;

    public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
    {
        foreach (var record in dynamoEvent.Records)
        {
            if (record.EventName == "INSERT")
            {
                var eventType = record.Dynamodb.NewImage["EventType"].S;
                var eventData = record.Dynamodb.NewImage["EventData"].S;

                var type = Type.GetType(eventType);
                var @event = JsonSerializer.Deserialize(eventData, type) as IDomainEvent;

                await _projectionService.ProjectAsync(@event, CancellationToken.None);
            }
        }
    }
}
```

**Enable DynamoDB Streams:**

```bash
aws dynamodb update-table \
  --table-name EventStore \
  --stream-specification StreamEnabled=true,StreamViewType=NEW_IMAGE

aws lambda create-event-source-mapping \
  --function-name projection-processor \
  --event-source-arn arn:aws:dynamodb:us-east-1:ACCOUNT_ID:table/EventStore/stream/... \
  --batch-size 100 \
  --starting-position LATEST
```

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
public async Task FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
{
    // Automatic CloudWatch Logs integration
    context.Logger.LogInformation($"Request: {request.RequestContext.RequestId}");
    context.Logger.LogError($"Error occurred: {ex.Message}");

    // Structured logging with Lambda Insights
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        level = "INFO",
        message = "Order processed",
        orderId = "123",
        duration = 150
    }));
}
```

### X-Ray Tracing

```xml
<PackageReference Include="AWSXRayRecorder.Core" Version="2.15.0" />
<PackageReference Include="AWSXRayRecorder.Handlers.AwsSdk" Version="2.12.0" />
```

```csharp
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;

// Initialize in constructor
AWSSDKHandler.RegisterXRayForAllServices();

// Instrument code
public async Task FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
{
    AWSXRayRecorder.Instance.BeginSubsegment("ProcessOrder");
    try
    {
        var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);
        return new APIGatewayProxyResponse { StatusCode = 200 };
    }
    finally
    {
        AWSXRayRecorder.Instance.EndSubsegment();
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

### Provisioned Concurrency (Eliminate Cold Starts)

```bash
# Configure provisioned concurrency
aws lambda put-provisioned-concurrency-config \
  --function-name your-function \
  --provisioned-concurrent-executions 5 \
  --qualifier LATEST
```

**Cost:** ~$0.0000041667 per GB-second (always running)
**Benefit:** Zero cold starts

### Lambda SnapStart (Java/.NET AOT)

```bash
# Enable SnapStart (for Native AOT functions)
aws lambda update-function-configuration \
  --function-name your-function \
  --snap-start ApplyOn=PublishedVersions
```

**Result:** ~90% cold start reduction (100ms vs 1-3s)

---

## Deployment Automation

### SAM Template

```yaml
# template.yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31

Globals:
  Function:
    Runtime: dotnet9
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
      CodeUri: ./bin/Release/net9.0/publish/
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
      CodeUri: ./bin/Release/net9.0/publish/
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
          dotnet-version: '9.0.x'

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
**AWS Lambda:** .NET 9 Runtime with Native AOT support
