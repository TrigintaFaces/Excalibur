// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#if AZURE_FUNCTIONS_SUPPORT
using Microsoft.Azure.Functions.Worker;
#endif

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Hosting.AzureFunctions;

/// <summary>
/// Azure Functions serverless host provider implementation.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureFunctionsHostProvider" /> class. </remarks>
/// <param name="logger"> The logger instance. </param>
public partial class AzureFunctionsHostProvider(ILogger logger) : IServerlessHostProvider, IServerlessHostConfigurator
{
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	// Source-generated logging methods (Sprint 368 - EventId migration)
	[LoggerMessage(AzureFunctionsEventId.ConfiguringServices, LogLevel.Debug,
		"Configuring services for Azure Functions")]
	private partial void LogConfiguringServices();

	[LoggerMessage(AzureFunctionsEventId.ServicesConfigured, LogLevel.Debug,
		"Azure Functions services configured successfully")]
	private partial void LogServicesConfigured();

	[LoggerMessage(AzureFunctionsEventId.SupportNotAvailable, LogLevel.Warning,
		"Azure Functions support is not available. Ensure Microsoft.Azure.Functions.Worker package is installed.")]
	private partial void LogAzFuncNotAvailable();

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

	/// <inheritdoc />
	public ServerlessPlatform Platform => ServerlessPlatform.AzureFunctions;

	/// <inheritdoc />
	public bool IsAvailable
	{
		get
		{
#if AZURE_FUNCTIONS_SUPPORT
			// Check for Azure Functions environment variables
			return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")) ||
				   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) ||
				   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME"));
#else
			return false;
#endif
		}
	}

	/// <inheritdoc />
	public void ConfigureServices(IServiceCollection services, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

#if AZURE_FUNCTIONS_SUPPORT
		LogConfiguringServices();

		// Register Azure Functions specific services
		services.AddSingleton(sp => CreateDefaultContext());

		// Configure cold start optimization if enabled
		if (options.EnableColdStartOptimization)
		{
			services.AddSingleton<IColdStartOptimizer, AzureFunctionsColdStartOptimizer>();
		}

		// Configure Application Insights if enabled
		if (options.EnableDistributedTracing)
		{
			ConfigureApplicationInsights(services, options);
		}

		// Configure metrics if enabled
		if (options.EnableMetrics)
		{
			ConfigureAzureMetrics(services, options);
		}

		// Configure Durable Functions if enabled
		if (options.AzureFunctions.EnableDurableFunctions)
		{
			ConfigureDurableFunctions(services, options);
		}

		LogServicesConfigured();
#else
		LogAzFuncNotAvailable();
#endif
	}

	/// <inheritdoc />
	public void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);
		ArgumentNullException.ThrowIfNull(options);

#if AZURE_FUNCTIONS_SUPPORT
		LogConfiguringHost();

		// Configure Azure Functions hosting
		hostBuilder.ConfigureServices(services =>
		{
			ConfigureServices(services, options);
		});

		// Inject environment variables as in-memory IConfiguration instead of polluting the process environment
		if (options.EnvironmentVariables.Any())
		{
			hostBuilder.ConfigureAppConfiguration((_, config) =>
			{
				config.AddInMemoryCollection(options.EnvironmentVariables!);
			});
		}

		LogHostConfigured();
