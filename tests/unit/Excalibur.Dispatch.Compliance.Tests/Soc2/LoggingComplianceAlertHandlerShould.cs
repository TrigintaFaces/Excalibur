using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class LoggingComplianceAlertHandlerShould
{
	private readonly NullLogger<LoggingComplianceAlertHandler> _logger = NullLogger<LoggingComplianceAlertHandler>.Instance;

	[Fact]
	public async Task Handle_critical_gap_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = CreateAlert(GapSeverity.Critical);

		await sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Handle_high_gap_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = CreateAlert(GapSeverity.High);

		await sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Handle_medium_gap_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = CreateAlert(GapSeverity.Medium);

		await sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Handle_low_gap_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = CreateAlert(GapSeverity.Low);

		await sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Handle_recurring_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = new ComplianceGapAlert
		{
			AlertId = Guid.NewGuid(),
			Gap = new ComplianceGap
			{
				GapId = "gap-1",
				Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
				Description = "Recurring gap",
				Severity = GapSeverity.High,
				Remediation = "Apply remediation steps",
				IdentifiedAt = DateTimeOffset.UtcNow
			},
			GeneratedAt = DateTimeOffset.UtcNow,
			IsRecurring = true,
			OccurrenceCount = 5
		};

		await sut.HandleComplianceGapAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_gap_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.HandleComplianceGapAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new LoggingComplianceAlertHandler(null!));
	}

	[Fact]
	public async Task Handle_status_change_notification()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var notification = new ComplianceStatusChangeNotification
		{
			NotificationId = Guid.NewGuid(),
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			WasCompliant = true,
			IsCompliant = false,
			ChangedAt = DateTimeOffset.UtcNow,
			Reason = "Control became ineffective"
		};

		await sut.HandleStatusChangeAsync(notification, CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task Handle_validation_failure_alert()
	{
		var sut = new LoggingComplianceAlertHandler(_logger);
		var alert = new ControlValidationFailureAlert
		{
			AlertId = Guid.NewGuid(),
			ControlId = "ctrl-1",
			Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
			ErrorMessage = "Validation failed",
			FailedAt = DateTimeOffset.UtcNow,
			ConsecutiveFailures = 3
		};

		await sut.HandleValidationFailureAsync(alert, CancellationToken.None).ConfigureAwait(false);
	}

	private static ComplianceGapAlert CreateAlert(GapSeverity severity) =>
		new()
		{
			AlertId = Guid.NewGuid(),
			Gap = new ComplianceGap
			{
				GapId = "gap-1",
				Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
				Description = "Test gap",
				Severity = severity,
				Remediation = "Apply remediation steps",
				IdentifiedAt = DateTimeOffset.UtcNow
			},
			GeneratedAt = DateTimeOffset.UtcNow,
			IsRecurring = false,
			OccurrenceCount = 1
		};
}
