// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Defines a handler for key rotation alerts.
/// </summary>
/// <remarks>
/// Implement this interface to receive alerts about key rotation events,
/// such as rotation failures or keys nearing expiration.
/// </remarks>
public interface IKeyRotationAlertHandler
{
	/// <summary>
	/// Handles a key rotation failure alert.
	/// </summary>
	/// <param name="alert">The alert details.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleRotationFailureAsync(KeyRotationFailureAlert alert, CancellationToken cancellationToken);

	/// <summary>
	/// Handles a key expiration warning alert.
	/// </summary>
	/// <param name="alert">The alert details.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleExpirationWarningAsync(KeyExpirationAlert alert, CancellationToken cancellationToken);

	/// <summary>
	/// Handles a key rotation success notification.
	/// </summary>
	/// <param name="notification">The notification details.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task HandleRotationSuccessAsync(KeyRotationSuccessNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Represents an alert for a key rotation failure.
/// </summary>
/// <param name="KeyId">The identifier of the key that failed to rotate.</param>
/// <param name="Provider">The KMS provider name.</param>
/// <param name="ErrorMessage">The error message describing the failure.</param>
/// <param name="FailedAt">When the failure occurred.</param>
/// <param name="ConsecutiveFailures">Number of consecutive failures for this key.</param>
public sealed record KeyRotationFailureAlert(
	string KeyId,
	string Provider,
	string ErrorMessage,
	DateTimeOffset FailedAt,
	int ConsecutiveFailures)
{
	/// <summary>
	/// Gets the severity based on consecutive failures.
	/// </summary>
	public AlertSeverity Severity => ConsecutiveFailures switch
	{
		>= 5 => AlertSeverity.Critical,
		>= 3 => AlertSeverity.High,
		>= 1 => AlertSeverity.Medium,
		_ => AlertSeverity.Low
	};
}

/// <summary>
/// Represents an alert for keys approaching expiration.
/// </summary>
/// <param name="KeyId">The identifier of the key nearing expiration.</param>
/// <param name="Provider">The KMS provider name.</param>
/// <param name="ExpiresAt">When the key expires.</param>
/// <param name="DaysUntilExpiration">Days remaining until expiration.</param>
public sealed record KeyExpirationAlert(
	string KeyId,
	string Provider,
	DateTimeOffset ExpiresAt,
	int DaysUntilExpiration)
{
	/// <summary>
	/// Gets the severity based on days until expiration.
	/// </summary>
	public AlertSeverity Severity => DaysUntilExpiration switch
	{
		<= 1 => AlertSeverity.Critical,
		<= 7 => AlertSeverity.High,
		<= 14 => AlertSeverity.Medium,
		_ => AlertSeverity.Low
	};
}

/// <summary>
/// Represents a notification for successful key rotation.
/// </summary>
/// <param name="KeyId">The identifier of the rotated key.</param>
/// <param name="Provider">The KMS provider name.</param>
/// <param name="OldKeyVersion">The previous key version.</param>
/// <param name="NewKeyVersion">The new key version.</param>
/// <param name="RotatedAt">When the rotation completed.</param>
public sealed record KeyRotationSuccessNotification(
	string KeyId,
	string Provider,
	string? OldKeyVersion,
	string? NewKeyVersion,
	DateTimeOffset RotatedAt);

/// <summary>
/// Represents the severity level of an alert.
/// </summary>
public enum AlertSeverity
{
	/// <summary>
	/// Low severity - informational.
	/// </summary>
	Low = 0,

	/// <summary>
	/// Medium severity - requires attention.
	/// </summary>
	Medium = 1,

	/// <summary>
	/// High severity - requires prompt action.
	/// </summary>
	High = 2,

	/// <summary>
	/// Critical severity - requires immediate action.
	/// </summary>
	Critical = 3
}
