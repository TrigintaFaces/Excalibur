# Excalibur.Jobs

Job scheduling and execution for the Excalibur framework using Quartz.NET.

## Quick Start

Get started in 5 minutes with the unified `AddExcaliburJobHost` API.

### 1. Create a new project

```bash
dotnet new worker -n MyJobWorker
cd MyJobWorker
dotnet add package Excalibur.Jobs
```

### 2. Add minimal Program.cs

```csharp
using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Single call sets up everything: base services, Quartz scheduling, health checks
builder.Services.AddExcaliburJobHost(
    configureJobs: jobs =>
    {
        jobs.AddRecurringJob<HelloWorldJob>(TimeSpan.FromMinutes(1), "hello-job");
    },
    typeof(Program).Assembly);

var host = builder.Build();
host.Run();

public class HelloWorldJob : IBackgroundJob
{
    private readonly ILogger<HelloWorldJob> _logger;

    public HelloWorldJob(ILogger<HelloWorldJob> logger) => _logger = logger;

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Hello from Excalibur Job at {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
```

### 3. Run

```bash
dotnet run
```

See `examples/MinimalJobSample` for the complete working sample.

## Features

- **Unified registration**: Single `AddExcaliburJobHost()` call configures all services
- **Quartz.NET integration**: Full scheduling with cron expressions, triggers, and persistence
- **Fluent job API**: `AddRecurringJob`, `AddOneTimeJob`, `AddJobIf` for conditional jobs
- **Health checks**: Built-in monitoring for job execution
- **Dependency injection**: Jobs resolve services from the DI container

## Advanced Configuration

```csharp
builder.Services.AddExcaliburJobHost(
    configureQuartz: q =>
    {
        // Configure persistence, clustering, thread pools
        q.UsePersistentStore(store => { /* ... */ });
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
    },
    configureJobs: jobs =>
    {
        jobs.AddRecurringJob<MyJob>(TimeSpan.FromHours(1), "hourly-job");
        jobs.AddOneTimeJob<StartupJob>("startup-init");
    },
    typeof(Program).Assembly);
```

## Installation

```bash
dotnet add package Excalibur.Jobs
```

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
