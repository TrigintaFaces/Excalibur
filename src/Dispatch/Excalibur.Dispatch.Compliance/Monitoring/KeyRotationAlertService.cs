// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Service for managing key rotation alerts and notifications.
/// </summary>
/// <remarks>
/// <para>
/// This service tracks key rotation failures and triggers alerts when thresholds are exceeded.
/// It also monitors key expiration and sends warnings before keys expire.
/// </para>
/// <para>
/// Register <see cref="IKeyRotationAlertHandler"/> implementations to receive alerts.
/// Multiple handlers can be registered for different notification channels
/// (e.g., email, Slack, PagerDuty).
/// </para>
/// </remarks>
public sealed partial class KeyRotationAlertService
{
	private readonly IEnumerable<IKeyRotationAlertHandler> _handlers;
	private readonly IComplianceMetrics? _metrics;
	private readonly ILogger<KeyRotationAlertService> _logger;
	private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
	private readonly KeyRotationAlertOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="KeyRotationAlertService"/> class.
	/// </summary>
	/// <param name="handlers">The alert handlers to notify.</param>
	/// <param name="metrics">Optional compliance metrics for recording alerts.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="options">Alert configuration options.</param>
	public KeyRotationAlertService(
		IEnumerable<IKeyRotationAlertHandler> handlers,
		IComplianceMetrics? metrics,
		ILogger<KeyRotationAlertService> logger,
		KeyRotationAlertOptions? options = null)
	{
		_handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
		_metrics = metrics;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options = options ?? new KeyRotationAlertOptions();
	}

	/// <summary>
	/// Reports a key rotation failure.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="errorMessage">The error message.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ReportRotationFailureAsync(
		string keyId,
		string provider,
		string errorMessage,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(provider);

		var compositeKey = $"{provider}:{keyId}";
		var consecutiveFailures = _consecutiveFailures.AddOrUpdate(compositeKey, 1, (_, count) => count + 1);

		LogKeyRotationFailureReported(keyId, provider, errorMessage, consecutiveFailures);

		_metrics?.RecordKeyRotationFailure(keyId, provider, ExtractErrorType(errorMessage));

