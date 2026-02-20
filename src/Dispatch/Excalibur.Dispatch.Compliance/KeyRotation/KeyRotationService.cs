// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Background service that automatically rotates encryption keys based on configured policies.
/// </summary>
/// <remarks>
/// <para>
/// This service implements:
/// - Policy-based automatic rotation (default 90-day cycle)
/// - Per-key-purpose rotation schedules
/// - Zero-downtime rotation with key versioning
/// - Rotation success/failure metrics
/// </para>
/// </remarks>
public partial class KeyRotationService : BackgroundService, IKeyRotationScheduler
{
	private readonly IKeyManagementProvider _keyProvider;
	private readonly IOptions<KeyRotationOptions> _options;
	private readonly ILogger<KeyRotationService> _logger;
	private readonly SemaphoreSlim _rotationSemaphore;
	private readonly Dictionary<string, DateTimeOffset> _failedRotations = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationService"/> class.
	/// </summary>
	/// <param name="keyProvider">The key management provider.</param>
	/// <param name="options">The rotation options.</param>
	/// <param name="logger">The logger.</param>
	public KeyRotationService(
		IKeyManagementProvider keyProvider,
		IOptions<KeyRotationOptions> options,
		ILogger<KeyRotationService> logger)
	{
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_rotationSemaphore = new SemaphoreSlim(options.Value.MaxConcurrentRotations);
	}

	/// <inheritdoc />
	public async Task<KeyRotationBatchResult> CheckAndRotateAsync(CancellationToken cancellationToken)
	{
		var config = _options.Value;
		var startedAt = DateTimeOffset.UtcNow;
		var results = new List<KeyRotationResult>();
		var errors = new List<KeyRotationError>();
		var keysDueForRotation = 0;

		// Get all active keys
		var activeKeys = await _keyProvider
			.ListKeysAsync(KeyStatus.Active, purpose: null, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		foreach (var key in activeKeys)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var policy = config.GetPolicyForPurpose(key.Purpose);

			// Check if key should be rotated
			if (!policy.IsRotationDue(key))
			{
				// Log warning if approaching rotation
				if (policy.ShouldWarn(key))
				{
					var nextRotation = policy.GetNextRotationTime(key);
					LogKeyRotationApproaching(key.KeyId, nextRotation);
				}

				continue;
			}

			keysDueForRotation++;

			// Check if this key recently failed and should be retried
			if (_failedRotations.TryGetValue(key.KeyId, out var lastFailure))
			{
				if (DateTimeOffset.UtcNow - lastFailure < config.RetryDelay)
				{
					LogKeyRotationRetryDelayed(
						key.KeyId,
						lastFailure.Add(config.RetryDelay));
					continue;
				}
			}

			// Perform rotation with concurrency control
			await _rotationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				cts.CancelAfter(config.RotationTimeout);

				LogKeyRotationStarted(
					key.KeyId,
					key.Purpose ?? "default",
					key.Version,
					key.CreatedAt);

				var result = await _keyProvider.RotateKeyAsync(
					key.KeyId,
					policy.Algorithm,
					key.Purpose,
					expiresAt: null,
					cancellationToken: cts.Token).ConfigureAwait(false);

				results.Add(result);

				if (result.Success)
				{
					_ = _failedRotations.Remove(key.KeyId);
					LogKeyRotationSucceeded(key.KeyId, result.NewKey?.Version);
				}
				else
				{
					_failedRotations[key.KeyId] = DateTimeOffset.UtcNow;
					errors.Add(new KeyRotationError { KeyId = key.KeyId, Message = result.ErrorMessage ?? "Unknown error" });

					LogKeyRotationFailed(key.KeyId, result.ErrorMessage);

					if (!config.ContinueOnError)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				_failedRotations[key.KeyId] = DateTimeOffset.UtcNow;
				errors.Add(new KeyRotationError { KeyId = key.KeyId, Message = ex.Message, Exception = ex });

				LogKeyRotationException(key.KeyId, ex);

				if (!config.ContinueOnError)
				{
					throw;
				}
			}
			finally
			{
				_ = _rotationSemaphore.Release();
			}
		}

		return new KeyRotationBatchResult
		{
			KeysChecked = activeKeys.Count,
			KeysDueForRotation = keysDueForRotation,
			KeysRotated = results.Count(r => r.Success),
			KeysFailed = errors.Count, // Errors list contains both result failures and exceptions
			Results = results,
			Errors = errors,
			StartedAt = startedAt,
			CompletedAt = DateTimeOffset.UtcNow
		};
	}

	/// <inheritdoc />
	public async Task<bool> IsRotationDueAsync(string keyId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var key = await _keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (key is null)
		{
			return false;
		}

		var policy = _options.Value.GetPolicyForPurpose(key.Purpose);
		return policy.IsRotationDue(key);
	}

	/// <inheritdoc />
	public async Task<KeyRotationResult> ForceRotateAsync(
		string keyId,
		string reason,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(reason);

		var key = await _keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (key is null)
		{
			LogKeyRotationForceMissingKey(keyId);
			return KeyRotationResult.Failed($"Key not found: {keyId}");
		}

		var policy = _options.Value.GetPolicyForPurpose(key.Purpose);

		LogKeyRotationForceStarted(keyId, reason);

		var result = await _keyProvider.RotateKeyAsync(
			keyId,
			policy.Algorithm,
			key.Purpose,
			expiresAt: null,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		if (result.Success)
		{
			_ = _failedRotations.Remove(keyId);
			LogKeyRotationForceSucceeded(keyId, result.NewKey?.Version);
		}
		else
		{
			LogKeyRotationForceFailed(keyId, result.ErrorMessage);
		}

		return result;
	}

	/// <inheritdoc />
	public async Task<DateTimeOffset?> GetNextRotationTimeAsync(
		string keyId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);

		var key = await _keyProvider.GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
		if (key is null)
		{
			return null;
		}

		var policy = _options.Value.GetPolicyForPurpose(key.Purpose);

		if (!policy.AutoRotateEnabled)
		{
			return null;
		}

		return policy.GetNextRotationTime(key);
	}

