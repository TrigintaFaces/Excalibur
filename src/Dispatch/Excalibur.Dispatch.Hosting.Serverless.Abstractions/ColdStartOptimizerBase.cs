// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Shared base class for cold start optimizers across serverless platforms.
/// Extracts common singleton warmup, JIT compilation, and lifecycle patterns.
/// </summary>
/// <remarks>
/// <para>Subclasses override <see cref="IsEnabled"/> and <see cref="WarmupPlatformSdkAsync"/>
/// to provide platform-specific behavior (AWS SDK, Azure SDK, GCP SDK).</para>
/// </remarks>
public abstract partial class ColdStartOptimizerBase : IColdStartOptimizer
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ColdStartOptimizerBase"/> class.
	/// </summary>
	/// <param name="serviceProvider">The service provider for DI container access.</param>
	/// <param name="logger">The logger instance.</param>
	protected ColdStartOptimizerBase(IServiceProvider serviceProvider, ILogger logger)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public abstract bool IsEnabled { get; }

	/// <inheritdoc />
	public async Task OptimizeAsync()
	{
		if (!IsEnabled)
		{
			LogOptimizationDisabled(PlatformName);
			return;
		}

		LogOptimizationStarting(PlatformName);
		await WarmupAsync().ConfigureAwait(false);
		LogOptimizationCompleted(PlatformName);
	}

	/// <inheritdoc />
	public async Task WarmupAsync()
	{
		if (!IsEnabled)
		{
			return;
		}

		LogWarmingUpServices(PlatformName);

		// 1. DI Container Pre-initialization
		WarmupSingletonServices();

		// 2. Platform-specific SDK warmup
		await WarmupPlatformSdkAsync().ConfigureAwait(false);

		// 3. JIT compilation warmup
		WarmupJitCompilation();

		LogServicesWarmedUp(PlatformName);
	}

	/// <summary>
	/// Gets the platform display name for logging (e.g. "AWS Lambda", "Azure Functions").
	/// </summary>
	protected abstract string PlatformName { get; }

	/// <summary>
	/// Warms up platform-specific SDK clients and environment.
	/// Override in subclasses to pre-warm AWS, Azure, or GCP SDK clients.
	/// </summary>
	/// <returns>A task representing the async warmup operation.</returns>
	protected abstract Task WarmupPlatformSdkAsync();

	/// <summary>
	/// Pre-initializes singleton services from the DI container.
	/// </summary>
	private void WarmupSingletonServices()
	{
		LogSingletonWarmupStarting();

		try
		{
			var scopeFactory = _serviceProvider.GetService<IServiceScopeFactory>();

			if (scopeFactory is not null)
			{
				using var scope = scopeFactory.CreateScope();
				_ = scope.ServiceProvider.GetService<ILoggerFactory>();
			}
			else
			{
				_ = _serviceProvider.GetService<ILoggerFactory>();
			}

			LogSingletonWarmupCompleted();
		}
		catch (Exception ex)
		{
			LogSingletonWarmupFailed(ex);
		}
	}

	/// <summary>
	/// Triggers JIT compilation for critical code paths.
	/// </summary>
	private void WarmupJitCompilation()
	{
		LogJitWarmupStarting();

		try
		{
			_ = DateTimeOffset.UtcNow.ToString("O");
			_ = Guid.NewGuid().ToString();
			const string warmupPayload = "{\"warmup\":true}";
			_ = warmupPayload.Length;

			LogJitWarmupCompleted();
		}
		catch (Exception ex)
		{
			LogJitWarmupFailed(ex);
		}
	}

	// Source-generated logging methods for shared cold start optimizer
	[LoggerMessage(ServerlessEventId.ColdStartOptimizationEnabled, LogLevel.Debug,
		"{PlatformName} cold start optimization is disabled")]
	private partial void LogOptimizationDisabled(string platformName);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 10, LogLevel.Debug,
		"Starting {PlatformName} cold start optimization")]
	private partial void LogOptimizationStarting(string platformName);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 11, LogLevel.Debug,
		"{PlatformName} cold start optimization completed")]
	private partial void LogOptimizationCompleted(string platformName);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 12, LogLevel.Debug,
		"Warming up {PlatformName} services")]
	private partial void LogWarmingUpServices(string platformName);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 13, LogLevel.Debug,
		"{PlatformName} services warmed up")]
	private partial void LogServicesWarmedUp(string platformName);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 14, LogLevel.Debug,
		"Starting DI singleton service warmup")]
	private partial void LogSingletonWarmupStarting();

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 15, LogLevel.Debug,
		"DI singleton service warmup completed")]
	private partial void LogSingletonWarmupCompleted();

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 16, LogLevel.Warning,
		"DI singleton service warmup failed")]
	private partial void LogSingletonWarmupFailed(Exception ex);

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 17, LogLevel.Debug,
		"Starting JIT compilation warmup")]
	private partial void LogJitWarmupStarting();

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 18, LogLevel.Debug,
		"JIT compilation warmup completed")]
	private partial void LogJitWarmupCompleted();

	[LoggerMessage(ServerlessEventId.ColdStartOptimizationCompleted + 19, LogLevel.Warning,
		"JIT compilation warmup failed")]
	private partial void LogJitWarmupFailed(Exception ex);
}
