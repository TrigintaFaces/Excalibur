// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Hosting.AzureFunctions;

/// <summary>
/// Azure Functions serverless host provider implementation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureFunctionsHostProvider" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
internal partial class AzureFunctionsHostProvider(ILogger<AzureFunctionsHostProvider> logger)
	: IServerlessHostProvider, IServerlessHostConfigurator
{
	private readonly ILogger<AzureFunctionsHostProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public ServerlessPlatform Platform => ServerlessPlatform.AzureFunctions;

	/// <inheritdoc />
	public bool IsAvailable =>
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	/// <inheritdoc />
	public void ConfigureServices(IServiceCollection services, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		LogConfiguringServices();

		// Register a default IServerlessContext resolution that throws NotSupportedException.
		// Azure Functions contexts are per-invocation and must be created via CreateContext
		// from the runtime-provided FunctionContext — they cannot be resolved from the root
		// container. We register a factory that throws to give a clear error if a consumer
		// accidentally attempts direct resolution.
		services.TryAddSingleton<IServerlessContext>(static _ =>
			throw new NotSupportedException(
				"IServerlessContext for Azure Functions must be created per-invocation via " +
				"IServerlessHostProvider.CreateContext(FunctionContext). It cannot be resolved " +
				"from the root service provider."));

		if (options.EnableColdStartOptimization)
		{
			services.TryAddSingleton<IColdStartOptimizer, AzureFunctionsColdStartOptimizer>();
		}

		LogServicesConfigured();
	}

	/// <inheritdoc />
	public void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);
		ArgumentNullException.ThrowIfNull(options);

		LogConfiguringHost();

		_ = hostBuilder.ConfigureServices((_, services) => ConfigureServices(services, options));

		// Bind option-supplied environment variables into in-memory IConfiguration
		// (avoids polluting the process environment).
		if (options.EnvironmentVariables.Count > 0)
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

		// Accept any concrete FunctionContext the Azure Functions Worker runtime provides.
		// In Worker v2 the runtime passes FunctionContextProxy (a test/runtime subclass of
		// the abstract FunctionContext). Use a pattern-match against the abstract base so
		// any derived context is accepted.
		if (platformContext is FunctionContext functionContext)
		{
			return new AzureFunctionsServerlessContext(functionContext, _logger);
		}

		throw new ArgumentException(
			$"Platform context must be a FunctionContext for Azure Functions provider. Received: {platformContext.GetType().Name}",
			nameof(platformContext));
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

		// Extract context headers (CorrelationId, TenantId) from Properties if populated
		ServerlessContextHeaders.ExtractAndSet(context, key =>
			context.Properties.TryGetValue(key, out var value) ? value?.ToString() : null);

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
			LogExecutionTimeout();
			throw new TimeoutException(ErrorConstants.FunctionExecutionTimedOut);
		}
		catch (Exception ex)
		{
			LogHandlerFailed(ex);
			throw;
		}
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(AzureFunctionsEventId.ConfiguringServices, LogLevel.Debug,
		"Configuring services for Azure Functions")]
	private partial void LogConfiguringServices();

	[LoggerMessage(AzureFunctionsEventId.ServicesConfigured, LogLevel.Debug,
		"Azure Functions services configured successfully")]
	private partial void LogServicesConfigured();

	[LoggerMessage(AzureFunctionsEventId.ConfiguringHost, LogLevel.Debug,
		"Configuring host for Azure Functions")]
	private partial void LogConfiguringHost();

	[LoggerMessage(AzureFunctionsEventId.HostConfigured, LogLevel.Debug,
		"Azure Functions host configured successfully")]
	private partial void LogHostConfigured();

	[LoggerMessage(AzureFunctionsEventId.ExecutingHandler, LogLevel.Debug,
		"Executing Azure Functions handler with input type {InputType}")]
	private partial void LogExecutingHandler(string inputType);

	[LoggerMessage(AzureFunctionsEventId.HandlerExecuted, LogLevel.Debug,
		"Azure Functions handler executed successfully")]
	private partial void LogHandlerExecuted();

	[LoggerMessage(AzureFunctionsEventId.ExecutionCancelled, LogLevel.Warning,
		"Azure Functions execution was cancelled by external token")]
	private partial void LogExecutionCancelled();

	[LoggerMessage(AzureFunctionsEventId.ExecutionTimedOut, LogLevel.Error,
		"Azure Functions execution timed out")]
	private partial void LogExecutionTimeout();

	[LoggerMessage(AzureFunctionsEventId.HandlerFailed, LogLevel.Error,
		"Azure Functions handler execution failed")]
	private partial void LogHandlerFailed(Exception ex);
}
