// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Hosting.AwsLambda;

/// <summary>
/// AWS Lambda serverless host provider implementation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsLambdaHostProvider" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
internal partial class AwsLambdaHostProvider(ILogger<AwsLambdaHostProvider> logger) : IServerlessHostProvider, IServerlessHostConfigurator
{
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public ServerlessPlatform Platform => ServerlessPlatform.AwsLambda;

	/// <inheritdoc />
	public bool IsAvailable =>

		// Check for AWS Lambda environment variables
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")) ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT"));

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT",
		"IL2026:RequiresUnreferencedCode",
		Justification = "DefaultLambdaJsonSerializer is registered as a fallback. " +
			"Consumers targeting AOT should register SourceGeneratorLambdaJsonSerializer instead.")]
	public void ConfigureServices(IServiceCollection services, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		LogConfiguringServicesForAwsLambda();

		// Register AWS Lambda specific services
		_ = services.AddSingleton(sp => CreateDefaultContext());

		// Add Lambda serializer if not already configured
		services.TryAddSingleton<DefaultLambdaJsonSerializer>();

		// Configure cold start optimization if enabled
		if (options.EnableColdStartOptimization)
		{
			_ = services.AddSingleton<IColdStartOptimizer, AwsLambdaColdStartOptimizer>();
		}

		// Log warnings when consumers enable telemetry options that require
		// platform SDK integration not yet wired up. This prevents silent no-ops
		// where consumers think EnableDistributedTracing=true does something.
		if (options.Telemetry.EnableDistributedTracing)
		{
			LogConfiguringAwsXRayTracingForLambda();
		}

		if (options.Telemetry.EnableMetrics)
		{
			LogConfiguringAwsLambdaMetrics();
		}

		LogAwsLambdaServicesConfiguredSuccessfully();
	}

	/// <inheritdoc />
	public void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);
		ArgumentNullException.ThrowIfNull(options);

		LogConfiguringHostForAwsLambda();

		// Configure Lambda-specific hosting
		_ = hostBuilder.ConfigureServices((_, services) => ConfigureServices(services, options));

		// Inject environment variables as in-memory IConfiguration instead of polluting the process environment
		if (options.EnvironmentVariables.Any())
		{
			_ = hostBuilder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(options.EnvironmentVariables!);
			});
		}

		LogAwsLambdaHostConfiguredSuccessfully();
	}

	/// <inheritdoc />
	public IServerlessContext CreateContext(object platformContext)
	{
		ArgumentNullException.ThrowIfNull(platformContext);

		if (platformContext is ILambdaContext lambdaContext)
		{
			return new AwsLambdaServerlessContext(lambdaContext, _logger);
		}

		throw new ArgumentException(
			$"Platform context must be an ILambdaContext for AWS Lambda provider. Received: {platformContext.GetType().Name}",
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

		LogExecutingAwsLambdaHandlerWithInputType(typeof(TInput).Name);

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
				var executionTimeout = remainingTime - TimeSpan.FromMilliseconds(100);
				if (executionTimeout > TimeSpan.Zero)
				{
					timeoutCts.CancelAfter(executionTimeout);
				}
			}

			using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, timeoutCts.Token);

			var result = await handler(input, context, combinedCts.Token).ConfigureAwait(false);

			LogAwsLambdaHandlerExecutedSuccessfully();
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			LogAwsLambdaExecutionCancelledByExternalToken();
			throw;
		}
		catch (OperationCanceledException)
		{
			LogAwsLambdaExecutionTimedOut();
			throw new TimeoutException(ErrorConstants.LambdaExecutionTimedOut);
		}
		catch (Exception ex)
		{
			LogAwsLambdaHandlerExecutionFailed(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(AwsLambdaEventId.ConfiguringServices, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Configuring services for AWS Lambda")]
	private partial void LogConfiguringServicesForAwsLambda();

	[LoggerMessage(AwsLambdaEventId.ServicesConfigured, Microsoft.Extensions.Logging.LogLevel.Debug,
		"AWS Lambda services configured successfully")]
	private partial void LogAwsLambdaServicesConfiguredSuccessfully();

	[LoggerMessage(AwsLambdaEventId.ConfiguringHost, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Configuring host for AWS Lambda")]
	private partial void LogConfiguringHostForAwsLambda();

	[LoggerMessage(AwsLambdaEventId.HostConfigured, Microsoft.Extensions.Logging.LogLevel.Debug,
		"AWS Lambda host configured successfully")]
	private partial void LogAwsLambdaHostConfiguredSuccessfully();

	[LoggerMessage(AwsLambdaEventId.ExecutingHandler, Microsoft.Extensions.Logging.LogLevel.Debug,
		"Executing AWS Lambda handler with input type {InputType}")]
	private partial void LogExecutingAwsLambdaHandlerWithInputType(string inputType);

	[LoggerMessage(AwsLambdaEventId.HandlerExecuted, Microsoft.Extensions.Logging.LogLevel.Debug,
		"AWS Lambda handler executed successfully")]
	private partial void LogAwsLambdaHandlerExecutedSuccessfully();

	[LoggerMessage(AwsLambdaEventId.ExecutionCancelled, Microsoft.Extensions.Logging.LogLevel.Warning,
		"AWS Lambda execution was cancelled by external token")]
	private partial void LogAwsLambdaExecutionCancelledByExternalToken();

	[LoggerMessage(AwsLambdaEventId.ExecutionTimedOut, Microsoft.Extensions.Logging.LogLevel.Error,
		"AWS Lambda execution timed out")]
	private partial void LogAwsLambdaExecutionTimedOut();

	[LoggerMessage(AwsLambdaEventId.HandlerFailed, Microsoft.Extensions.Logging.LogLevel.Error,
		"AWS Lambda handler execution failed")]
	private partial void LogAwsLambdaHandlerExecutionFailed(Exception ex);

	[LoggerMessage(AwsLambdaEventId.ConfiguringXRayTracing, Microsoft.Extensions.Logging.LogLevel.Warning,
		"AWS X-Ray tracing integration is not yet implemented. Distributed tracing will not be active.")]
	private partial void LogConfiguringAwsXRayTracingForLambda();

	[LoggerMessage(AwsLambdaEventId.ConfiguringMetrics, Microsoft.Extensions.Logging.LogLevel.Warning,
		"AWS CloudWatch metrics integration is not yet implemented. Metrics will not be collected.")]
	private partial void LogConfiguringAwsLambdaMetrics();

	/// <summary>
	/// Creates a default serverless context for AWS Lambda.
	/// </summary>
	/// <returns> A default AWS Lambda serverless context. </returns>
	private IServerlessContext CreateDefaultContext()
	{
		// Create a minimal context for dependency injection
		var mockLambdaContext = new DefaultLambdaContext();
		return new AwsLambdaServerlessContext(mockLambdaContext, _logger);
	}

	/// <summary>
	/// Default Lambda context for development and DI bootstrapping scenarios.
	/// </summary>
	private sealed class DefaultLambdaContext : ILambdaContext
	{
		public DefaultLambdaContext()
		{
			FunctionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") ?? "DefaultFunction";
			FunctionVersion = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION") ?? "$LATEST";
			var region = Environment.GetEnvironmentVariable("AWS_REGION")
				?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")
				?? "us-east-1";
			var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID") ?? "123456789012";
			InvokedFunctionArn = $"arn:aws:lambda:{region}:{accountId}:function:{FunctionName}";
			MemoryLimitInMB = int.TryParse(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE"), out var memory)
				? memory
				: 128;
			LogGroupName = $"/aws/lambda/{FunctionName}";
			LogStreamName = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}[{Environment.GetEnvironmentVariable("AWS_LAMBDA_LOG_STREAM_NAME") ?? "LOCAL"}]";
		}

		public string RequestId { get; } = Guid.NewGuid().ToString();

		public string AwsRequestId => RequestId;

		public string FunctionName { get; }

		public string FunctionVersion { get; }

		public string InvokedFunctionArn { get; }

		public int MemoryLimitInMB { get; }

		public TimeSpan RemainingTime { get; } = TimeSpan.FromMinutes(15);

		public string LogGroupName { get; }

		public string LogStreamName { get; }

		public ILambdaLogger Logger { get; } = new MockLambdaLogger();

		public ICognitoIdentity? Identity { get; }

		public IClientContext? ClientContext { get; }
	}

	/// <summary>
	/// Mock Lambda logger for development scenarios.
	/// </summary>
	private sealed class MockLambdaLogger : ILambdaLogger
	{
		public void Log(string message)
		{
		}

		public void LogLine(string message)
		{
		}
	}
}
