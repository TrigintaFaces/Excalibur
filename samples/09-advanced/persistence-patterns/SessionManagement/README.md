# SessionManagement

Demonstrates AWS session management with SQS FIFO queues for ordered message processing.

## Purpose

This sample shows how to implement session-based message processing with AWS SQS FIFO queues. Sessions ensure messages for the same entity (e.g., order) are processed in order and by a single consumer.

## What This Sample Demonstrates

- **Session Affinity** - Processing related messages together
- **SQS FIFO Integration** - Using AWS SQS FIFO with message groups
- **Session Locking** - Preventing concurrent processing of the same session
- **Auto-Renewal** - Extending session locks during long operations
- **Ordered Processing** - Maintaining message order within sessions

## Prerequisites

- AWS account with SQS access
- SQS FIFO queue configured
- AWS credentials in environment or config

## Configuration

Configure AWS credentials and SQS settings:

```json
{
  "AWS": {
    "Region": "us-east-1"
  }
}
```

Or use environment variables:
```bash
export AWS_ACCESS_KEY_ID=your-key
export AWS_SECRET_ACCESS_KEY=your-secret
export AWS_REGION=us-east-1
```

## Running the Sample

```bash
dotnet run --project samples/09-advanced/SessionManagement
```

## Key Concepts

### Session Management

Sessions group related messages for ordered processing:

```csharp
// SQS FIFO Message Groups = Sessions
// Messages with the same MessageGroupId are processed in order
```

### Session Options

```csharp
services.Configure<SessionOptions>(options =>
{
    options.SessionTimeout = TimeSpan.FromMinutes(5);    // Session expiry
    options.MaxConcurrentSessions = 100;                 // Parallel sessions
    options.EnableAutoRenewal = true;                    // Auto-extend locks
    options.LockTimeout = TimeSpan.FromMinutes(2);       // Lock duration
});
```

### Session Workflow

```csharp
// 1. Acquire session for message group
var session = await sessionManager.AcquireSessionAsync(messageGroupId);

// 2. Process messages for this session
await ProcessOrderEvents(session, messages);

// 3. Release session when done
await sessionManager.ReleaseSessionAsync(session.SessionId);
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    SQS FIFO Queue                           │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │Group: A │ │Group: A │ │Group: B │ │Group: A │           │
│  │ Msg 1   │ │ Msg 2   │ │ Msg 1   │ │ Msg 3   │           │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘           │
└─────────────────────────┬───────────────────────────────────┘
                          │
            ┌─────────────▼─────────────┐
            │     Session Manager       │
            │  ┌─────────┬─────────┐    │
            │  │Session A│Session B│    │
            │  │ Msg 1   │ Msg 1   │    │
            │  │ Msg 2   │         │    │
            │  │ Msg 3   │         │    │
            │  └─────────┴─────────┘    │
            └─────────────┬─────────────┘
                          │
         ┌────────────────┼────────────────┐
         │                │                │
    ┌────▼────┐      ┌────▼────┐      ┌────▼────┐
    │Consumer │      │Consumer │      │Consumer │
    │   1     │      │   2     │      │   3     │
    └─────────┘      └─────────┘      └─────────┘
```

## Use Cases

- **Order Processing** - Process all events for an order in sequence
- **Account Updates** - Serialize balance changes per account
- **Workflow Steps** - Ensure step ordering per workflow instance
- **Entity Synchronization** - Keep entity operations atomic

## Project Structure

```
SessionManagement/
├── SessionManagementExample.csproj
├── Program.cs                     # Session management setup
└── README.md                      # This file
```

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Transport.Aws` | AWS SQS/SNS transport |
| `AWSSDK.SQS` | AWS SQS client |

## Next Steps

- [MultiProviderQueueProcessor](../../02-messaging-transports/MultiProviderQueueProcessor/) - Multi-provider setup
- [SagaOrchestration](../../04-reliability/SagaOrchestration/) - Distributed transactions

---

*Category: Advanced | Sprint 428*