	/// <inheritdoc />
	public override void Dispose()
	{
		_rotationSemaphore.Dispose();
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var config = _options.Value;

		if (!config.Enabled)
		{
			LogKeyRotationServiceDisabled();
			return;
		}

		LogKeyRotationServiceStarted(
			config.CheckInterval,
			config.DefaultPolicy.Name,
			config.DefaultPolicy.MaxKeyAge.TotalDays);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				var result = await CheckAndRotateAsync(stoppingToken).ConfigureAwait(false);

				if (result.KeysRotated > 0 || result.KeysFailed > 0)
				{
					LogKeyRotationCheckCompleted(
						result.KeysChecked,
						result.KeysRotated,
						result.KeysFailed);
				}
				else
				{
					LogKeyRotationCheckNoKeys(result.KeysChecked);
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				LogKeyRotationCheckError(ex);
			}

			try
			{
				await Task.Delay(config.CheckInterval, stoppingToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}

		LogKeyRotationServiceStopped();
	}

	[LoggerMessage(
		ComplianceEventId.KeyRotationServiceDisabled,
		LogLevel.Information,
		"Key rotation service is disabled")]
	private partial void LogKeyRotationServiceDisabled();

	[LoggerMessage(
		ComplianceEventId.KeyRotationServiceStarted,
		LogLevel.Information,
		"Key rotation service started. Check interval: {Interval}, Default policy: {PolicyName} ({MaxAge} days)")]
	private partial void LogKeyRotationServiceStarted(TimeSpan interval, string policyName, double maxAge);

	[LoggerMessage(
		ComplianceEventId.KeyRotationCheckCompleted,
		LogLevel.Information,
		"Rotation check completed. Checked: {CheckedCount}, Rotated: {Rotated}, Failed: {Failed}")]
	private partial void LogKeyRotationCheckCompleted(int checkedCount, int rotated, int failed);

	[LoggerMessage(
		ComplianceEventId.KeyRotationCheckNoKeys,
		LogLevel.Debug,
		"Rotation check completed. No keys due for rotation. Checked: {CheckedCount}")]
	private partial void LogKeyRotationCheckNoKeys(int checkedCount);

	[LoggerMessage(
		ComplianceEventId.KeyRotationCheckError,
		LogLevel.Error,
		"Error during key rotation check")]
	private partial void LogKeyRotationCheckError(Exception exception);

	[LoggerMessage(
		ComplianceEventId.KeyRotationServiceStopped,
		LogLevel.Information,
		"Key rotation service stopped")]
	private partial void LogKeyRotationServiceStopped();

	[LoggerMessage(
		ComplianceEventId.KeyRotationApproaching,
		LogLevel.Warning,
		"Key {KeyId} approaching rotation. Scheduled for: {NextRotation}")]
	private partial void LogKeyRotationApproaching(string keyId, DateTimeOffset nextRotation);

	[LoggerMessage(
		ComplianceEventId.KeyRotationRetryDelayed,
		LogLevel.Debug,
		"Skipping recently failed key {KeyId}. Will retry after: {RetryAfter}")]
	private partial void LogKeyRotationRetryDelayed(string keyId, DateTimeOffset retryAfter);

	[LoggerMessage(
		ComplianceEventId.KeyRotationStarted,
		LogLevel.Information,
		"Rotating key {KeyId} (purpose: {Purpose}, version: {Version}, created: {Created})")]
	private partial void LogKeyRotationStarted(string keyId, string purpose, int version, DateTimeOffset created);

	[LoggerMessage(
		ComplianceEventId.KeyRotationSucceeded,
		LogLevel.Information,
		"Successfully rotated key {KeyId}. New version: {Version}")]
	private partial void LogKeyRotationSucceeded(string keyId, int? version);

	[LoggerMessage(
		ComplianceEventId.KeyRotationFailed,
		LogLevel.Error,
		"Failed to rotate key {KeyId}: {Error}")]
	private partial void LogKeyRotationFailed(string keyId, string? error);

	[LoggerMessage(
		ComplianceEventId.KeyRotationException,
		LogLevel.Error,
		"Exception during rotation of key {KeyId}")]
	private partial void LogKeyRotationException(string keyId, Exception exception);

	[LoggerMessage(
		ComplianceEventId.KeyRotationForceMissingKey,
		LogLevel.Warning,
		"Attempted to force rotate non-existent key: {KeyId}")]
	private partial void LogKeyRotationForceMissingKey(string keyId);

	[LoggerMessage(
		ComplianceEventId.KeyRotationForceStarted,
		LogLevel.Warning,
		"Force rotating key {KeyId}. Reason: {Reason}")]
	private partial void LogKeyRotationForceStarted(string keyId, string reason);

	[LoggerMessage(
		ComplianceEventId.KeyRotationForceSucceeded,
		LogLevel.Information,
		"Force rotation of key {KeyId} succeeded. New version: {Version}")]
	private partial void LogKeyRotationForceSucceeded(string keyId, int? version);

	[LoggerMessage(
		ComplianceEventId.KeyRotationForceFailed,
		LogLevel.Error,
		"Force rotation of key {KeyId} failed: {Error}")]
	private partial void LogKeyRotationForceFailed(string keyId, string? error);
}
