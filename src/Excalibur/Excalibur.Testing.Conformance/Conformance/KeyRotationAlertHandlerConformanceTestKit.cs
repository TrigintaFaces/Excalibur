// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IKeyRotationAlertHandler conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateHandler"/> to verify that
/// your key rotation alert handler implementation conforms to the IKeyRotationAlertHandler contract.
/// </para>
/// <para>
/// The test kit verifies core alert handling operations including:
/// <list type="bullet">
/// <item><description>HandleRotationFailureAsync with various severity levels</description></item>
/// <item><description>HandleExpirationWarningAsync with various severity levels</description></item>
/// <item><description>HandleRotationSuccessAsync with nullable version properties</description></item>
/// <item><description>Null parameter validation (ArgumentNullException)</description></item>
/// <item><description>Fire-and-forget completion (Task.CompletedTask)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>ALERT-HANDLER PATTERN:</strong> IKeyRotationAlertHandler is an event-driven notification
/// interface. Methods return Task.CompletedTask - they do NOT throw on valid inputs.
/// </para>
/// <para>
/// <strong>SIMPLEST CONSTRUCTOR:</strong> LoggingAlertHandler requires only ILogger, making this
/// the simplest conformance kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingAlertHandlerConformanceTests : KeyRotationAlertHandlerConformanceTestKit
/// {
///     protected override IKeyRotationAlertHandler CreateHandler()
///     {
///         return new LoggingAlertHandler(NullLogger&lt;LoggingAlertHandler&gt;.Instance);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class KeyRotationAlertHandlerConformanceTestKit
{
	/// <summary>
	/// Creates a fresh key rotation alert handler instance for testing.
	/// </summary>
	/// <returns>An IKeyRotationAlertHandler implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For LoggingAlertHandler, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IKeyRotationAlertHandler CreateHandler() =>
	///     new LoggingAlertHandler(NullLogger&lt;LoggingAlertHandler&gt;.Instance);
	/// </code>
	/// </remarks>
	protected abstract IKeyRotationAlertHandler CreateHandler();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	protected virtual void Cleanup()
	{
	}

	/// <summary>
	/// Generates a unique key ID for test isolation.
	/// </summary>
	/// <returns>A unique key identifier.</returns>
	protected virtual string GenerateKeyId() => $"test-key-{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a failure alert with the specified consecutive failures count.
	/// </summary>
	/// <param name="consecutiveFailures">Number of consecutive failures (affects Severity).</param>
	/// <param name="keyId">Optional key ID (auto-generated if null).</param>
	/// <param name="provider">Optional provider name.</param>
	/// <param name="errorMessage">Optional error message.</param>
	/// <returns>A KeyRotationFailureAlert instance.</returns>
	protected virtual KeyRotationFailureAlert CreateFailureAlert(
		int consecutiveFailures = 1,
		string? keyId = null,
		string? provider = null,
		string? errorMessage = null) => new(
		keyId ?? GenerateKeyId(),
		provider ?? "test-provider",
		errorMessage ?? "Test error message",
		DateTimeOffset.UtcNow,
		consecutiveFailures);

	/// <summary>
	/// Creates an expiration alert with the specified days until expiration.
	/// </summary>
	/// <param name="daysUntilExpiration">Days until key expires (affects Severity).</param>
	/// <param name="keyId">Optional key ID (auto-generated if null).</param>
	/// <param name="provider">Optional provider name.</param>
	/// <returns>A KeyExpirationAlert instance.</returns>
	protected virtual KeyExpirationAlert CreateExpirationAlert(
		int daysUntilExpiration = 30,
		string? keyId = null,
		string? provider = null) => new(
		keyId ?? GenerateKeyId(),
		provider ?? "test-provider",
		DateTimeOffset.UtcNow.AddDays(daysUntilExpiration),
		daysUntilExpiration);

	/// <summary>
	/// Creates a success notification with optional nullable versions.
	/// </summary>
	/// <param name="keyId">Optional key ID (auto-generated if null).</param>
	/// <param name="provider">Optional provider name.</param>
	/// <param name="oldVersion">Old key version (nullable).</param>
	/// <param name="newVersion">New key version (nullable).</param>
	/// <returns>A KeyRotationSuccessNotification instance.</returns>
	protected virtual KeyRotationSuccessNotification CreateSuccessNotification(
		string? keyId = null,
		string? provider = null,
		string? oldVersion = "v1",
		string? newVersion = "v2") => new(
		keyId ?? GenerateKeyId(),
		provider ?? "test-provider",
		oldVersion,
		newVersion,
		DateTimeOffset.UtcNow);

	#region HandleRotationFailureAsync Tests

	/// <summary>
	/// Verifies that HandleRotationFailureAsync throws ArgumentNullException for null alert.
	/// </summary>
	protected virtual async Task HandleRotationFailureAsync_NullAlert_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleRotationFailureAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleRotationFailureAsync to throw ArgumentNullException for null alert.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationFailureAsync completes successfully with low severity.
	/// </summary>
	protected virtual async Task HandleRotationFailureAsync_LowSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// ConsecutiveFailures = 0 => Low severity
			var alert = CreateFailureAlert(consecutiveFailures: 0);

			// Verify severity
			if (alert.Severity != AlertSeverity.Low)
			{
				throw new TestFixtureAssertionException(
					$"Expected Low severity for 0 consecutive failures, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleRotationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationFailureAsync completes successfully with medium severity.
	/// </summary>
	protected virtual async Task HandleRotationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// ConsecutiveFailures = 1 or 2 => Medium severity
			var alert = CreateFailureAlert(consecutiveFailures: 1);

			// Verify severity
			if (alert.Severity != AlertSeverity.Medium)
			{
				throw new TestFixtureAssertionException(
					$"Expected Medium severity for 1 consecutive failure, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleRotationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationFailureAsync completes successfully with critical severity.
	/// </summary>
	protected virtual async Task HandleRotationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// ConsecutiveFailures >= 5 => Critical severity
			var alert = CreateFailureAlert(consecutiveFailures: 5);

			// Verify severity
			if (alert.Severity != AlertSeverity.Critical)
			{
				throw new TestFixtureAssertionException(
					$"Expected Critical severity for 5 consecutive failures, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleRotationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region HandleExpirationWarningAsync Tests

	/// <summary>
	/// Verifies that HandleExpirationWarningAsync throws ArgumentNullException for null alert.
	/// </summary>
	protected virtual async Task HandleExpirationWarningAsync_NullAlert_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleExpirationWarningAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleExpirationWarningAsync to throw ArgumentNullException for null alert.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleExpirationWarningAsync completes successfully with low severity.
	/// </summary>
	protected virtual async Task HandleExpirationWarningAsync_LowSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// DaysUntilExpiration > 14 => Low severity
			var alert = CreateExpirationAlert(daysUntilExpiration: 30);

			// Verify severity
			if (alert.Severity != AlertSeverity.Low)
			{
				throw new TestFixtureAssertionException(
					$"Expected Low severity for 30 days until expiration, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleExpirationWarningAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleExpirationWarningAsync completes successfully with high severity.
	/// </summary>
	protected virtual async Task HandleExpirationWarningAsync_HighSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// DaysUntilExpiration <= 7 and > 1 => High severity
			var alert = CreateExpirationAlert(daysUntilExpiration: 7);

			// Verify severity
			if (alert.Severity != AlertSeverity.High)
			{
				throw new TestFixtureAssertionException(
					$"Expected High severity for 7 days until expiration, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleExpirationWarningAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleExpirationWarningAsync completes successfully with critical severity.
	/// </summary>
	protected virtual async Task HandleExpirationWarningAsync_CriticalSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// DaysUntilExpiration <= 1 => Critical severity
			var alert = CreateExpirationAlert(daysUntilExpiration: 1);

			// Verify severity
			if (alert.Severity != AlertSeverity.Critical)
			{
				throw new TestFixtureAssertionException(
					$"Expected Critical severity for 1 day until expiration, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleExpirationWarningAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region HandleRotationSuccessAsync Tests

	/// <summary>
	/// Verifies that HandleRotationSuccessAsync throws ArgumentNullException for null notification.
	/// </summary>
	protected virtual async Task HandleRotationSuccessAsync_NullNotification_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleRotationSuccessAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleRotationSuccessAsync to throw ArgumentNullException for null notification.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationSuccessAsync completes successfully with valid notification.
	/// </summary>
	protected virtual async Task HandleRotationSuccessAsync_ValidNotification_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			var notification = CreateSuccessNotification(oldVersion: "v1", newVersion: "v2");

			// Act - Should complete without throwing
			await handler.HandleRotationSuccessAsync(notification, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationSuccessAsync completes successfully with null OldKeyVersion.
	/// </summary>
	protected virtual async Task HandleRotationSuccessAsync_NullOldVersion_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// OldKeyVersion can be null (e.g., first key creation)
			var notification = CreateSuccessNotification(oldVersion: null, newVersion: "v1");

			// Act - Should complete without throwing
			await handler.HandleRotationSuccessAsync(notification, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleRotationSuccessAsync completes successfully with both versions null.
	/// </summary>
	protected virtual async Task HandleRotationSuccessAsync_NullBothVersions_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Both versions can be null in edge cases
			var notification = CreateSuccessNotification(oldVersion: null, newVersion: null);

			// Act - Should complete without throwing
			await handler.HandleRotationSuccessAsync(notification, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	#endregion
}