		if (consecutiveFailures >= _options.AlertAfterFailures)
		{
			var alert = new KeyRotationFailureAlert(
				keyId,
				provider,
				errorMessage,
				DateTimeOffset.UtcNow,
				consecutiveFailures);

			await NotifyHandlersAsync(
				h => h.HandleRotationFailureAsync(alert, cancellationToken),
				"rotation failure",
				cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reports a successful key rotation.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="oldKeyVersion">The previous key version.</param>
	/// <param name="newKeyVersion">The new key version.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ReportRotationSuccessAsync(
		string keyId,
		string provider,
		string? oldKeyVersion,
		string? newKeyVersion,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(provider);

		var compositeKey = $"{provider}:{keyId}";
		_ = _consecutiveFailures.TryRemove(compositeKey, out _);

		LogKeyRotationSuccessReported(keyId, provider, oldKeyVersion, newKeyVersion);

		_metrics?.RecordKeyRotation(keyId, provider);

		if (_options.NotifyOnSuccess)
		{
			var notification = new KeyRotationSuccessNotification(
				keyId,
				provider,
				oldKeyVersion,
				newKeyVersion,
				DateTimeOffset.UtcNow);

			await NotifyHandlersAsync(
				h => h.HandleRotationSuccessAsync(notification, cancellationToken),
				"rotation success",
				cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reports a key approaching expiration.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <param name="expiresAt">When the key expires.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ReportExpirationWarningAsync(
		string keyId,
		string provider,
		DateTimeOffset expiresAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(keyId);
		ArgumentException.ThrowIfNullOrEmpty(provider);

		var daysUntilExpiration = (int)(expiresAt - DateTimeOffset.UtcNow).TotalDays;

		if (daysUntilExpiration <= _options.ExpirationWarningDays)
		{
			LogKeyExpirationWarningReported(keyId, provider, daysUntilExpiration, expiresAt);

			var alert = new KeyExpirationAlert(keyId, provider, expiresAt, daysUntilExpiration);

			await NotifyHandlersAsync(
				h => h.HandleExpirationWarningAsync(alert, cancellationToken),
				"expiration warning",
				cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Resets the failure counter for a specific key.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="provider">The KMS provider name.</param>
	public void ResetFailureCount(string keyId, string provider)
	{
		var compositeKey = $"{provider}:{keyId}";
		_ = _consecutiveFailures.TryRemove(compositeKey, out _);
	}

	/// <summary>
	/// Gets the current consecutive failure count for a key.
	/// </summary>
	/// <param name="keyId">The key identifier.</param>
	/// <param name="provider">The KMS provider name.</param>
	/// <returns>The number of consecutive failures.</returns>
	public int GetFailureCount(string keyId, string provider)
	{
		var compositeKey = $"{provider}:{keyId}";
		return _consecutiveFailures.GetValueOrDefault(compositeKey, 0);
	}

	private static string ExtractErrorType(string errorMessage)
	{
		if (string.IsNullOrEmpty(errorMessage))
		{
			return "Unknown";
		}

		if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
		{
			return "Timeout";
		}

		if (errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
		{
			return "AuthorizationError";
		}

		if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
		{
			return "NotFound";
		}

		if (errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
			errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase))
		{
			return "NetworkError";
		}

		return "GeneralError";
	}

	private async Task NotifyHandlersAsync(
		Func<IKeyRotationAlertHandler, Task> action,
		string alertType,
		CancellationToken cancellationToken)
	{
		foreach (var handler in _handlers)
		{
			try
			{
				await action(handler).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				LogKeyRotationAlertNotifyFailed(handler.GetType().Name, alertType, ex);
			}
		}
	}

	[LoggerMessage(
		ComplianceEventId.KeyRotationFailureReported,
		LogLevel.Warning,
		"Key rotation failure for {KeyId} on {Provider}: {ErrorMessage}. Consecutive failures: {ConsecutiveFailures}")]
	private partial void LogKeyRotationFailureReported(
		string keyId,
		string provider,
		string errorMessage,
		int consecutiveFailures);

	[LoggerMessage(
		ComplianceEventId.KeyRotationSuccessReported,
		LogLevel.Information,
		"Key rotation succeeded for {KeyId} on {Provider}. Version: {OldKeyVersion} -> {NewKeyVersion}")]
	private partial void LogKeyRotationSuccessReported(
		string keyId,
		string provider,
		string? oldKeyVersion,
		string? newKeyVersion);

	[LoggerMessage(
		ComplianceEventId.KeyExpirationWarningReported,
		LogLevel.Warning,
		"Key {KeyId} on {Provider} expires in {Days} days at {ExpiresAt}")]
	private partial void LogKeyExpirationWarningReported(
		string keyId,
		string provider,
		int days,
		DateTimeOffset expiresAt);

	[LoggerMessage(
		ComplianceEventId.KeyRotationAlertNotifyFailed,
		LogLevel.Error,
		"Failed to notify handler {Handler} for {AlertType}")]
	private partial void LogKeyRotationAlertNotifyFailed(string handler, string alertType, Exception exception);
}

/// <summary>
/// Configuration options for the key rotation alert service.
/// </summary>
public sealed class KeyRotationAlertOptions
{
	/// <summary>
	/// Gets or sets the number of consecutive failures before triggering an alert.
	/// Default is 1 (alert on first failure).
	/// </summary>
	public int AlertAfterFailures { get; set; } = 1;

	/// <summary>
	/// Gets or sets whether to send notifications on successful rotations.
	/// Default is false.
	/// </summary>
	public bool NotifyOnSuccess { get; set; }

	/// <summary>
	/// Gets or sets the number of days before expiration to start sending warnings.
	/// Default is 14 days.
	/// </summary>
	public int ExpirationWarningDays { get; set; } = 14;
}
