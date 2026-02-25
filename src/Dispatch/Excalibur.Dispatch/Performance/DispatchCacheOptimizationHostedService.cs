// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Hosted service that automatically freezes Dispatch caches when the application starts.
/// </summary>
/// <remarks>
/// <para>
/// PERF-22: This service provides zero-configuration cache optimization for production deployments.
/// It listens for <see cref="IHostApplicationLifetime.ApplicationStarted"/> and freezes all caches
/// at that point, after the DI container is built and all handlers are registered.
/// </para>
/// <para>
/// The service automatically detects hot reload scenarios via the <c>DOTNET_WATCH</c> and
/// <c>DOTNET_MODIFIABLE_ASSEMBLIES</c> environment variables and skips freezing in those cases
/// to ensure handler discovery continues to work during development.
/// </para>
/// <para>
/// Configuration is controlled via <see cref="PerformanceOptions.AutoFreezeOnStart"/>.
/// </para>
/// </remarks>
public sealed partial class DispatchCacheOptimizationHostedService : IHostedService
{
	private readonly IDispatchCacheManager _cacheManager;
	private readonly IHostApplicationLifetime _applicationLifetime;
	private readonly IOptions<DispatchOptions> _options;
	private readonly ILogger<DispatchCacheOptimizationHostedService> _logger;
	private CancellationTokenRegistration _registration;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchCacheOptimizationHostedService"/> class.
	/// </summary>
	/// <param name="cacheManager">The cache manager to use for freezing.</param>
	/// <param name="applicationLifetime">The application lifetime to listen for startup.</param>
	/// <param name="options">The dispatch options.</param>
	/// <param name="logger">Optional logger for diagnostics.</param>
	public DispatchCacheOptimizationHostedService(
		IDispatchCacheManager cacheManager,
		IHostApplicationLifetime applicationLifetime,
		IOptions<DispatchOptions> options,
		ILogger<DispatchCacheOptimizationHostedService>? logger = null)
	{
		_cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
		_applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger<DispatchCacheOptimizationHostedService>.Instance;
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		// Register callback for ApplicationStarted event
		// Using ApplicationStarted (not ApplicationStarting) ensures:
		// 1. DI container is fully built
		// 2. All handlers have been registered
		// 3. Application is ready to serve requests
		_registration = _applicationLifetime.ApplicationStarted.Register(OnApplicationStarted);

		LogServiceStarted();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _registration.DisposeAsync().ConfigureAwait(false);
	}

	private void OnApplicationStarted()
	{
		try
		{
			if (!_options.Value.CrossCutting.Performance.AutoFreezeOnStart)
			{
				LogAutoFreezeDisabled();
				return;
			}

			if (IsHotReloadEnabled())
			{
				LogHotReloadDetected();
				return;
			}

			_cacheManager.FreezeAll();
		}
		catch (Exception ex)
		{
			// Log but don't throw - cache freezing is an optimization, not a requirement
			// The application should still function correctly without frozen caches
			LogFreezeFailed(ex);
		}
	}

	[LoggerMessage(PerformanceEventId.CacheOptimizationStarted, LogLevel.Debug,
		"DispatchCacheOptimizationHostedService started, waiting for ApplicationStarted")]
	private partial void LogServiceStarted();

	[LoggerMessage(PerformanceEventId.CacheAutoFreezeDisabled, LogLevel.Information,
		"Auto-freeze disabled via configuration (Performance.AutoFreezeOnStart = false)")]
	private partial void LogAutoFreezeDisabled();

	[LoggerMessage(PerformanceEventId.CacheHotReloadDetected, LogLevel.Information,
		"Hot reload detected (DOTNET_WATCH or DOTNET_MODIFIABLE_ASSEMBLIES), skipping cache freeze")]
	private partial void LogHotReloadDetected();

	[LoggerMessage(PerformanceEventId.CacheFreezeFailed, LogLevel.Warning,
		"Failed to freeze Dispatch caches on application startup")]
	private partial void LogFreezeFailed(Exception exception);

	/// <summary>
	/// Detects if hot reload is enabled via environment variables.
	/// </summary>
	/// <returns><see langword="true"/> if hot reload is enabled; otherwise, <see langword="false"/>.</returns>
	private static bool IsHotReloadEnabled()
	{
		// DOTNET_WATCH is set when running via 'dotnet watch'
		var dotnetWatch = Environment.GetEnvironmentVariable("DOTNET_WATCH");
		if (!string.IsNullOrEmpty(dotnetWatch) &&
			(dotnetWatch.Equals("1", StringComparison.OrdinalIgnoreCase) ||
			 dotnetWatch.Equals("true", StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}

		// DOTNET_MODIFIABLE_ASSEMBLIES is set when assemblies can be modified at runtime
		// This is used by hot reload and Edit & Continue features
		var modifiableAssemblies = Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES");
		if (!string.IsNullOrEmpty(modifiableAssemblies) &&
			modifiableAssemblies.Equals("debug", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return false;
	}
}
