// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Compliance.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// A default alert handler that logs alerts using structured logging.
/// </summary>
/// <remarks>
/// <para>
/// This handler provides a baseline implementation that writes all alerts
/// to the configured logging infrastructure. Use this in combination with
/// a centralized logging system (e.g., ELK, Splunk, CloudWatch) for production monitoring.
/// </para>
/// <para>
/// For active alerting (email, Slack, PagerDuty), implement custom
/// <see cref="IKeyRotationAlertHandler"/> implementations.
/// </para>
/// </remarks>
public sealed partial class LoggingAlertHandler : IKeyRotationAlertHandler
{
	private readonly ILogger<LoggingAlertHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LoggingAlertHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public LoggingAlertHandler(ILogger<LoggingAlertHandler> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public Task HandleRotationFailureAsync(KeyRotationFailureAlert alert, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(alert);

		if (alert.Severity == AlertSeverity.Critical)
		{
			LogKeyRotationFailureCritical(
					alert.KeyId,
					alert.Provider,
					alert.ErrorMessage,
					alert.ConsecutiveFailures,
					alert.Severity);
		}
		else if (alert.Severity == AlertSeverity.High)
		{
			LogKeyRotationFailureHigh(
					alert.KeyId,
					alert.Provider,
					alert.ErrorMessage,
					alert.ConsecutiveFailures,
					alert.Severity);
		}
		else if (alert.Severity == AlertSeverity.Medium)
		{
			LogKeyRotationFailureMedium(
					alert.KeyId,
					alert.Provider,
					alert.ErrorMessage,
					alert.ConsecutiveFailures,
					alert.Severity);
		}
		else
		{
			LogKeyRotationFailureLow(
					alert.KeyId,
					alert.Provider,
					alert.ErrorMessage,
					alert.ConsecutiveFailures,
					alert.Severity);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task HandleExpirationWarningAsync(KeyExpirationAlert alert, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(alert);

		if (alert.Severity == AlertSeverity.Critical)
		{
			LogKeyExpirationWarningCritical(
					alert.KeyId,
					alert.Provider,
					alert.ExpiresAt,
					alert.DaysUntilExpiration,
					alert.Severity);
		}
		else if (alert.Severity == AlertSeverity.High)
		{
			LogKeyExpirationWarningHigh(
					alert.KeyId,
					alert.Provider,
					alert.ExpiresAt,
					alert.DaysUntilExpiration,
					alert.Severity);
		}
		else if (alert.Severity == AlertSeverity.Medium)
		{
			LogKeyExpirationWarningMedium(
					alert.KeyId,
					alert.Provider,
					alert.ExpiresAt,
					alert.DaysUntilExpiration,
					alert.Severity);
		}
		else
		{
			LogKeyExpirationWarningLow(
					alert.KeyId,
					alert.Provider,
					alert.ExpiresAt,
					alert.DaysUntilExpiration,
					alert.Severity);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task HandleRotationSuccessAsync(KeyRotationSuccessNotification notification, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(notification);

		LogKeyRotationSuccess(
				notification.KeyId,
				notification.Provider,
				notification.OldKeyVersion,
				notification.NewKeyVersion,
				notification.RotatedAt);

		return Task.CompletedTask;
	}

	[LoggerMessage(
			ComplianceEventId.KeyRotationFailureAlertCriticalLogged,
			LogLevel.Critical,
			"KEY_ROTATION_FAILURE: Key={KeyId} Provider={Provider} Error={ErrorMessage} ConsecutiveFailures={ConsecutiveFailures} Severity={Severity}")]
	private partial void LogKeyRotationFailureCritical(
			string keyId,
			string provider,
			string errorMessage,
			int consecutiveFailures,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyRotationFailureAlertHighLogged,
			LogLevel.Error,
			"KEY_ROTATION_FAILURE: Key={KeyId} Provider={Provider} Error={ErrorMessage} ConsecutiveFailures={ConsecutiveFailures} Severity={Severity}")]
	private partial void LogKeyRotationFailureHigh(
			string keyId,
			string provider,
			string errorMessage,
			int consecutiveFailures,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyRotationFailureAlertMediumLogged,
			LogLevel.Warning,
			"KEY_ROTATION_FAILURE: Key={KeyId} Provider={Provider} Error={ErrorMessage} ConsecutiveFailures={ConsecutiveFailures} Severity={Severity}")]
	private partial void LogKeyRotationFailureMedium(
			string keyId,
			string provider,
			string errorMessage,
			int consecutiveFailures,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyRotationFailureAlertLowLogged,
			LogLevel.Information,
			"KEY_ROTATION_FAILURE: Key={KeyId} Provider={Provider} Error={ErrorMessage} ConsecutiveFailures={ConsecutiveFailures} Severity={Severity}")]
	private partial void LogKeyRotationFailureLow(
			string keyId,
			string provider,
			string errorMessage,
			int consecutiveFailures,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyExpirationWarningAlertCriticalLogged,
			LogLevel.Critical,
			"KEY_EXPIRATION_WARNING: Key={KeyId} Provider={Provider} ExpiresAt={ExpiresAt} DaysRemaining={DaysRemaining} Severity={Severity}")]
	private partial void LogKeyExpirationWarningCritical(
			string keyId,
			string provider,
			DateTimeOffset expiresAt,
			int daysRemaining,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyExpirationWarningAlertHighLogged,
			LogLevel.Error,
			"KEY_EXPIRATION_WARNING: Key={KeyId} Provider={Provider} ExpiresAt={ExpiresAt} DaysRemaining={DaysRemaining} Severity={Severity}")]
	private partial void LogKeyExpirationWarningHigh(
			string keyId,
			string provider,
			DateTimeOffset expiresAt,
			int daysRemaining,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyExpirationWarningAlertMediumLogged,
			LogLevel.Warning,
			"KEY_EXPIRATION_WARNING: Key={KeyId} Provider={Provider} ExpiresAt={ExpiresAt} DaysRemaining={DaysRemaining} Severity={Severity}")]
	private partial void LogKeyExpirationWarningMedium(
			string keyId,
			string provider,
			DateTimeOffset expiresAt,
			int daysRemaining,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyExpirationWarningAlertLowLogged,
			LogLevel.Information,
			"KEY_EXPIRATION_WARNING: Key={KeyId} Provider={Provider} ExpiresAt={ExpiresAt} DaysRemaining={DaysRemaining} Severity={Severity}")]
	private partial void LogKeyExpirationWarningLow(
			string keyId,
			string provider,
			DateTimeOffset expiresAt,
			int daysRemaining,
			AlertSeverity severity);

	[LoggerMessage(
			ComplianceEventId.KeyRotationSuccessAlertLogged,
			LogLevel.Information,
			"KEY_ROTATION_SUCCESS: Key={KeyId} Provider={Provider} OldVersion={OldVersion} NewVersion={NewVersion} RotatedAt={RotatedAt}")]
	private partial void LogKeyRotationSuccess(
			string keyId,
			string provider,
			string? oldVersion,
			string? newVersion,
			DateTimeOffset rotatedAt);
}
