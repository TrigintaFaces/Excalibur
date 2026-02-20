// Minimal Excalibur Job Host sample - get started in 5 minutes

using Excalibur.Jobs.Abstractions;

var builder = Host.CreateApplicationBuilder(args);

// Add Excalibur Job Host - this single call sets up everything:
// - Base services (data, application, domain layers)
// - Quartz.NET scheduling with dependency injection
// - Health checks for job monitoring
builder.Services.AddExcaliburJobHost(
	configureJobs: jobs =>
	{
		// Add a recurring job that runs every minute
		_ = jobs.AddRecurringJob<HelloWorldJob>(TimeSpan.FromMinutes(1), "hello-job");
	},
	typeof(Program).Assembly);

var host = builder.Build();
host.Run();

// Simple job that logs a message
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
