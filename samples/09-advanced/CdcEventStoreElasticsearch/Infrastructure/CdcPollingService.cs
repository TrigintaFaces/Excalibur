// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CdcEventStoreElasticsearch.Infrastructure;

/// <summary>
/// Configuration options for CDC polling.
/// </summary>
public sealed class CdcPollingOptions
{
	/// <summary>
	/// Configuration section name.
	/// </summary>
	public const string SectionName = "CdcPolling";

	/// <summary>Gets or sets the polling interval.</summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>Gets or sets the maximum batch size per poll.</summary>
	public int BatchSize { get; set; } = 100;

	/// <summary>Gets or sets whether to start polling immediately on startup.</summary>
	public bool StartImmediately { get; set; } = true;

	/// <summary>Gets or sets the initial delay before first poll.</summary>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);

	/// <summary>Gets or sets the CDC source connection string.</summary>
	public string? CdcSourceConnectionString { get; set; }

	/// <summary>Gets or sets the CDC state store connection string.</summary>
	public string? StateStoreConnectionString { get; set; }
}

/// <summary>
/// Background service that uses the framework's <see cref="IDataChangeEventProcessor"/> for
/// production-grade CDC processing with automatic recovery from SQL Server interruptions.
/// </summary>
/// <remarks>
/// <para>
/// This service demonstrates production-like CDC processing with:
/// </para>
/// <list type="bullet">
/// <item>Uses <see cref="IDataChangeEventProcessorFactory"/> to create processors</item>
/// <item>Automatic LSN checkpoint tracking via framework's state store</item>
/// <item>Stale position recovery for backup restores, CDC cleanup jobs</item>
/// <item>Configurable recovery strategies (FallbackToEarliest, FallbackToLatest, etc.)</item>
/// <item>Retry logic with exponential backoff</item>
/// <item>Graceful shutdown via application lifetime integration</item>
/// </list>
/// </remarks>
public sealed class CdcPollingBackgroundService : BackgroundService
{
	private readonly IDataChangeEventProcessorFactory _processorFactory;
	private readonly IDatabaseConfig _dbConfig;
	private readonly CdcPollingOptions _options;
	private readonly ILogger<CdcPollingBackgroundService> _logger;

	private IDataChangeEventProcessor? _processor;

	/// <summary>
	/// Initializes a new instance of the <see cref="CdcPollingBackgroundService"/> class.
	/// </summary>
	public CdcPollingBackgroundService(
		IDataChangeEventProcessorFactory processorFactory,
		IDatabaseConfig dbConfig,
		IOptions<CdcPollingOptions> options,
		ILogger<CdcPollingBackgroundService> logger)
	{
		_processorFactory = processorFactory;
		_dbConfig = dbConfig;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("CDC Polling Service stopping...");

		// Dispose current processor if running
		if (_processor is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (_processor is IDisposable disposable)
		{
			disposable.Dispose();
		}

		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation(
			"CDC Polling Service starting. Interval: {Interval}, BatchSize: {BatchSize}, " +
			"CaptureInstances: [{CaptureInstances}]",
			_options.PollingInterval,
			_options.BatchSize,
			string.Join(", ", _dbConfig.CaptureInstances));

		if (_options.InitialDelay > TimeSpan.Zero)
		{
			_logger.LogDebug("Waiting {Delay} before first poll", _options.InitialDelay);
			await Task.Delay(_options.InitialDelay, stoppingToken).ConfigureAwait(false);
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				// Create processor with real SQL connections
				await using var cdcConnection = new SqlConnection(_options.CdcSourceConnectionString);
				await using var stateConnection = new SqlConnection(_options.StateStoreConnectionString);

				await cdcConnection.OpenAsync(stoppingToken).ConfigureAwait(false);
				await stateConnection.OpenAsync(stoppingToken).ConfigureAwait(false);

				_processor = _processorFactory.Create(_dbConfig, cdcConnection, stateConnection);

				// Process CDC changes using the high-level processor
				// This handles:
				// - LSN tracking and checkpointing
				// - Stale position detection and recovery
				// - Handler resolution via DI
				// - Ordered event processing
				var processedCount = await _processor.ProcessCdcChangesAsync(stoppingToken)
					.ConfigureAwait(false);

				if (processedCount > 0)
				{
					_logger.LogInformation("Processed {Count} CDC changes", processedCount);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (CdcStalePositionException ex)
			{
				// Stale position detected - this can happen after:
				// - Database backup restore
				// - CDC cleanup job purged records
				// - CDC disabled/re-enabled
				// The framework's recovery options handle this automatically based on config
				_logger.LogWarning(
					ex,
					"CDC stale position detected. The recovery strategy configured in " +
					"CdcRecoveryOptions will be applied automatically.");
			}
			catch (CdcMissingTableHandlerException ex)
			{
				// No handler registered for a table
				_logger.LogWarning(
					ex,
					"No handler registered for CDC table. Configure StopOnMissingTableHandler=false " +
					"to skip unhandled tables.");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during CDC polling cycle. Retrying after interval.");
			}
			finally
			{
				// Dispose the processor to release connections
				if (_processor is IAsyncDisposable asyncDisposable)
				{
					await asyncDisposable.DisposeAsync().ConfigureAwait(false);
				}
				else if (_processor is IDisposable disposable)
				{
					disposable.Dispose();
				}

				_processor = null;
			}

			await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
		}

		_logger.LogInformation("CDC Polling Service stopped");
	}
}

/// <summary>
/// Sample database configuration for CDC processing with recovery options.
/// </summary>
public sealed class SampleCdcDatabaseConfig : IDatabaseConfig
{
	/// <inheritdoc/>
	public required string DatabaseName { get; init; }

	/// <inheritdoc/>
	public required string DatabaseConnectionIdentifier { get; init; }

	/// <inheritdoc/>
	public required string StateConnectionIdentifier { get; init; }

	/// <inheritdoc/>
	public bool StopOnMissingTableHandler { get; init; }

	/// <inheritdoc/>
	public string[] CaptureInstances { get; init; } = [];

	/// <inheritdoc/>
	public int BatchTimeInterval { get; init; } = 5000;

	/// <inheritdoc/>
	public int QueueSize { get; init; } = 1000;

	/// <inheritdoc/>
	public int ProducerBatchSize { get; init; } = 100;

	/// <inheritdoc/>
	public int ConsumerBatchSize { get; init; } = 10;

	/// <inheritdoc/>
	public CdcRecoveryOptions? RecoveryOptions { get; init; }

	/// <summary>
	/// Creates a configuration with production-recommended recovery settings.
	/// </summary>
	public static SampleCdcDatabaseConfig CreateWithRecovery(
		string databaseName,
		string[] captureInstances,
		StalePositionRecoveryStrategy recoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest,
		CdcPositionResetHandler? onPositionReset = null)
	{
		return new SampleCdcDatabaseConfig
		{
			DatabaseName = databaseName,
			DatabaseConnectionIdentifier = $"cdc-{databaseName}",
			StateConnectionIdentifier = $"state-{databaseName}",
			CaptureInstances = captureInstances,
			StopOnMissingTableHandler = false, // Production: skip unknown tables
			RecoveryOptions = new CdcRecoveryOptions
			{
				RecoveryStrategy = recoveryStrategy,
				MaxRecoveryAttempts = 3,
				RecoveryAttemptDelay = TimeSpan.FromSeconds(5),
				EnableStructuredLogging = true,
				OnPositionReset = onPositionReset
			}
		};
	}
}
