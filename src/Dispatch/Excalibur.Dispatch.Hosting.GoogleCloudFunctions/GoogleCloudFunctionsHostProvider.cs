// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.GoogleCloudFunctions;

namespace Excalibur.Dispatch.Hosting.GoogleCloud;

/// <summary>
/// Provides Google Cloud Functions specific serverless hosting implementation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="GoogleCloudFunctionsHostProvider" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public sealed partial class GoogleCloudFunctionsHostProvider(ILogger<GoogleCloudFunctionsHostProvider> logger) : IServerlessHostProvider, IServerlessHostConfigurator
{
	private readonly ILogger<GoogleCloudFunctionsHostProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public ServerlessPlatform Platform => ServerlessPlatform.GoogleCloudFunctions;

	/// <inheritdoc />
	public bool IsAvailable =>

		// Check for Google Cloud Functions environment variables
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_NAME")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_REGION")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE"));

	/// <inheritdoc />
	public void ConfigureServices(IServiceCollection services, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		LogServicesConfiguring();

		// Register Google Cloud Functions specific services
		_ = services.AddSingleton(sp => CreateDefaultContext());

		// Configure cold start optimization if enabled
		if (options.EnableColdStartOptimization)
		{
			_ = services.AddSingleton<IColdStartOptimizer, GoogleCloudFunctionsColdStartOptimizer>();
		}

		// Configure distributed tracing if enabled
		if (options.EnableDistributedTracing)
		{
			ConfigureGoogleCloudTracing(services, options);
		}

		// Configure metrics if enabled
		if (options.EnableMetrics)
		{
			ConfigureGoogleCloudMetrics(services, options);
		}

		LogServicesConfigured();
	}

	/// <inheritdoc />
	public void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);
		ArgumentNullException.ThrowIfNull(options);

		LogHostConfiguring();

		// Configure Google Cloud Functions hosting
		_ = hostBuilder.ConfigureServices(services => ConfigureServices(services, options));

		// Inject environment variables as in-memory IConfiguration instead of polluting the process environment
		if (options.EnvironmentVariables.Any())
		{
			_ = hostBuilder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(options.EnvironmentVariables!);
			});
		}

		LogHostConfigured();
	}

	/// <inheritdoc />
	public IServerlessContext CreateContext(object platformContext)
	{
		ArgumentNullException.ThrowIfNull(platformContext);

		// For Google Cloud Functions, we create a context from the environment since GCF doesn't provide a strongly-typed context object
		// like Azure Functions
		return new GoogleCloudFunctionsServerlessContext(platformContext, _logger);
	}

	/// <inheritdoc />
	public async Task<TOutput> ExecuteAsync<TInput, TOutput>(
		TInput input,
		IServerlessContext context,
		Func<TInput, IServerlessContext, CancellationToken, Task<TOutput>> handler,
		CancellationToken cancellationToken)
		where TInput : class
		where TOutput : class
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(handler);

		LogExecutingHandler(typeof(TInput).Name);

		try
		{
			// Create a timeout cancellation token based on remaining execution time
			using var timeoutCts = new CancellationTokenSource();
			var remainingTime = context.RemainingTime;

			if (remainingTime > TimeSpan.Zero)
			{
				// Reserve some time for cleanup
				var executionTimeout = remainingTime - TimeSpan.FromMilliseconds(500);
				if (executionTimeout > TimeSpan.Zero)
				{
					timeoutCts.CancelAfter(executionTimeout);
				}
			}

			using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutCts.Token);

			var result = await handler(input, context, combinedCts.Token).ConfigureAwait(false);

			LogHandlerExecuted();
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			LogExecutionCancelled();
			throw;
		}
		catch (OperationCanceledException)
		{
			LogExecutionTimedOut();
			throw new TimeoutException(ErrorConstants.FunctionExecutionTimedOut);
		}
		catch (Exception ex)
		{
			LogHandlerFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	private IServerlessContext CreateDefaultContext() =>

		// Create a context from environment variables for Google Cloud Functions
		new GoogleCloudFunctionsServerlessContext(new { }, _logger);

	private void ConfigureGoogleCloudTracing(IServiceCollection services, ServerlessHostOptions options)
	{
		_ = services; // Reserved for future Google Cloud Trace service registration
		_ = options; // Reserved for future trace configuration options

		// Configure Google Cloud Trace integration
		LogTraceConfiguring();
	}

	private void ConfigureGoogleCloudMetrics(IServiceCollection services, ServerlessHostOptions options)
	{
		_ = services; // Reserved for future Google Cloud Monitoring service registration
		_ = options; // Reserved for future monitoring configuration options

		// Configure Google Cloud Monitoring integration
		LogMetricsConfiguring();
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(GoogleCloudFunctionsEventId.ConfiguringServices, LogLevel.Debug, "Configuring services for Google Cloud Functions")]
	private partial void LogServicesConfiguring();

	[LoggerMessage(GoogleCloudFunctionsEventId.ServicesConfigured, LogLevel.Debug, "Google Cloud Functions services configured successfully")]
	private partial void LogServicesConfigured();

	[LoggerMessage(GoogleCloudFunctionsEventId.ConfiguringHost, LogLevel.Debug, "Configuring host for Google Cloud Functions")]
	private partial void LogHostConfiguring();

	[LoggerMessage(GoogleCloudFunctionsEventId.HostConfigured, LogLevel.Debug, "Google Cloud Functions host configured successfully")]
	private partial void LogHostConfigured();

	[LoggerMessage(GoogleCloudFunctionsEventId.ExecutingHandler, LogLevel.Debug, "Executing Google Cloud Functions handler with input type {InputType}")]
	private partial void LogExecutingHandler(string inputType);

	[LoggerMessage(GoogleCloudFunctionsEventId.HandlerExecuted, LogLevel.Debug, "Google Cloud Functions handler executed successfully")]
	private partial void LogHandlerExecuted();

	[LoggerMessage(GoogleCloudFunctionsEventId.ExecutionCancelled, LogLevel.Warning, "Google Cloud Functions execution was cancelled by external token")]
	private partial void LogExecutionCancelled();

	[LoggerMessage(GoogleCloudFunctionsEventId.ExecutionTimedOut, LogLevel.Error, "Google Cloud Functions execution timed out")]
	private partial void LogExecutionTimedOut();

	[LoggerMessage(GoogleCloudFunctionsEventId.HandlerFailed, LogLevel.Error, "Google Cloud Functions handler execution failed")]
	private partial void LogHandlerFailed(Exception ex);

	[LoggerMessage(GoogleCloudFunctionsEventId.ConfiguringCloudTrace, LogLevel.Warning, "Google Cloud Trace integration is not yet implemented. Distributed tracing will not be active.")]
	private partial void LogTraceConfiguring();

	[LoggerMessage(GoogleCloudFunctionsEventId.ConfiguringCloudMonitoring, LogLevel.Warning, "Google Cloud Monitoring integration is not yet implemented. Metrics will not be collected.")]
	private partial void LogMetricsConfiguring();
}
