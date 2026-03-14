# Excalibur.Hosting.HealthChecks

Health check UI and monitoring integration for Excalibur hosting applications.

## Features

- Readiness and liveness endpoints
- Health check dashboard UI
- Memory health checks (allocated + working set)

## Usage

```csharp
services.AddExcaliburHealthChecks();

app.UseExcaliburHealthChecks();
```
