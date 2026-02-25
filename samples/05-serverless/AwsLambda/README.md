# AWS Lambda Serverless Sample

This sample demonstrates how to use `Excalibur.Dispatch.Hosting.AwsLambda` for building serverless applications with AWS Lambda using the Excalibur framework.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- [Docker](https://www.docker.com/products/docker-desktop) (for local testing with SAM)
- [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)
- [LocalStack](https://docs.localstack.cloud/getting-started/installation/) (optional, for local AWS emulation)

### Installing Prerequisites

```bash
# Install AWS SAM CLI (macOS)
brew install aws-sam-cli

# Install AWS SAM CLI (Windows/Linux - via pip)
pip install aws-sam-cli

# Verify installation
sam --version
```

## Quick Start

### 1. Build the Project

```bash
dotnet build
```

### 2. Build with SAM

```bash
sam build
```

### 3. Run Locally with SAM

```bash
# Start local API Gateway
sam local start-api

# Or invoke a specific function
sam local invoke CreateOrderFunction --event events/create-order.json
```

### 4. Test the API

```bash
# Create an order (local)
curl -X POST http://127.0.0.1:3000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "ORD-001",
    "customerId": "CUST-100",
    "totalAmount": 99.99,
    "items": [
      {"productId": "PROD-1", "productName": "Widget", "quantity": 2, "unitPrice": 49.99}
    ]
  }'

# Get order (local)
curl http://127.0.0.1:3000/orders/ORD-001
```

## What This Sample Demonstrates

### API Gateway-Triggered Lambda

The `ApiGatewayHandler` demonstrates REST API operations:

```csharp
[LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
public async Task<APIGatewayProxyResponse> CreateOrderAsync(
    APIGatewayProxyRequest request,
    ILambdaContext context)
{
    var orderRequest = JsonSerializer.Deserialize<CreateOrderRequest>(request.Body);
    var orderEvent = new OrderCreatedEvent(...);
    await dispatcher.DispatchAsync(orderEvent, dispatchContext);
    // ...
}
```

### SQS-Triggered Lambda

The `SqsHandler` processes messages from SQS with batch failure reporting:

```csharp
[LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
public async Task<SQSBatchResponse> ProcessOrderEventsAsync(
    SQSEvent sqsEvent,
    ILambdaContext context)
{
    foreach (var record in sqsEvent.Records)
    {
        var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(record.Body);
        await dispatcher.DispatchAsync(orderEvent, dispatchContext);
    }
    // Returns batch item failures for retry
}
```

### EventBridge-Triggered Lambda

The `EventBridgeHandler` handles scheduled events:

```csharp
[LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
public async Task GenerateDailyReportAsync(
    ScheduledEvent scheduledEvent,
    ILambdaContext context)
{
    var taskEvent = new ScheduledTaskEvent("DailyReport", ...);
    await dispatcher.DispatchAsync(taskEvent, dispatchContext);
}
```

## Project Structure

```
AwsLambda/
├── Functions/
│   ├── ApiGatewayHandler.cs       # HTTP API triggers
│   ├── SqsHandler.cs              # SQS queue triggers
│   └── EventBridgeHandler.cs      # Scheduled event triggers
├── Messages/
│   └── OrderMessages.cs           # Event definitions
├── Handlers/
│   ├── OrderCreatedEventHandler.cs
│   └── ScheduledTaskHandler.cs
├── Startup.cs                     # DI configuration
├── AwsLambda.csproj
├── template.yaml                  # SAM template
├── samconfig.toml                 # SAM configuration
└── README.md
```

## SAM Template Resources

| Resource | Type | Description |
|----------|------|-------------|
| `OrdersApi` | HTTP API | API Gateway for REST endpoints |
| `CreateOrderFunction` | Lambda | POST /orders handler |
| `GetOrderFunction` | Lambda | GET /orders/{orderId} handler |
| `OrderEventsQueue` | SQS | Order events queue |
| `OrderEventsDeadLetterQueue` | SQS | DLQ for failed messages |
| `ProcessOrderEventsFunction` | Lambda | SQS message processor |
| `DailyReportRule` | EventBridge | Daily cron trigger |
| `DailyReportFunction` | Lambda | Report generation |
| `HourlyHealthCheckRule` | EventBridge | Hourly health check |
| `HourlyHealthCheckFunction` | Lambda | Health monitoring |

## Cold Start Optimization

This sample uses `Excalibur.Dispatch.Hosting.AwsLambda` with cold start optimization:

```csharp
services.AddExcaliburAwsLambdaServerless(opts =>
{
    opts.EnableColdStartOptimization = true;
    opts.AwsLambda.Runtime = "dotnet8";
});
```

### Best Practices

1. **Use .NET 8** - Best cold start performance for managed runtime
2. **Consider ARM64** - Lower cost and often better cold start
3. **Minimize dependencies** - Only include necessary packages
4. **Use Provisioned Concurrency** - For latency-sensitive workloads
5. **Keep Lambda warm** - Use EventBridge ping for critical functions

## Deployment to AWS

### Validate Template

```bash
sam validate --lint
```

### Deploy to AWS

```bash
# Deploy to development
sam deploy --config-env dev

# Deploy to staging
sam deploy --config-env staging

# Deploy to production (requires confirmation)
sam deploy --config-env prod
```

### View Deployed Resources

```bash
# List stack outputs
aws cloudformation describe-stacks \
    --stack-name dispatch-lambda-sample-dev \
    --query 'Stacks[0].Outputs'

# Get API endpoint
aws cloudformation describe-stacks \
    --stack-name dispatch-lambda-sample-dev \
    --query 'Stacks[0].Outputs[?OutputKey==`ApiEndpoint`].OutputValue' \
    --output text
```

## Local Development with LocalStack

### Start LocalStack

```bash
docker run --rm -it -p 4566:4566 -p 4510-4559:4510-4559 localstack/localstack
```

### Deploy to LocalStack

```bash
# Configure SAM to use LocalStack
export AWS_ENDPOINT_URL=http://localhost:4566

# Deploy
samlocal deploy --guided
```

## Monitoring

### CloudWatch Logs

```bash
# View function logs
sam logs -n CreateOrderFunction --stack-name dispatch-lambda-sample-dev --tail

# View all function logs
aws logs tail /aws/lambda/dispatch-create-order-dev --follow
```

### X-Ray Tracing

X-Ray tracing is enabled by default (`Tracing: Active` in template.yaml). View traces in the AWS X-Ray console.

## Testing

### Create Test Events

```bash
# Create events directory
mkdir -p events

# Create order event
cat > events/create-order.json << 'EOF'
{
  "body": "{\"orderId\":\"ORD-001\",\"customerId\":\"CUST-100\",\"totalAmount\":99.99}",
  "requestContext": {
    "http": { "method": "POST", "path": "/orders" }
  }
}
EOF
```

### Invoke Locally

```bash
sam local invoke CreateOrderFunction --event events/create-order.json
```

## Troubleshooting

### Build Failures

```bash
# Clean and rebuild
dotnet clean
sam build --use-container
```

### Function Timeout

Increase timeout in `template.yaml`:
```yaml
Globals:
  Function:
    Timeout: 60  # Increase from 30
```

### SQS Messages Not Processing

1. Check DLQ for failed messages
2. Verify IAM permissions
3. Check Lambda execution logs

## Related Samples

- [Azure Functions](../AzureFunctions/) - Azure serverless
- [Google Cloud Functions](../GoogleCloudFunctions/) - GCP serverless

## Learn More

- [AWS Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html)
- [AWS SAM Documentation](https://docs.aws.amazon.com/serverless-application-model/)
- [Excalibur.Dispatch.Hosting.AwsLambda Package](../../../src/Dispatch/Excalibur.Dispatch.Hosting.AwsLambda/)
- [LocalStack Documentation](https://docs.localstack.cloud/)
