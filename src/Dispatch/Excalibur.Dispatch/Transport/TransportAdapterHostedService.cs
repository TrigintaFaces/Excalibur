// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Hosted service that manages the lifecycle of transport adapters.
/// </summary>
/// <remarks>
/// <para>
/// This service ensures transport adapters are properly started when the application starts
/// and gracefully stopped when the application shuts down. Adapters are started in registration
/// order and stopped in reverse order to ensure proper dependency handling.
/// </para>
/// <para>
/// During shutdown, the service allows a configurable drain period for pending messages
/// before forcefully stopping adapters.
/// </para>
/// <para>
/// Factory-registered transports are initialized using the service provider during startup,
/// before starting any adapters.
/// </para>
/// <para>
/// In addition to automatic lifecycle management via <see cref="IHostedService"/>, this class
/// implements <see cref="ITransportLifecycleManager"/> to provide runtime control over
/// individual transports for scenarios like graceful degradation, dynamic scaling, and testing.
/// </para>
/// </remarks>
public sealed partial class TransportAdapterHostedService : ITransportLifecycleManager, IHostedService
{
	private readonly TransportRegistry _transportRegistry;
	private readonly TransportAdapterHostedServiceOptions _options;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<TransportAdapterHostedService> _logger;
	private readonly List<string> _startedAdapters = [];

	[LoggerMessage(LogLevel.Debug, "Initializing {FactoryCount} factory-registered transport(s)")]
	private partial void LogInitializingFactories(int factoryCount);

	[LoggerMessage(LogLevel.Information, "Initialized {InitializedCount} factory-registered transport(s)")]
	private partial void LogInitializedFactories(int initializedCount);

	[LoggerMessage(LogLevel.Debug, "No transport adapters registered, skipping startup")]
	private partial void LogNoTransportAdaptersRegistered();

	[LoggerMessage(LogLevel.Information, "Starting {TransportCount} transport adapter(s)")]
	private partial void LogStartingTransportAdapters(int transportCount);

	[LoggerMessage(LogLevel.Debug, "Starting transport adapter '{TransportName}' ({TransportType})")]
	private partial void LogStartingTransportAdapter(string transportName, string transportType);

	[LoggerMessage(LogLevel.Information, "Transport adapter '{TransportName}' started successfully")]
	private partial void LogTransportAdapterStarted(string transportName);

	[LoggerMessage(LogLevel.Warning, "Startup cancelled while starting transport adapter '{TransportName}'")]
	private partial void LogStartupCancelled(string transportName);

	[LoggerMessage(LogLevel.Error, "Failed to start transport adapter '{TransportName}': {ErrorMessage}")]
	private partial void LogTransportAdapterStartFailed(
		string transportName,
		string errorMessage,
		Exception exception);

	[LoggerMessage(LogLevel.Information, "Successfully started {StartedCount}/{TotalCount} transport adapter(s)")]
	private partial void LogTransportAdapterStartupSummary(int startedCount, int totalCount);

	[LoggerMessage(LogLevel.Debug, "No transport adapters to stop")]
	private partial void LogNoTransportAdaptersToStop();

	[LoggerMessage(LogLevel.Information,
		"Stopping {TransportCount} transport adapter(s) with {DrainTimeout}s drain timeout")]
	private partial void LogStoppingTransportAdapters(int transportCount, int drainTimeout);

	[LoggerMessage(LogLevel.Information, "All transport adapters stopped")]
	private partial void LogAllTransportAdaptersStopped();

	[LoggerMessage(LogLevel.Warning, "Transport adapter '{TransportName}' not found in registry during shutdown")]
	private partial void LogTransportAdapterNotFound(string transportName);

	[LoggerMessage(LogLevel.Debug, "Stopping transport adapter '{TransportName}'")]
	private partial void LogStoppingTransportAdapter(string transportName);

	[LoggerMessage(LogLevel.Information, "Transport adapter '{TransportName}' stopped successfully")]
	private partial void LogTransportAdapterStopped(string transportName);

	[LoggerMessage(LogLevel.Warning,
		"Drain timeout exceeded while stopping transport adapter '{TransportName}', forcing shutdown")]
	private partial void LogTransportAdapterDrainTimeoutExceeded(string transportName);

	[LoggerMessage(LogLevel.Error, "Error stopping transport adapter '{TransportName}': {ErrorMessage}")]
	private partial void LogTransportAdapterStopFailed(
		string transportName,
		string errorMessage,
		Exception exception);

