# MinimalJobSample

Get started with Excalibur Job Host in 5 minutes.

## Purpose

This sample demonstrates the simplest possible setup for background job processing using the Excalibur Job Host. It shows how to create recurring jobs with minimal configuration.

## What This Sample Demonstrates

- **Job Host Setup** - Single-call configuration with `AddExcaliburJobHost`
- **Recurring Jobs** - Jobs that run on a schedule
- **Job Implementation** - Creating jobs with `IBackgroundJob`
- **Dependency Injection** - Jobs support constructor injection

## Running the Sample

```bash
dotnet run --project samples/09-advanced/MinimalJobSample
```

## Sample Output

```
info: HelloWorldJob[0]
      Hello from Excalibur Job at 2026-01-21T14:32:00+00:00
info: HelloWorldJob[0]
      Hello from Excalibur Job at 2026-01-21T14:33:00+00:00
```

## Project Structure

```
MinimalJobSample/
├── MinimalJobSample.csproj    # Project file
├── Program.cs                 # Host setup and job definition
└── README.md                  # This file
```

## The Entire Application

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Single call sets up everything:
// - Base services (data, application, domain layers)
// - Quartz.NET scheduling with dependency injection
// - Health checks for job monitoring
builder.Services.AddExcaliburJobHost(
    configureJobs: jobs =>
    {
        // Add a recurring job that runs every minute
        jobs.AddRecurringJob<HelloWorldJob>(TimeSpan.FromMinutes(1), "hello-job");
    },
    typeof(Program).Assembly);

var host = builder.Build();
host.Run();

// Simple job implementation
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

## Key Features

### AddExcaliburJobHost

This single call configures:
- Base Excalibur services
- Quartz.NET scheduler with DI
- Health checks (`/health/jobs`)
- Job registration

### Job Types

| Method | Schedule |
|--------|----------|
| `AddRecurringJob<T>(interval)` | Fixed interval |
| `AddCronJob<T>(cronExpression)` | Cron schedule |
| `AddOneTimeJob<T>(delay)` | Single execution |

### Cron Example

```csharp
jobs.AddCronJob<DailyReportJob>("0 0 6 * * ?", "daily-report");  // Every day at 6 AM
```

## Dependencies

- `Excalibur.Hosting.Jobs` - Job host and scheduling
- `Excalibur.Jobs.Abstractions` - `IBackgroundJob` interface

## Next Steps

- [JobWorkerSample](../JobWorkerSample/) - Full-featured job example with multiple job types
- [BackgroundServices](../BackgroundServices/) - Other background processing patterns

---

*Category: Advanced | Sprint 428*