#endif
	}

	/// <inheritdoc />
	public IServerlessContext CreateContext(object platformContext)
	{
		ArgumentNullException.ThrowIfNull(platformContext);

#if AZURE_FUNCTIONS_SUPPORT
		if (platformContext is FunctionContext functionContext)
		{
			return new AzureFunctionsServerlessContext(functionContext, _logger);
		}
#endif

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

#if AZURE_FUNCTIONS_SUPPORT
	/// <summary>
	/// Configures Application Insights for distributed tracing.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> The host options. </param>
	private void ConfigureApplicationInsights(IServiceCollection services, ServerlessHostOptions options)
	{
		LogConfiguringAppInsights();

		// Add Application Insights telemetry services.AddApplicationInsightsTelemetry();
	}

	/// <summary>
	/// Configures Azure-specific metrics collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> The host options. </param>
	private void ConfigureAzureMetrics(IServiceCollection services, ServerlessHostOptions options)
	{
		LogConfiguringMetrics();

		// Add Azure Monitor metrics integration services.AddAzureMonitorMetrics();
	}

	/// <summary>
	/// Configures Durable Functions support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> The host options. </param>
	private void ConfigureDurableFunctions(IServiceCollection services, ServerlessHostOptions options)
	{
		LogConfiguringDurableFunctions();

		// Add Durable Functions support services.AddDurableFunctions();
	}

	/// <summary>
	/// Creates a default serverless context for Azure Functions.
	/// </summary>
	/// <returns> A default Azure Functions serverless context. </returns>
	private IServerlessContext CreateDefaultContext()
	{
		// Create a minimal context for dependency injection
		var mockFunctionContext = new DefaultFunctionContext();
		return new AzureFunctionsServerlessContext(mockFunctionContext, _logger);
	}

	/// <summary>
	/// Default Function context for development and DI bootstrapping scenarios.
	/// </summary>
	private sealed class DefaultFunctionContext : FunctionContext
	{
		private readonly Dictionary<object, object> _items = new();

		public override string FunctionId { get; } = Guid.NewGuid().ToString();
		public override string InvocationId { get; } = Guid.NewGuid().ToString();
		public string FunctionName => Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "DefaultFunction";
		public override Microsoft.Azure.Functions.Worker.TraceContext TraceContext { get; } = new DefaultTraceContext();
		public override BindingContext BindingContext { get; } = new DefaultBindingContext();
		public override RetryContext RetryContext { get; } = new DefaultRetryContext();
		public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
		public override FunctionDefinition FunctionDefinition { get; } = CreateDefaultFunctionDefinition();
		public override IDictionary<object, object> Items { get => _items; set => throw new NotSupportedException(); }
		public override IInvocationFeatures Features { get; } = new DefaultInvocationFeatures();

		private static FunctionDefinition CreateDefaultFunctionDefinition() =>
			// FunctionDefinition is an abstract class with no public constructor.
			// A real Azure Functions host creates these internally. In a default/bootstrap
			// context, callers should not depend on FunctionDefinition.
			throw new NotSupportedException(
				"FunctionDefinition is not available in the default bootstrap context. " +
				"Use a real FunctionContext from the Azure Functions runtime instead of DefaultFunctionContext.");
	}

	private sealed class DefaultTraceContext : Microsoft.Azure.Functions.Worker.TraceContext
	{
		public override string TraceParent { get; } = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString()[..16]}-01";
		public override string? TraceState { get; }
	}

	private sealed class DefaultBindingContext : BindingContext
	{
		public override IReadOnlyDictionary<string, object?> BindingData { get; } = new Dictionary<string, object?>();
	}

	private sealed class DefaultRetryContext : RetryContext
	{
		public override int RetryCount { get; }
		public override int MaxRetryCount { get; } = 3;
	}

	private sealed class DefaultInvocationFeatures : IInvocationFeatures, IEnumerable<KeyValuePair<Type, object>>
	{
		private readonly Dictionary<Type, object> _features = new();

		public TFeature Get<TFeature>() => (TFeature)_features.GetValueOrDefault(typeof(TFeature), default(TFeature)!);
		public void Set<TFeature>(TFeature feature) => _features[typeof(TFeature)] = feature!;

		public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}

	// Source-generated logging methods for Azure-specific configuration (Sprint 368 - EventId migration)
	[LoggerMessage(AzureFunctionsEventId.ConfiguringAppInsights, LogLevel.Warning,
		"Application Insights integration is not yet implemented. Distributed tracing will not be active.")]
	private partial void LogConfiguringAppInsights();

	[LoggerMessage(AzureFunctionsEventId.ConfiguringMetrics, LogLevel.Warning,
		"Azure Monitor metrics integration is not yet implemented. Metrics will not be collected.")]
	private partial void LogConfiguringMetrics();

	[LoggerMessage(AzureFunctionsEventId.ConfiguringDurableFunctions, LogLevel.Warning,
		"Durable Functions integration is not yet implemented. Durable orchestrations will not be available.")]
	private partial void LogConfiguringDurableFunctions();
#endif
}
