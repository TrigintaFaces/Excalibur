# Enhanced Stores E-Commerce Sample

This sample demonstrates the enhanced stores (Inbox, Outbox, Schedule) in a realistic e-commerce scenario featuring order processing, email notifications, and inventory management.

## Features Demonstrated

### 🛒 **Order Processing with Enhanced Inbox Store**
- **Content-based deduplication** using SHA256 hashing (R9.51)
- **Advanced deduplication cache** with configurable size and time windows
- **Hot-path optimizations** for frequent operations
- **Duplicate order detection** and rejection

### 📧 **Email Notifications with Enhanced Outbox Store**
- **Batch staging** for efficient outbound message processing
- **Exponential backoff** for retry logic with configurable delays
- **Message priority** and staging optimization
- **Reliable email delivery** with failure handling

### 📦 **Inventory Management with Enhanced Schedule Store**
- **Execution time indexing** for fast scheduled task lookup
- **Duplicate detection** for scheduled inventory checks
- **Batch operations** for bulk scheduling scenarios
- **Automated cleanup** of completed schedules

### 📊 **Comprehensive Observability**
- **OpenTelemetry integration** with distributed tracing (R8.21)
- **Structured metrics collection** (counters, histograms, gauges)
- **Activity creation** for all major operations
- **Performance monitoring** and alerting

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ OrderProcessing │    │ Notification    │    │ Inventory       │
│ Service         │    │ Service         │    │ Service         │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          ▼                      ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Enhanced        │    │ Enhanced        │    │ Enhanced        │
│ Inbox Store     │    │ Outbox Store    │    │ Schedule Store  │
│ - Deduplication │    │ - Batch Staging │    │ - Time Indexing │
│ - Hot-path Opts │    │ - Exp. Backoff  │    │ - Bulk Ops      │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          ▼                      ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ In-Memory       │    │ In-Memory       │    │ In-Memory       │
│ Inbox Store     │    │ Outbox Store    │    │ Schedule Store  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Project Structure

```
ECommerceSample/
├── Program.cs                    # Application entry point and configuration
├── Services/
│   └── OrderProcessingService.cs # Business services (OrderProcessing, Notification, Inventory)
├── Infrastructure/
│   ├── InMemoryRepositories.cs  # Sample data repositories
│   ├── InMemoryStores.cs        # In-memory store implementations
│   └── HealthChecks.cs          # Health check implementations
├── HostedServices/
│   └── OrderProcessorHostedService.cs # Background processing services
├── ECommerceSample.csproj       # Project file
└── README.md                    # This file
```

## Running the Sample

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code

### Build and Run

```bash
# Build the sample
dotnet build

# Run the sample
dotnet run
```

### Sample Output

```
🛒 E-Commerce Enhanced Stores Sample
=====================================

🚀 Starting e-commerce order processing system...
✅ System started successfully!

📊 Monitoring Dashboard:
   - Order Processing: Enhanced Inbox Store with deduplication
   - Email Notifications: Enhanced Outbox Store with batching
   - Inventory Checks: Enhanced Schedule Store with execution tracking

🔄 Processing sample orders...
📦 Created inbox entry for order ORD-2025-001
💰 Order ORD-2025-001: $1299.99 -> $1104.99 (discount: 15.0%)
🔄 Duplicate order detected and rejected: ORD-2025-001
📧 Queued Welcome email for customer-alice@example.com
📅 Scheduled inventory check for product laptop-pro-15 at 2025-01-21T14:45:00Z

📈 Performance metrics and alerts are being collected...
Press 'q' to quit, 'm' for metrics, 'h' for health status
```

### Interactive Commands

While the application is running, you can use these keyboard commands:

- **`m`** - Show current performance metrics
- **`h`** - Show system health status
- **`q`** - Quit the application

## Configuration

The sample uses performance profiles to demonstrate different enhanced store configurations:

### Production Profile (Default)
```csharp
services.AddEnhancedInboxStore(options =>
{
    options.EnableAdvancedDeduplication = true;
    options.EnableContentBasedDeduplication = true;
    options.DeduplicationCacheSize = 50000;
    options.ContentDeduplicationWindow = TimeSpan.FromMinutes(30);
});
```

