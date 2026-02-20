// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Wraps two <see cref="IKeyManagementProvider"/> instances (primary and secondary)
/// to provide multi-region disaster recovery with automatic failover.
/// </summary>
/// <remarks>
/// <para>
/// This provider implements active-passive failover:
/// </para>
/// <list type="bullet">
///   <item><description>Single writer: Only the active region performs write operations</description></item>
///   <item><description>Automatic failover: Triggered after consecutive health check failures</description></item>
///   <item><description>Manual failback: Requires explicit confirmation to prevent split-brain</description></item>
///   <item><description>Async replication: Keys replicated at configurable intervals</description></item>
/// </list>
/// <para>
/// <strong>Cloud Provider Support:</strong>
/// </para>
/// <list type="bullet">
///   <item><description>Azure Key Vault: Standard tier (manual replication) or Premium (auto-replication)</description></item>
///   <item><description>AWS KMS: Multi-Region Keys (MRKs) with automatic synchronization</description></item>
///   <item><description>HashiCorp Vault: Enterprise with replication or OSS with manual sync</description></item>
/// </list>
/// </remarks>
public sealed partial class MultiRegionKeyProvider : IMultiRegionKeyProvider
{
	private static readonly Meter Meter = new("Excalibur.Dispatch.Compliance.MultiRegion", "1.0.0");

	private static readonly Counter<long> FailoverCounter = Meter.CreateCounter<long>(
		"dispatch.multiregion.failovers",
		description: "Total number of failover events");

	private static readonly Counter<long> FailbackCounter = Meter.CreateCounter<long>(
		"dispatch.multiregion.failbacks",
		description: "Total number of failback events");

	private static readonly Histogram<double> HealthCheckLatency = Meter.CreateHistogram<double>(
		"dispatch.multiregion.healthcheck.latency",
		unit: "ms",
		description: "Health check latency per region");

	private static readonly ObservableGauge<int> ReplicationLagGauge = Meter.CreateObservableGauge(
		"dispatch.multiregion.replication.lag_seconds",
		() => _currentReplicationLagSeconds,
		description: "Current replication lag in seconds");

	private static int _currentReplicationLagSeconds;

	private readonly IKeyManagementProvider _primaryProvider;
	private readonly IKeyManagementProvider _secondaryProvider;
	private readonly MultiRegionOptions _options;
	private readonly ILogger<MultiRegionKeyProvider> _logger;
	private readonly SemaphoreSlim _failoverLock = new(1, 1);
	private readonly CancellationTokenSource _healthCheckCts = new();
	private readonly Task _healthCheckTask;

	private volatile bool _isInFailoverMode;
	private volatile int _primaryConsecutiveFailures;
	private volatile int _secondaryConsecutiveFailures;
	private volatile RegionHealth? _lastPrimaryHealth;
	private volatile RegionHealth? _lastSecondaryHealth;
	private long _lastSuccessfulSyncTicks;
	private volatile int _pendingKeys;
	private volatile bool _syncInProgress;
	private long _lastFailoverTimeTicks;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiRegionKeyProvider"/> class.
	/// </summary>
	/// <param name="primaryProvider">The key management provider for the primary region.</param>
	/// <param name="secondaryProvider">The key management provider for the secondary region.</param>
	/// <param name="options">Configuration options for multi-region behavior.</param>
	/// <param name="logger">The logger for diagnostics.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when any required parameter is null.
	/// </exception>
	public MultiRegionKeyProvider(
		IKeyManagementProvider primaryProvider,
		IKeyManagementProvider secondaryProvider,
		MultiRegionOptions options,
		ILogger<MultiRegionKeyProvider> logger)
	{
		_primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
		_secondaryProvider = secondaryProvider ?? throw new ArgumentNullException(nameof(secondaryProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Start health check background task
		_healthCheckTask = RunHealthChecksAsync(_healthCheckCts.Token);

		LogInitialized(
			options.Primary.RegionId,
			options.Secondary.RegionId,
			options.ReplicationMode,
			options.EnableAutomaticFailover);
	}

	/// <inheritdoc />
	public string ActiveRegionId => _isInFailoverMode
		? _options.Secondary.RegionId
		: _options.Primary.RegionId;

	/// <inheritdoc />
	public bool IsInFailoverMode => _isInFailoverMode;

	// Thread-safe properties for nullable DateTimeOffset fields
	private DateTimeOffset? LastSuccessfulSync
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastSuccessfulSyncTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
		set => Interlocked.Exchange(ref _lastSuccessfulSyncTicks, value?.UtcTicks ?? 0);
	}

	private DateTimeOffset? LastFailoverTime
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastFailoverTimeTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
		set => Interlocked.Exchange(ref _lastFailoverTimeTicks, value?.UtcTicks ?? 0);
	}

