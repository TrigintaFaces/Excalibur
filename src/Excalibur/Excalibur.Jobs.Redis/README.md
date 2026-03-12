# Excalibur.Jobs.Redis

Redis-based distributed job coordination for the Excalibur Jobs framework.

## Features

- Distributed job locking via Redis
- Job instance registry with heartbeat monitoring
- Job distribution across available instances
- Leadership token management

## Usage

```csharp
services.AddJobCoordinationRedis("localhost:6379");
```
