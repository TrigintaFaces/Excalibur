// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IComplianceAlertHandler conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateHandler"/> to verify that
/// your compliance alert handler implementation conforms to the IComplianceAlertHandler contract.
/// </para>
/// <para>
/// The test kit verifies core alert handling operations including:
/// <list type="bullet">
/// <item><description>HandleComplianceGapAsync with various severity levels</description></item>
/// <item><description>HandleValidationFailureAsync with various severity levels</description></item>
/// <item><description>HandleStatusChangeAsync for compliance lost/restored transitions</description></item>
/// <item><description>Null parameter validation (ArgumentNullException)</description></item>
/// <item><description>Fire-and-forget completion (Task.CompletedTask)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>ALERT-HANDLER PATTERN:</strong> IComplianceAlertHandler is an event-driven notification
/// interface for SOC 2 compliance alerts. Methods return Task.CompletedTask - they do NOT throw on valid inputs.
/// </para>
/// <para>
/// <strong>SIMPLE CONSTRUCTOR:</strong> LoggingComplianceAlertHandler requires only ILogger, making this
/// similar to KeyRotationAlertHandler.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingComplianceAlertHandlerConformanceTests : ComplianceAlertHandlerConformanceTestKit
/// {
///     protected override IComplianceAlertHandler CreateHandler()
///     {
///         return new LoggingComplianceAlertHandler(NullLogger&lt;LoggingComplianceAlertHandler&gt;.Instance);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ComplianceAlertHandlerConformanceTestKit
{
	/// <summary>
	/// Creates a fresh compliance alert handler instance for testing.
	/// </summary>
	/// <returns>An IComplianceAlertHandler implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For LoggingComplianceAlertHandler, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IComplianceAlertHandler CreateHandler() =>
	///     new LoggingComplianceAlertHandler(NullLogger&lt;LoggingComplianceAlertHandler&gt;.Instance);
	/// </code>
	/// </remarks>
	protected abstract IComplianceAlertHandler CreateHandler();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	protected virtual void Cleanup()
	{
	}

	/// <summary>
	/// Generates a unique gap ID for test isolation.
	/// </summary>
	/// <returns>A unique gap identifier.</returns>
	protected virtual string GenerateGapId() => $"test-gap-{Guid.NewGuid():N}";

	/// <summary>
	/// Generates a unique control ID for test isolation.
	/// </summary>
	/// <returns>A unique control identifier.</returns>
	protected virtual string GenerateControlId() => $"test-control-{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a compliance gap alert with the specified severity.
	/// </summary>
	/// <param name="severity">The gap severity.</param>
	/// <param name="gapId">Optional gap ID (auto-generated if null).</param>
	/// <param name="description">Optional description.</param>
	/// <returns>A ComplianceGapAlert instance.</returns>
	protected virtual ComplianceGapAlert CreateComplianceGapAlert(
		GapSeverity severity = GapSeverity.Medium,
		string? gapId = null,
		string? description = null) => new()
		{
			AlertId = Guid.NewGuid(),
			Gap = new ComplianceGap
			{
				GapId = gapId ?? GenerateGapId(),
				Description = description ?? "Test compliance gap",
				Criterion = TrustServicesCriterion.CC6_LogicalAccess,
				Severity = severity,
				IdentifiedAt = DateTimeOffset.UtcNow,
				Remediation = "Test remediation guidance"
			},
			GeneratedAt = DateTimeOffset.UtcNow,
			IsRecurring = false,
			OccurrenceCount = 1
		};

	/// <summary>
	/// Creates a control validation failure alert with the specified consecutive failures.
	/// </summary>
	/// <param name="consecutiveFailures">Number of consecutive failures (affects Severity).</param>
	/// <param name="controlId">Optional control ID (auto-generated if null).</param>
	/// <param name="errorMessage">Optional error message.</param>
	/// <returns>A ControlValidationFailureAlert instance.</returns>
	protected virtual ControlValidationFailureAlert CreateValidationFailureAlert(
		int consecutiveFailures = 1,
		string? controlId = null,
		string? errorMessage = null) => new()
		{
			AlertId = Guid.NewGuid(),
			ControlId = controlId ?? GenerateControlId(),
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ErrorMessage = errorMessage ?? "Test validation failure",
			FailedAt = DateTimeOffset.UtcNow,
			ConsecutiveFailures = consecutiveFailures
		};

	/// <summary>
	/// Creates a compliance status change notification.
	/// </summary>
	/// <param name="isCompliant">Whether the new status is compliant.</param>
	/// <param name="wasCompliant">Whether the previous status was compliant.</param>
	/// <param name="reason">Optional reason for the change.</param>
	/// <returns>A ComplianceStatusChangeNotification instance.</returns>
	protected virtual ComplianceStatusChangeNotification CreateStatusChangeNotification(
		bool isCompliant = true,
		bool wasCompliant = false,
		string? reason = null) => new()
		{
			NotificationId = Guid.NewGuid(),
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			WasCompliant = wasCompliant,
			IsCompliant = isCompliant,
			ChangedAt = DateTimeOffset.UtcNow,
			Reason = reason ?? "Test status change reason"
		};

	#region HandleComplianceGapAsync Tests

	/// <summary>
	/// Verifies that HandleComplianceGapAsync throws ArgumentNullException for null alert.
	/// </summary>
	protected virtual async Task HandleComplianceGapAsync_NullAlert_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleComplianceGapAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleComplianceGapAsync to throw ArgumentNullException for null alert.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleComplianceGapAsync completes successfully with low severity.
	/// </summary>
	protected virtual async Task HandleComplianceGapAsync_LowSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			var alert = CreateComplianceGapAlert(severity: GapSeverity.Low);

			// Verify severity
			if (alert.Gap.Severity != GapSeverity.Low)
			{
				throw new TestFixtureAssertionException(
					$"Expected Low severity, but got {alert.Gap.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleComplianceGapAsync completes successfully with critical severity.
	/// </summary>
	protected virtual async Task HandleComplianceGapAsync_CriticalSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			var alert = CreateComplianceGapAlert(severity: GapSeverity.Critical);

			// Verify severity
			if (alert.Gap.Severity != GapSeverity.Critical)
			{
				throw new TestFixtureAssertionException(
					$"Expected Critical severity, but got {alert.Gap.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region HandleValidationFailureAsync Tests

	/// <summary>
	/// Verifies that HandleValidationFailureAsync throws ArgumentNullException for null alert.
	/// </summary>
	protected virtual async Task HandleValidationFailureAsync_NullAlert_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleValidationFailureAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleValidationFailureAsync to throw ArgumentNullException for null alert.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleValidationFailureAsync completes successfully with medium severity (1 failure).
	/// </summary>
	protected virtual async Task HandleValidationFailureAsync_MediumSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// ConsecutiveFailures = 1 or 2 => Medium severity
			var alert = CreateValidationFailureAlert(consecutiveFailures: 1);

			// Verify severity
			if (alert.Severity != GapSeverity.Medium)
			{
				throw new TestFixtureAssertionException(
					$"Expected Medium severity for 1 consecutive failure, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleValidationFailureAsync completes successfully with critical severity (5+ failures).
	/// </summary>
	protected virtual async Task HandleValidationFailureAsync_CriticalSeverity_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// ConsecutiveFailures >= 5 => Critical severity
			var alert = CreateValidationFailureAlert(consecutiveFailures: 5);

			// Verify severity
			if (alert.Severity != GapSeverity.Critical)
			{
				throw new TestFixtureAssertionException(
					$"Expected Critical severity for 5 consecutive failures, but got {alert.Severity}.");
			}

			// Act - Should complete without throwing
			await handler.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	#endregion

	#region HandleStatusChangeAsync Tests

	/// <summary>
	/// Verifies that HandleStatusChangeAsync throws ArgumentNullException for null notification.
	/// </summary>
	protected virtual async Task HandleStatusChangeAsync_NullNotification_ShouldThrowArgumentNullException()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Act & Assert
			var caughtException = false;
			try
			{
				await handler.HandleStatusChangeAsync(null!, CancellationToken.None).ConfigureAwait(false);
			}
			catch (ArgumentNullException)
			{
				caughtException = true;
			}

			if (!caughtException)
			{
				throw new TestFixtureAssertionException(
					"Expected HandleStatusChangeAsync to throw ArgumentNullException for null notification.");
			}
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleStatusChangeAsync completes successfully when compliance is restored.
	/// </summary>
	protected virtual async Task HandleStatusChangeAsync_ComplianceRestored_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Compliance restored: was not compliant, now is compliant
			var notification = CreateStatusChangeNotification(isCompliant: true, wasCompliant: false);

			// Act - Should complete without throwing
			await handler.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);

			// Assert - If we get here, the method completed successfully
		}
		finally
		{
			Cleanup();
			(handler as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Verifies that HandleStatusChangeAsync completes successfully when compliance is lost.
	/// </summary>
	protected virtual async Task HandleStatusChangeAsync_ComplianceLost_ShouldCompleteSuccessfully()
	{
		// Arrange
		var handler = CreateHandler();
		try
		{
			// Compliance lost: was compliant, now is not compliant
			var notification = CreateStatusChangeNotification(isCompliant: false, wasCompliant: true);

			// Act - Should complete without throwing
			await handler.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);

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
