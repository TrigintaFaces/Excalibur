# Excalibur.Hosting.HealthChecks

Health check and monitoring integration for Excalibur hosting applications.

## Features

- Readiness endpoint at `/.well-known/ready` and liveness endpoint at `/.well-known/live`
- Memory health checks (allocated + working set)
- No ORM or database dependency

## Health check dashboard

This package deliberately ships no dashboard, so that it carries no ORM or database
dependency. To add one, reference `AspNetCore.HealthChecks.UI` and a storage provider
directly in your application and register them alongside the calls below.

## Usage

```csharp
services.AddExcaliburHealthChecks();

app.UseExcaliburHealthChecks();
```