### Available Profiles
- **Development** - Enhanced debugging capabilities
- **Production** - Balanced performance and reliability
- **HighPerformance** - Maximum throughput optimizations
- **Reliability** - Maximum fault tolerance

## Sample Workload

The application generates a realistic e-commerce workload:

### Orders
- 12 sample orders (including 2 duplicates for deduplication testing)
- 5 different customers
- 5 different products with varying prices
- Random quantities and order dates

### Customers & Products
```csharp
var customers = ["alice@example.com", "bob@example.com", ...];
var products = [
    ("laptop-pro-15", "Laptop Pro 15\"", $1299.99),
    ("wireless-mouse", "Wireless Gaming Mouse", $79.99),
    // ...
];
```

### Business Logic
- **Discount calculation** based on order total
- **Inventory checks** scheduled at random intervals
- **Email notifications** (welcome + promotional)
- **Performance tracking** across all operations

## Observability Features

### Metrics Dashboard
```
📊 Current Performance Metrics:
   Orders Processed: 10
   Duplicates Detected: 2
   Emails Queued: 10
   Inventory Checks Scheduled: 5
   Average Processing Time: 125.4ms
   Cache Hit Rate: 85.2%
```

### Health Monitoring
```
🏥 System Health Status:
   Overall: Healthy
   Description: Inbox: OK (12.3ms); Outbox: OK (8.7ms); Schedule: OK (15.1ms)
```

### OpenTelemetry Integration
- **Distributed tracing** across all enhanced store operations
- **Activity tags** for correlation (order.id, customer.id, product.id)
- **Structured metrics** exported to console (configurable for production exporters)
- **Performance counters** for cache hit rates and processing times

## Enhanced Store Configuration

### Inbox Store (Order Processing)
```csharp
services.AddEnhancedInboxStore(options =>
{
    options.EnableAdvancedDeduplication = true;
    options.EnableContentBasedDeduplication = true;
    options.DeduplicationCacheSize = 50000;
    options.ContentDeduplicationWindow = TimeSpan.FromMinutes(30);
    options.MaxConcurrentOperations = 200;
});
```

### Outbox Store (Email Notifications)
```csharp
services.AddEnhancedOutboxStore(options =>
{
    options.EnableBatchStaging = true;
    options.EnableExponentialBackoff = true;
    options.StagingBatchSize = 50;
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(2);
    options.MaxRetryDelay = TimeSpan.FromMinutes(10);
});
```

### Schedule Store (Inventory Management)
```csharp
services.AddEnhancedScheduleStore(options =>
{
    options.EnableDuplicateDetection = true;
    options.EnableExecutionTimeIndexing = true;
    options.EnableBatchOperations = true;
    options.ScheduleCacheSize = 25000;
    options.BatchSize = 100;
    options.DuplicateDetectionWindow = TimeSpan.FromMinutes(15);
});
```

## Background Services

The sample includes four background services that demonstrate continuous processing:

1. **OrderProcessorHostedService** - Processes pending inbox entries
2. **NotificationProcessorHostedService** - Sends staged outbox messages
3. **InventoryCheckHostedService** - Executes scheduled inventory checks
4. **MetricsReportingService** - Collects and reports performance metrics

## Key Requirements Demonstrated

- **R9.51**: Content-based deduplication using SHA256 hashing
- **R8.21**: Enhanced store observability with OpenTelemetry integration
- **R7.12**: High-performance optimizations with hot-path improvements

## Production Considerations

For production use, consider:

1. **Replace in-memory stores** with persistent implementations (SQL Server, PostgreSQL, etc.)
2. **Configure telemetry exporters** for your monitoring system (Prometheus, CloudWatch, etc.)
3. **Adjust performance profiles** based on your throughput requirements
4. **Set up alerting** on key metrics (duplicate rates, processing times, failure rates)
5. **Implement proper security** for message payloads and metadata
6. **Configure appropriate cache sizes** based on available memory
7. **Set up health check endpoints** for load balancer integration

## See Also

- [Basic Usage Example](../EnhancedStores/BasicUsageExample.cs) - Simple enhanced store usage
- [High Performance Example](../EnhancedStores/HighPerformanceExample.cs) - Performance-optimized configuration
- [Observability Example](../EnhancedStores/ObservabilityExample.cs) - Advanced monitoring setup


