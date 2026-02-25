# MinimizedWindow Delivery Guarantee

This example demonstrates the **MinimizedWindow** delivery guarantee for smaller failure windows.

## Overview

- **DeliveryGuarantee**: `MinimizedWindow`
- **Behavior**: Each message is marked as sent immediately after publishing
- **Throughput**: ~50% lower than AtLeastOnce (N DB round-trips per batch)
- **Failure Window**: Single message (not entire batch)

## When to Use

Choose MinimizedWindow when:
- **Duplicate impact is significant** (e.g., financial transactions)
- You need **audit logging** with minimal duplicate risk
- You can accept lower throughput for smaller failure windows

## Configuration

```csharp
services.Configure<OutboxOptions>(options =>
{
    options.DeliveryGuarantee = OutboxDeliveryGuarantee.MinimizedWindow;
    options.BatchSize = 50;
    options.PollingInterval = TimeSpan.FromSeconds(5);
});
```

## Trade-off Analysis

| Aspect | AtLeastOnce | MinimizedWindow |
|--------|-------------|-----------------|
| DB Round-Trips | 1 per batch | N per batch |
| Throughput | Highest | ~50% lower |
| Failure Window | Entire batch | Single message |
| Duplicates on Crash | Up to batch size | 1 message max |

## Running the Example

```bash
cd samples/BackgroundServices/MinimizedWindow
dotnet run
```