	[LoggerMessage(LogLevel.Debug, "Transport adapter '{TransportName}' is already running")]
	private partial void LogTransportAdapterAlreadyRunning(string transportName);

	[LoggerMessage(LogLevel.Information, "Starting transport adapter '{TransportName}' ({TransportType})")]
	private partial void LogStartingTransportAdapterViaLifecycleManager(string transportName, string transportType);

	[LoggerMessage(LogLevel.Information,
		"Transport adapter '{TransportName}' started successfully via lifecycle manager")]
	private partial void LogTransportAdapterStartedViaLifecycleManager(string transportName);

	[LoggerMessage(LogLevel.Debug, "Transport adapter '{TransportName}' is already stopped")]
	private partial void LogTransportAdapterAlreadyStopped(string transportName);

	[LoggerMessage(LogLevel.Information, "Stopping transport adapter '{TransportName}' via lifecycle manager")]
	private partial void LogStoppingTransportAdapterViaLifecycleManager(string transportName);

	[LoggerMessage(LogLevel.Information,
		"Transport adapter '{TransportName}' stopped successfully via lifecycle manager")]
	private partial void LogTransportAdapterStoppedViaLifecycleManager(string transportName);
#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="TransportAdapterHostedService"/> class.
	/// </summary>
	/// <param name="transportRegistry"> The transport registry containing registered adapters. </param>
	/// <param name="options"> The hosted service options. </param>
	/// <param name="serviceProvider"> The service provider for factory-based transport creation. </param>
	/// <param name="logger"> The logger. </param>
	public TransportAdapterHostedService(
		TransportRegistry transportRegistry,
		IOptions<TransportAdapterHostedServiceOptions> options,
		IServiceProvider serviceProvider,
		ILogger<TransportAdapterHostedService> logger)
	{
		_transportRegistry = transportRegistry ?? throw new ArgumentNullException(nameof(transportRegistry));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		// Initialize any factory-registered transports first
		if (_transportRegistry.HasPendingFactories)
		{
			LogInitializingFactories(_transportRegistry.GetPendingFactoryNames().Count());

			var initialized = _transportRegistry.InitializeFactories(_serviceProvider);

			LogInitializedFactories(initialized);
		}

		var transports = _transportRegistry.GetAllTransports();

		if (transports.Count == 0)
		{
			LogNoTransportAdaptersRegistered();
			return;
		}

		LogStartingTransportAdapters(transports.Count);

		foreach (var (name, registration) in transports)
		{
			try
			{
				LogStartingTransportAdapter(name, registration.TransportType);

				await registration.Adapter.StartAsync(cancellationToken).ConfigureAwait(false);
				_startedAdapters.Add(name);

				LogTransportAdapterStarted(name);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogStartupCancelled(name);
				throw;
			}
			catch (Exception ex)
			{
				LogTransportAdapterStartFailed(name, ex.Message, ex);

				if (_options.ThrowOnStartupFailure)
				{
					// Stop any adapters that were already started
					await StopStartedAdaptersAsync(CancellationToken.None).ConfigureAwait(false);
					throw;
				}
			}
		}

		LogTransportAdapterStartupSummary(_startedAdapters.Count, transports.Count);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_startedAdapters.Count == 0)
		{
			LogNoTransportAdaptersToStop();
			return;
		}

		LogStoppingTransportAdapters(_startedAdapters.Count, _options.DrainTimeoutSeconds);