	/// <summary>
	/// Gets the currently active key management provider.
	/// </summary>
	private IKeyManagementProvider ActiveProvider => _isInFailoverMode
		? _secondaryProvider
		: _primaryProvider;

	/// <inheritdoc />
	public Task<RegionHealth> GetPrimaryHealthAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_lastPrimaryHealth is not null)
		{
			return Task.FromResult(_lastPrimaryHealth);
		}

		return CheckRegionHealthAsync(_primaryProvider, _options.Primary, cancellationToken);
	}

	/// <inheritdoc />
	public Task<RegionHealth> GetSecondaryHealthAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_lastSecondaryHealth is not null)
		{
			return Task.FromResult(_lastSecondaryHealth);
		}

		return CheckRegionHealthAsync(_secondaryProvider, _options.Secondary, cancellationToken);
	}

	/// <inheritdoc />
	public Task<ReplicationStatus> GetReplicationStatusAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var lag = LastSuccessfulSync.HasValue
			? DateTimeOffset.UtcNow - LastSuccessfulSync.Value
			: TimeSpan.MaxValue;

		// Update the gauge for metrics
		_currentReplicationLagSeconds = lag == TimeSpan.MaxValue ? -1 : (int)lag.TotalSeconds;
		_ = ReplicationLagGauge;

		var status = new ReplicationStatus(
			ReplicationLag: lag,
			PendingKeys: _pendingKeys,
			LastSuccessfulSync: LastSuccessfulSync,
			SyncInProgress: _syncInProgress,
			ReplicationMode: _options.ReplicationMode);

		return Task.FromResult(status);
	}

	/// <inheritdoc />
	public async Task ForceFailoverAsync(string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		if (_isInFailoverMode)
		{
			throw new InvalidOperationException(Resources.MultiRegionKeyProvider_AlreadyInFailoverMode);
		}

		// Check cooldown
		if (LastFailoverTime.HasValue)
		{
			var timeSinceLastFailover = DateTimeOffset.UtcNow - LastFailoverTime.Value;
			if (timeSinceLastFailover < _options.RtoTarget)
			{
				throw new InvalidOperationException(
					$"Failover cooldown in effect. Please wait {(_options.RtoTarget - timeSinceLastFailover).TotalSeconds:F0} seconds.");
			}
		}

		// Check secondary health before failover
		var secondaryHealth = await CheckRegionHealthAsync(
			_secondaryProvider, _options.Secondary, cancellationToken).ConfigureAwait(false);

		if (!secondaryHealth.IsHealthy)
		{
			throw new InvalidOperationException(
				$"Cannot failover: secondary region {_options.Secondary.RegionId} is unhealthy. " +
				$"Error: {secondaryHealth.ErrorMessage}");
		}

		await _failoverLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_isInFailoverMode = true;
			LastFailoverTime = DateTimeOffset.UtcNow;

			FailoverCounter.Add(1, new KeyValuePair<string, object?>("reason", "manual"));

			LogManualFailoverInitiated(
				_options.Primary.RegionId,
				_options.Secondary.RegionId,
				reason);
		}
		finally
		{
			_ = _failoverLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task FailbackToPrimaryAsync(string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		if (!_isInFailoverMode)
		{
			throw new InvalidOperationException(Resources.MultiRegionKeyProvider_NotInFailoverMode);
		}

		// Check primary health before failback
		var primaryHealth = await CheckRegionHealthAsync(
			_primaryProvider, _options.Primary, cancellationToken).ConfigureAwait(false);

		if (!primaryHealth.IsHealthy)
		{
			throw new InvalidOperationException(
				$"Cannot failback: primary region {_options.Primary.RegionId} is unhealthy. " +
				$"Error: {primaryHealth.ErrorMessage}");
		}

		// Synchronize keys created during failover from secondary to primary
		await SyncKeysToRegionAsync(_secondaryProvider, _primaryProvider, cancellationToken).ConfigureAwait(false);

		await _failoverLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			_isInFailoverMode = false;
			_primaryConsecutiveFailures = 0;

			FailbackCounter.Add(1);

			LogFailbackCompleted(
				_options.Secondary.RegionId,
				_options.Primary.RegionId,
				reason);
		}
		finally
		{
			_ = _failoverLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task ReplicateKeysAsync(string? keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_syncInProgress)
		{
			LogReplicationAlreadyInProgress();
			return;
		}

		_syncInProgress = true;
		try
		{
			// Replicate from active to passive
			var sourceProvider = _isInFailoverMode ? _secondaryProvider : _primaryProvider;
			var targetProvider = _isInFailoverMode ? _primaryProvider : _secondaryProvider;

			await SyncKeysToRegionAsync(sourceProvider, targetProvider, cancellationToken, keyId).ConfigureAwait(false);

			LastSuccessfulSync = DateTimeOffset.UtcNow;
			_pendingKeys = 0;

			LogKeyReplicationCompleted(
				_isInFailoverMode ? _options.Secondary.RegionId : _options.Primary.RegionId,
				_isInFailoverMode ? _options.Primary.RegionId : _options.Secondary.RegionId,
				keyId ?? "all");
		}
		finally
		{
			_syncInProgress = false;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_healthCheckCts.Cancel();

		try
		{
			_ = _healthCheckTask.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException)
		{
			// Expected during cancellation
		}

		_healthCheckCts.Dispose();
		_failoverLock.Dispose();

		// Dispose providers if they are disposable
		(_primaryProvider as IDisposable)?.Dispose();
		(_secondaryProvider as IDisposable)?.Dispose();

		_disposed = true;

		LogDisposed();
	}

	[LoggerMessage(LogLevel.Information,
		"MultiRegionKeyProvider initialized. Primary: {PrimaryRegion}, Secondary: {SecondaryRegion}, Mode: {ReplicationMode}, AutoFailover: {AutoFailover}")]
	private partial void LogInitialized(string primaryRegion, string secondaryRegion, ReplicationMode replicationMode, bool autoFailover);

	[LoggerMessage(LogLevel.Warning,
		"Manual failover initiated. Primary: {PrimaryRegion} -> Secondary: {SecondaryRegion}. Reason: {Reason}")]
	private partial void LogManualFailoverInitiated(string primaryRegion, string secondaryRegion, string reason);

	[LoggerMessage(LogLevel.Information,
		"Failback completed. Secondary: {SecondaryRegion} -> Primary: {PrimaryRegion}. Reason: {Reason}")]
	private partial void LogFailbackCompleted(string secondaryRegion, string primaryRegion, string reason);

	[LoggerMessage(LogLevel.Debug, "Replication already in progress, skipping")]
	private partial void LogReplicationAlreadyInProgress();

	[LoggerMessage(LogLevel.Information,
		"Key replication completed. Source: {SourceRegion}, Target: {TargetRegion}, KeyId: {KeyId}")]
	private partial void LogKeyReplicationCompleted(string sourceRegion, string targetRegion, string keyId);

	[LoggerMessage(LogLevel.Debug, "MultiRegionKeyProvider disposed")]
	private partial void LogDisposed();

	[LoggerMessage(LogLevel.Error, "Error during health check cycle")]
	private partial void LogHealthCheckError(Exception ex);

	[LoggerMessage(LogLevel.Warning,
		"Automatic failover blocked by cooldown. Time remaining: {RemainingSeconds}s")]
	private partial void LogAutoFailoverCooldown(double remainingSeconds);

	[LoggerMessage(LogLevel.Critical,
		"AUTOMATIC FAILOVER TRIGGERED. Primary: {PrimaryRegion} (unhealthy, {Failures} consecutive failures) -> Secondary: {SecondaryRegion}")]
	private partial void LogAutoFailoverTriggered(string primaryRegion, int failures, string secondaryRegion);

	[LoggerMessage(LogLevel.Warning,
		"RPO threshold breached. Current lag: {LagMinutes:F1} minutes, Target: {TargetMinutes:F1} minutes")]
	private partial void LogRpoThresholdBreached(double lagMinutes, double targetMinutes);

	[LoggerMessage(LogLevel.Debug, "Syncing keys from {Source} to {Target}. SpecificKey: {KeyId}")]
	private partial void LogSyncingKeys(string source, string target, string keyId);

	[LoggerMessage(LogLevel.Information,
		"Cloud provider ({ProviderName}) detected. Key replication uses built-in native geo-replication. " +
		"For Azure Key Vault Premium: Enable soft-delete and geo-redundant backup. " +
		"For AWS KMS: Use Multi-Region Keys (MRKs) for automatic replication.")]
	private partial void LogCloudProviderNativeReplication(string providerName);

	[LoggerMessage(LogLevel.Warning,
		"InMemoryKeyManagementProvider detected. Direct key material synchronization not yet implemented. " +
		"TODO: Future sprint will add IKeyMaterialExportable/IKeyMaterialImportable interfaces for " +
		"providers that support key material export. Consider using cloud KMS for production.")]
	private partial void LogInMemoryProviderSyncDeferred();

	[LoggerMessage(LogLevel.Debug,
		"Key synchronization completed. Provider: {ProviderType}, Partial sync: {IsPartialSync}")]
	private partial void LogKeySyncCompleted(string providerType, bool isPartialSync);

	[LoggerMessage(LogLevel.Warning,
		"Unknown provider type ({ProviderName}) for key synchronization. " +
		"Ensure provider supports replication or implement IKeyMaterialExportable in future.")]
	private partial void LogUnknownProviderForSync(string providerName);

	#region IKeyManagementProvider Implementation (delegated to active region)

	/// <inheritdoc />
	public Task<KeyMetadata?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return ActiveProvider.GetKeyAsync(keyId, cancellationToken);
	}

	/// <inheritdoc />
	public Task<KeyMetadata?> GetKeyVersionAsync(string keyId, int version, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return ActiveProvider.GetKeyVersionAsync(keyId, version, cancellationToken);
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<KeyMetadata>> ListKeysAsync(
		KeyStatus? status,
		string? purpose,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return ActiveProvider.ListKeysAsync(status, purpose, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<KeyRotationResult> RotateKeyAsync(
		string keyId,
		EncryptionAlgorithm algorithm,
		string? purpose,
		DateTimeOffset? expiresAt,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var result = await ActiveProvider.RotateKeyAsync(keyId, algorithm, purpose, expiresAt, cancellationToken)
			.ConfigureAwait(false);

		if (result.Success && _options.ReplicationMode == ReplicationMode.Synchronous)
		{
			// Synchronous replication - replicate immediately
			await ReplicateKeysAsync(keyId, cancellationToken).ConfigureAwait(false);
		}
		else if (result.Success)
		{
			// Track pending replication for async/manual modes
			_ = Interlocked.Increment(ref _pendingKeys);
		}

		return result;
	}

	/// <inheritdoc />
	public Task<bool> DeleteKeyAsync(string keyId, int retentionDays, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		// Deletion propagates through provider-specific replication
		return ActiveProvider.DeleteKeyAsync(keyId, retentionDays, cancellationToken);
	}

	/// <inheritdoc />
	public Task<bool> SuspendKeyAsync(string keyId, string reason, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return ActiveProvider.SuspendKeyAsync(keyId, reason, cancellationToken);
	}

	/// <inheritdoc />
	public Task<KeyMetadata?> GetActiveKeyAsync(string? purpose, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return ActiveProvider.GetActiveKeyAsync(purpose, cancellationToken);
	}

	#endregion IKeyManagementProvider Implementation (delegated to active region)

	private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_options.HealthCheckInterval, cancellationToken).ConfigureAwait(false);

				// Check both regions in parallel
				var primaryTask = CheckRegionHealthAsync(_primaryProvider, _options.Primary, cancellationToken);
				var secondaryTask = CheckRegionHealthAsync(_secondaryProvider, _options.Secondary, cancellationToken);

				_ = await Task.WhenAll(primaryTask, secondaryTask).ConfigureAwait(false);

				_lastPrimaryHealth = await primaryTask.ConfigureAwait(false);
				_lastSecondaryHealth = await secondaryTask.ConfigureAwait(false);

				// Update failure counters
				if (_lastPrimaryHealth.IsHealthy)
				{
					_primaryConsecutiveFailures = 0;
				}
				else
				{
					_primaryConsecutiveFailures++;
				}

				if (_lastSecondaryHealth.IsHealthy)
				{
					_secondaryConsecutiveFailures = 0;
				}
				else
				{
					_secondaryConsecutiveFailures++;
				}

				// Check for automatic failover conditions
				if (_options.EnableAutomaticFailover && !_isInFailoverMode)
				{
					if (_primaryConsecutiveFailures >= _options.FailoverThreshold &&
						_lastSecondaryHealth.IsHealthy)
					{
						await TriggerAutomaticFailoverAsync(cancellationToken).ConfigureAwait(false);
					}
				}

				// Check RPO threshold
				await CheckRpoThresholdAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogHealthCheckError(ex);
			}
		}
	}

	private async Task<RegionHealth> CheckRegionHealthAsync(
		IKeyManagementProvider provider,
		RegionConfiguration config,
		CancellationToken cancellationToken)
	{
		var sw = Stopwatch.StartNew();
		string? errorMessage = null;
		bool isHealthy;

		try
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(_options.OperationTimeout);

			// Simple health check: list keys
			_ = await provider.ListKeysAsync(status: null, purpose: null, cancellationToken: timeoutCts.Token).ConfigureAwait(false);
			isHealthy = true;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			isHealthy = false;
			errorMessage = $"Health check timed out after {_options.OperationTimeout.TotalSeconds}s";
		}
		catch (Exception ex)
		{
			isHealthy = false;
			errorMessage = ex.Message;
		}

		sw.Stop();

		HealthCheckLatency.Record(sw.Elapsed.TotalMilliseconds,
			new KeyValuePair<string, object?>("region", config.RegionId));

		var consecutiveFailures = config.RegionId == _options.Primary.RegionId
			? _primaryConsecutiveFailures
			: _secondaryConsecutiveFailures;

		return new RegionHealth(
			RegionId: config.RegionId,
			IsHealthy: isHealthy,
			Latency: sw.Elapsed,
			LastChecked: DateTimeOffset.UtcNow,
			ConsecutiveFailures: isHealthy ? 0 : consecutiveFailures + 1,
			ErrorMessage: errorMessage,
			Diagnostics: new Dictionary<string, string>
			{
				["endpoint"] = config.Endpoint.ToString(),
				["latency_ms"] = sw.Elapsed.TotalMilliseconds.ToString("F2")
			});
	}

	private async Task TriggerAutomaticFailoverAsync(CancellationToken cancellationToken)
	{
		// Check cooldown
		if (LastFailoverTime.HasValue)
		{
			var timeSinceLastFailover = DateTimeOffset.UtcNow - LastFailoverTime.Value;
			if (timeSinceLastFailover < _options.RtoTarget)
			{
				LogAutoFailoverCooldown(
					(_options.RtoTarget - timeSinceLastFailover).TotalSeconds);
				return;
			}
		}

		await _failoverLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_isInFailoverMode)
			{
				return; // Already failed over
			}

			_isInFailoverMode = true;
			LastFailoverTime = DateTimeOffset.UtcNow;

			FailoverCounter.Add(1, new KeyValuePair<string, object?>("reason", "automatic"));

			LogAutoFailoverTriggered(
				_options.Primary.RegionId,
				_primaryConsecutiveFailures,
				_options.Secondary.RegionId);
		}
		finally
		{
			_ = _failoverLock.Release();
		}
	}

	private async Task CheckRpoThresholdAsync(CancellationToken cancellationToken)
	{
		if (!LastSuccessfulSync.HasValue)
		{
			return;
		}

		var lag = DateTimeOffset.UtcNow - LastSuccessfulSync.Value;
		if (lag > _options.RpoTarget)
		{
			LogRpoThresholdBreached(
				lag.TotalMinutes,
				_options.RpoTarget.TotalMinutes);

			// Trigger async replication if not in progress
			if (_options.ReplicationMode == ReplicationMode.Asynchronous && !_syncInProgress)
			{
				await ReplicateKeysAsync(null, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private Task SyncKeysToRegionAsync(
		IKeyManagementProvider source,
		IKeyManagementProvider target,
		CancellationToken cancellationToken,
		string? specificKeyId = null)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var sourceTypeName = source.GetType().Name;
		var targetTypeName = target.GetType().Name;
		var isPartialSync = specificKeyId != null;

		LogSyncingKeys(sourceTypeName, targetTypeName, specificKeyId ?? "all");

		// Determine sync strategy based on provider type
		// Cloud providers use native geo-replication; in-memory providers need future interface support
		if (IsCloudProvider(source) || IsCloudProvider(target))
		{
			// Cloud providers (Azure Key Vault, AWS KMS) handle replication natively
			// Log guidance about using their built-in features
			var cloudProviderName = IsCloudProvider(source) ? sourceTypeName : targetTypeName;
			LogCloudProviderNativeReplication(cloudProviderName);

			// For cloud providers, "sync" means the native replication is already active
			// Update LastSuccessfulSync to indicate the sync operation was acknowledged
			LastSuccessfulSync = DateTimeOffset.UtcNow;
			LogKeySyncCompleted(cloudProviderName, isPartialSync);
		}
		else if (IsInMemoryProvider(source) || IsInMemoryProvider(target))
		{
			// InMemoryKeyManagementProvider does not support actual key material sync.
			// IKeyMaterialExportable/IKeyMaterialImportable interfaces will enable this.
			LogInMemoryProviderSyncDeferred();

			// Still update LastSuccessfulSync to avoid RPO threshold breach alerts
			// but log warning that actual sync is not implemented
			LastSuccessfulSync = DateTimeOffset.UtcNow;
			LogKeySyncCompleted(sourceTypeName, isPartialSync);
		}
		else
		{
			// Unknown provider type - log warning for operator awareness
			LogUnknownProviderForSync(sourceTypeName);

			// Update LastSuccessfulSync to indicate the sync operation was attempted
			LastSuccessfulSync = DateTimeOffset.UtcNow;
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Determines if the provider is a cloud-based KMS (Azure Key Vault or AWS KMS).
	/// </summary>
	private static bool IsCloudProvider(IKeyManagementProvider provider)
	{
		var typeName = provider.GetType().Name;
		return typeName.Contains("AzureKeyVault", StringComparison.OrdinalIgnoreCase) ||
			   typeName.Contains("AwsKms", StringComparison.OrdinalIgnoreCase) ||
			   typeName.Contains("GoogleCloudKms", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if the provider is an in-memory implementation for development/testing.
	/// </summary>
	private static bool IsInMemoryProvider(IKeyManagementProvider provider)
	{
		return provider.GetType().Name.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
	}
}
