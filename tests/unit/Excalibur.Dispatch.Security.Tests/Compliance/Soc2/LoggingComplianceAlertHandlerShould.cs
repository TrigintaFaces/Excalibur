// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Soc2;

/// <summary>
/// Unit tests for <see cref="LoggingComplianceAlertHandler"/>.
/// Tests alert handling behavior per ADR-055.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class LoggingComplianceAlertHandlerShould
{
	private readonly ILogger<LoggingComplianceAlertHandler> _logger;
	private readonly LoggingComplianceAlertHandler _sut;

	public LoggingComplianceAlertHandlerShould()
	{
		_logger = NullLogger<LoggingComplianceAlertHandler>.Instance;
		_sut = new LoggingComplianceAlertHandler(_logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new LoggingComplianceAlertHandler(null!))
			.ParamName.ShouldBe("logger");
	}

	#endregion

	#region HandleComplianceGapAsync Tests

	[Fact]
	public async Task HandleComplianceGapAsync_ThrowsArgumentNullException_WhenAlertIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.HandleComplianceGapAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_CompletesSuccessfully_ForCriticalSeverity()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.Critical);

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_CompletesSuccessfully_ForHighSeverity()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.High);

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_CompletesSuccessfully_ForMediumSeverity()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.Medium);

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_CompletesSuccessfully_ForLowSeverity()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.Low);

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_HandlesRemediationGuidance()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.High, remediation: "Update configuration");

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_HandlesRecurringAlerts()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.High) with
		{
			IsRecurring = true,
			OccurrenceCount = 5
		};

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleComplianceGapAsync_HandlesAlertWithTenantId()
	{
		// Arrange
		var alert = CreateComplianceGapAlert(GapSeverity.High) with
		{
			TenantId = "tenant-123"
		};

		// Act & Assert - should not throw
		await _sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	#endregion

	#region HandleValidationFailureAsync Tests

	[Fact]
	public async Task HandleValidationFailureAsync_ThrowsArgumentNullException_WhenAlertIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.HandleValidationFailureAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleValidationFailureAsync_CompletesSuccessfully_ForCriticalSeverity()
	{
		// Arrange - 5+ consecutive failures = Critical
		var alert = CreateValidationFailureAlert(consecutiveFailures: 5);
		alert.Severity.ShouldBe(GapSeverity.Critical);

		// Act & Assert - should not throw
		await _sut.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleValidationFailureAsync_CompletesSuccessfully_ForHighSeverity()
	{
		// Arrange - 3-4 consecutive failures = High
		var alert = CreateValidationFailureAlert(consecutiveFailures: 3);
		alert.Severity.ShouldBe(GapSeverity.High);

		// Act & Assert - should not throw
		await _sut.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleValidationFailureAsync_CompletesSuccessfully_ForMediumSeverity()
	{
		// Arrange - 1-2 consecutive failures = Medium
		var alert = CreateValidationFailureAlert(consecutiveFailures: 1);
		alert.Severity.ShouldBe(GapSeverity.Medium);

		// Act & Assert - should not throw
		await _sut.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleValidationFailureAsync_HandlesAlertWithTenantId()
	{
		// Arrange
		var alert = CreateValidationFailureAlert(consecutiveFailures: 1) with
		{
			TenantId = "tenant-456"
		};

		// Act & Assert - should not throw
		await _sut.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	#endregion

	#region HandleStatusChangeAsync Tests

	[Fact]
	public async Task HandleStatusChangeAsync_ThrowsArgumentNullException_WhenNotificationIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.HandleStatusChangeAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleStatusChangeAsync_CompletesSuccessfully_WhenCompliant()
	{
		// Arrange
		var notification = CreateStatusChangeNotification(wasCompliant: false, isCompliant: true);

		// Act & Assert - should not throw
		await _sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleStatusChangeAsync_CompletesSuccessfully_WhenNotCompliant()
	{
		// Arrange
		var notification = CreateStatusChangeNotification(wasCompliant: true, isCompliant: false);

		// Act & Assert - should not throw
		await _sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleStatusChangeAsync_HandlesNullReason()
	{
		// Arrange
		var notification = CreateStatusChangeNotification(wasCompliant: true, isCompliant: true, reason: null);

		// Act & Assert - should not throw
		await _sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleStatusChangeAsync_HandlesNotificationWithTenantId()
	{
		// Arrange
		var notification = CreateStatusChangeNotification(wasCompliant: true, isCompliant: true) with
		{
			TenantId = "tenant-789"
		};

		// Act & Assert - should not throw
		await _sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleStatusChangeAsync_HandlesEmptyReason()
	{
		// Arrange
		var notification = CreateStatusChangeNotification(wasCompliant: true, isCompliant: false, reason: "");

		// Act & Assert - should not throw
		await _sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	#endregion

	#region Severity Calculation Tests

	[Theory]
	[InlineData(0, GapSeverity.Low)]
	[InlineData(1, GapSeverity.Medium)]
	[InlineData(2, GapSeverity.Medium)]
	[InlineData(3, GapSeverity.High)]
	[InlineData(4, GapSeverity.High)]
	[InlineData(5, GapSeverity.Critical)]
	[InlineData(10, GapSeverity.Critical)]
	public void ControlValidationFailureAlert_CalculatesSeverity_BasedOnConsecutiveFailures(
		int consecutiveFailures, GapSeverity expectedSeverity)
	{
		// Arrange
		var alert = CreateValidationFailureAlert(consecutiveFailures);

		// Assert
		alert.Severity.ShouldBe(expectedSeverity);
	}

	#endregion

	#region Helpers

	private static ComplianceGapAlert CreateComplianceGapAlert(
		GapSeverity severity,
		string? remediation = "Apply fix")
	{
		return new ComplianceGapAlert
		{
			AlertId = Guid.NewGuid(),
			GeneratedAt = DateTimeOffset.UtcNow,
			Gap = new ComplianceGap
			{
				GapId = "GAP-001",
				Description = "Test gap description",
				Criterion = TrustServicesCriterion.CC6_LogicalAccess,
				Severity = severity,
				IdentifiedAt = DateTimeOffset.UtcNow,
				Remediation = remediation ?? "No remediation provided"
			},
			IsRecurring = false,
			OccurrenceCount = 1
		};
	}

	private static ControlValidationFailureAlert CreateValidationFailureAlert(int consecutiveFailures)
	{
		return new ControlValidationFailureAlert
		{
			AlertId = Guid.NewGuid(),
			ControlId = "SEC-001",
			ErrorMessage = "Validation failed",
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			ConsecutiveFailures = consecutiveFailures,
			FailedAt = DateTimeOffset.UtcNow
		};
	}

	private static ComplianceStatusChangeNotification CreateStatusChangeNotification(
		bool wasCompliant,
		bool isCompliant,
		string? reason = "Test reason")
	{
		return new ComplianceStatusChangeNotification
		{
			NotificationId = Guid.NewGuid(),
			Criterion = TrustServicesCriterion.CC6_LogicalAccess,
			WasCompliant = wasCompliant,
			IsCompliant = isCompliant,
			Reason = reason,
			ChangedAt = DateTimeOffset.UtcNow
		};
	}

	#endregion
}