		// Create a combined token that includes the drain timeout
		using var drainCts = new CancellationTokenSource(_options.DrainTimeout);
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			drainCts.Token);

		await StopStartedAdaptersAsync(combinedCts.Token).ConfigureAwait(false);

		LogAllTransportAdaptersStopped();
	}

	private async Task StopStartedAdaptersAsync(CancellationToken cancellationToken)
	{
		// Stop adapters in reverse order of startup
		for (var i = _startedAdapters.Count - 1; i >= 0; i--)
		{
			var name = _startedAdapters[i];
			var registration = _transportRegistry.GetTransportRegistration(name);

			if (registration is null)
			{
				LogTransportAdapterNotFound(name);
				continue;
			}

			try
			{
				LogStoppingTransportAdapter(name);

				await registration.Adapter.StopAsync(cancellationToken).ConfigureAwait(false);

				LogTransportAdapterStopped(name);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				LogTransportAdapterDrainTimeoutExceeded(name);
			}
			catch (Exception ex)
			{
				LogTransportAdapterStopFailed(name, ex.Message, ex);
			}
		}

		_startedAdapters.Clear();
	}

	#region ITransportLifecycleManager Implementation

	/// <inheritdoc/>
	public IReadOnlyCollection<ITransportAdapter> RegisteredTransports
	{
		get
		{
			lock (_lock)
			{
				return _transportRegistry.GetAllTransports()
					.Select(static kvp => kvp.Value.Adapter)
					.ToList()
					.AsReadOnly();
			}
		}
	}

	/// <inheritdoc/>
	public IReadOnlyCollection<string> TransportNames
	{
		get
		{
			lock (_lock)
			{
				return _transportRegistry.GetTransportNames().ToList().AsReadOnly();
			}
		}
	}

	/// <inheritdoc/>
	public async Task StartTransportAsync(string name, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var registration = _transportRegistry.GetTransportRegistration(name)
						   ?? throw new InvalidOperationException(
							   $"No transport with name '{name}' is registered. " +
							   $"Available transports: {string.Join(", ", _transportRegistry.GetTransportNames())}");

		// Check if already running
		if (registration.Adapter.IsRunning)
		{
			LogTransportAdapterAlreadyRunning(name);
			return;
		}

		LogStartingTransportAdapterViaLifecycleManager(name, registration.TransportType);

		await registration.Adapter.StartAsync(cancellationToken).ConfigureAwait(false);

		lock (_lock)
		{
			if (!_startedAdapters.Contains(name))
			{
				_startedAdapters.Add(name);
			}
		}

		LogTransportAdapterStartedViaLifecycleManager(name);
	}

	/// <inheritdoc/>
	public async Task StopTransportAsync(string name, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var registration = _transportRegistry.GetTransportRegistration(name)
						   ?? throw new InvalidOperationException(
							   $"No transport with name '{name}' is registered. " +
							   $"Available transports: {string.Join(", ", _transportRegistry.GetTransportNames())}");

		// Check if already stopped
		if (!registration.Adapter.IsRunning)
		{
			LogTransportAdapterAlreadyStopped(name);
			return;
		}

		LogStoppingTransportAdapterViaLifecycleManager(name);

		// Apply drain timeout
		using var drainCts = new CancellationTokenSource(_options.DrainTimeout);
		using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			drainCts.Token);

		try
		{
			await registration.Adapter.StopAsync(combinedCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (drainCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			LogTransportAdapterDrainTimeoutExceeded(name);
		}

		lock (_lock)
		{
			_ = _startedAdapters.Remove(name);
		}

		LogTransportAdapterStoppedViaLifecycleManager(name);
	}

	/// <inheritdoc/>
	public ITransportAdapter? GetTransport(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _transportRegistry.GetTransportAdapter(name);
	}

	/// <inheritdoc/>
	public bool IsRegistered(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		return _transportRegistry.GetTransportRegistration(name) is not null;
	}

	/// <inheritdoc/>
	public bool IsRunning(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var registration = _transportRegistry.GetTransportRegistration(name);
		return registration?.Adapter.IsRunning ?? false;
	}

	#endregion
}

/// <summary>
/// Configuration options for the transport adapter hosted service.
/// </summary>
public sealed class TransportAdapterHostedServiceOptions
{
	/// <summary>
	/// The default drain timeout in seconds.
	/// </summary>
	public const int DefaultDrainTimeoutSeconds = 30;

	/// <summary>
	/// Gets or sets the timeout in seconds to wait for pending messages to drain during shutdown.
	/// </summary>
	/// <value> The drain timeout in seconds. Default is 30 seconds. </value>
	/// <remarks>
	/// <para>
	/// During graceful shutdown, the service will wait up to this duration for transport adapters
	/// to complete processing any in-flight messages before forcing a stop.
	/// </para>
	/// </remarks>
	public int DrainTimeoutSeconds { get; set; } = DefaultDrainTimeoutSeconds;

	/// <summary>
	/// Gets the drain timeout as a <see cref="TimeSpan"/>.
	/// </summary>
	/// <value> The drain timeout duration. </value>
	public TimeSpan DrainTimeout => TimeSpan.FromSeconds(DrainTimeoutSeconds);

	/// <summary>
	/// Gets or sets a value indicating whether to throw an exception if a transport adapter
	/// fails to start.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to throw on startup failure and prevent the application from starting;
	/// <see langword="false"/> to log the error and continue with remaining adapters.
	/// Default is <see langword="true"/>.
	/// </value>
	/// <remarks>
	/// <para>
	/// In production environments, it is recommended to keep this as <see langword="true"/>
	/// to ensure all required transports are operational before accepting traffic.
	/// </para>
	/// </remarks>
	public bool ThrowOnStartupFailure { get; set; } = true;
}
