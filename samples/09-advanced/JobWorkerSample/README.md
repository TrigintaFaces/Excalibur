# Job Worker Service Sample

This sample demonstrates how to configure a .NET Worker Service to run jobs using the Excalibur Jobs framework with Quartz.NET scheduling.

## Features Demonstrated

### 1. Basic Job Hosting
- Setting up an Excalibur Job Host with minimal configuration
- Automatic job discovery and registration
- Quartz.NET integration with dependency injection

### 2. Individual Job Configuration
- Fluent API for configuring specific jobs
- Different scheduling patterns:
  - **Cron expressions** (`DataCleanupJob` - daily at 2 AM)
  - **Recurring intervals** (`HealthCheckJob` - every 5 minutes)
  - **One-time execution** (`StartupJob` - runs immediately)
  - **Delayed execution** (`WelcomeJob` - runs 30 seconds after start)

### 3. Job Context and Parameters
- Jobs with context data (`EmailJob` with `EmailConfiguration`)
- Serialization and deserialization of job parameters
- Type-safe job configuration

### 4. Conditional Job Registration
- Environment-specific jobs (`DevelopmentJob` - only in Development)
- Configuration-based job enabling/disabling

### 5. Multiple Job Instances
- Same job type with different schedules (`ReportJob`)
- Daily, weekly, and monthly report generation
- Instance-specific configuration

### 6. Advanced Features
- **Job Persistence**: Track job execution history (optional)
- **Job Coordination**: Distributed job coordination with Redis (optional)
- **Workflow Support**: Orchestrate complex job workflows (optional)

## Sample Jobs

### DataCleanupJob
Performs daily maintenance tasks like cleaning temporary files, old logs, and expired cache entries.
- **Schedule**: Daily at 2:00 AM (`0 2 * * *`)
- **Type**: Scheduled maintenance

### HealthCheckJob
Monitors system health by checking database, external APIs, file system, and memory usage.
- **Schedule**: Every 5 minutes
- **Type**: System monitoring

### StartupJob
Performs one-time initialization tasks when the application starts.
- **Schedule**: Immediate execution
- **Type**: Application initialization

### WelcomeJob
Displays a welcome message and application status after startup.
- **Schedule**: 30 seconds after application start
- **Type**: Informational

### EmailJob
Sends emails using configuration provided as context data.
- **Schedule**: Weekly on Mondays at 9:00 AM (`0 9 * * MON`)
- **Type**: Communication with context

### DevelopmentJob (Development only)
Runs development-specific tasks like clearing caches and generating test data.
- **Schedule**: Every minute (Development environment only)
- **Type**: Development utilities

### ReportJob
Generates different types of reports (daily, weekly, monthly) based on the schedule.
- **Daily Report**: Every day at 8:00 AM (`0 8 * * *`)
- **Weekly Report**: Mondays at 8:00 AM (`0 8 * * MON`)
- **Monthly Report**: First day of month at 8:00 AM (`0 8 1 * *`)

## Configuration

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ExcaliburJobsSample;Trusted_Connection=true;",
    "Redis": "localhost:6379"
  }
}
```

- **DefaultConnection**: Used for job persistence (optional)
- **Redis**: Used for distributed job coordination (optional)

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` to enable development-specific jobs

## Running the Sample

### Prerequisites
- .NET 9.0 SDK
- SQL Server LocalDB (optional, for job persistence)
- Redis (optional, for job coordination)

### Basic Run
```bash
dotnet run
```

### With Job Persistence
1. Ensure SQL Server LocalDB is available
2. Update the `DefaultConnection` in `appsettings.Development.json`
3. Run the application - it will automatically create the required tables

### With Distributed Coordination
1. Start a Redis server on `localhost:6379`
2. Update the `Redis` connection string if needed
3. Run the application

## Code Examples

### Basic Job Host Setup
```csharp
// Simple setup - just assemblies
builder.Services.AddExcaliburJobHost(typeof(Program).Assembly);
```

### Job Host with Quartz Configuration
```csharp
// With Quartz configuration
builder.Services.AddExcaliburJobHost(
    configureQuartz: q =>
    {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
    },
    typeof(Program).Assembly);
```

### Job Host with Job Configuration
```csharp
// With job configuration using the unified API
builder.Services.AddExcaliburJobHost(
    configureJobs: jobs =>
    {
        // Cron-based job
        jobs.AddJob<DataCleanupJob>("0 2 * * *", "daily-cleanup");

        // Interval-based job
        jobs.AddRecurringJob<HealthCheckJob>(TimeSpan.FromMinutes(5));

        // One-time job
        jobs.AddOneTimeJob<StartupJob>();

        // Job with context
        var config = new EmailConfiguration { SmtpServer = "smtp.example.com" };
        jobs.AddJob<EmailJob, EmailConfiguration>("0 9 * * MON", config);
    },
    typeof(Program).Assembly);
```

### Full Configuration
```csharp
// Complete setup with both Quartz and job configuration
builder.Services.AddExcaliburJobHost(
    configureQuartz: q =>
    {
        q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
    },
    configureJobs: jobs =>
    {
        jobs.AddJob<DataCleanupJob>("0 2 * * *", "daily-cleanup");
    },
    typeof(Program).Assembly);
```

## Monitoring

The sample includes comprehensive logging using Serilog. All job executions, successes, and failures are logged with appropriate detail levels.

### Log Levels
- **Information**: Job start/completion, important status updates
- **Debug**: Detailed execution steps (Development environment)
- **Warning**: Recoverable issues, health check failures
- **Error**: Job failures, unhandled exceptions

## Extending the Sample

### Adding New Jobs
1. Create a new class implementing `IBackgroundJob` or `IBackgroundJob<TContext>`
2. Add job registration in `Program.cs`
3. Configure the desired schedule

### Custom Job Types
- **IBackgroundJob**: Simple jobs without context
- **IBackgroundJob<TContext>**: Jobs requiring configuration/context data
- **IWorkflow<TInput, TOutput>**: Complex workflow orchestration

### Additional Features
- Add health checks for job monitoring
- Implement custom job persistence providers
- Create custom coordination strategies
- Add metrics and telemetry

## Architecture

The sample demonstrates proper separation of concerns:
- **Program.cs**: Application configuration and job registration
- **Jobs/**: Individual job implementations
- **Configuration**: Strongly-typed configuration classes
- **Logging**: Structured logging with Serilog

This architecture supports easy testing, maintenance, and extension of the job system.