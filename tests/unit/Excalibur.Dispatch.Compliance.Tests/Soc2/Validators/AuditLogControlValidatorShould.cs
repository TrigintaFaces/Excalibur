using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Soc2.Validators;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditLogControlValidatorShould
{
	private readonly IAuditLogger _auditLogger = A.Fake<IAuditLogger>();
	private readonly IAuditStore _auditStore = A.Fake<IAuditStore>();

	[Fact]
	public void Return_two_supported_controls()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		sut.SupportedControls.Count.ShouldBe(2);
		sut.SupportedControls.ShouldContain("SEC-004");
		sut.SupportedControls.ShouldContain("SEC-005");
	}

	[Fact]
	public void Return_supported_criteria()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.CC1_ControlEnvironment);
		sut.SupportedCriteria.ShouldContain(TrustServicesCriterion.CC4_Monitoring);
	}

	[Fact]
	public async Task Validate_audit_logging_with_valid_integrity()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(new AuditIntegrityResult
			{
				IsValid = true,
				EventsVerified = 100,
				StartDate = DateTimeOffset.UtcNow.AddDays(-1),
				EndDate = DateTimeOffset.UtcNow,
				VerifiedAt = DateTimeOffset.UtcNow
			});

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Fail_audit_logging_without_logger()
	{
		var sut = new AuditLogControlValidator(auditLogger: null, auditStore: null);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not configured"));
	}

	[Fact]
	public async Task Fail_audit_logging_with_invalid_integrity()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(new AuditIntegrityResult
			{
				IsValid = false,
				EventsVerified = 50,
				StartDate = DateTimeOffset.UtcNow.AddDays(-1),
				EndDate = DateTimeOffset.UtcNow,
				VerifiedAt = DateTimeOffset.UtcNow,
				ViolationDescription = "Hash chain broken at event 42"
			});

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("integrity check failed"));
	}

	[Fact]
	public async Task Handle_integrity_check_exception_gracefully()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database unavailable"));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		// Exception is caught — still passes because logger is configured
		result.ControlId.ShouldBe("SEC-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_security_monitoring_with_audit_store()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-005");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_security_monitoring_with_logger_only()
	{
		var sut = new AuditLogControlValidator(_auditLogger, auditStore: null);

		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None).ConfigureAwait(false);

		// Still passes — logger alone provides monitoring capability
		result.ControlId.ShouldBe("SEC-005");
		result.IsEffective.ShouldBeTrue();
	}

	[Fact]
	public async Task Fail_security_monitoring_without_any_infrastructure()
	{
		var sut = new AuditLogControlValidator(auditLogger: null, auditStore: null);

		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-005");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("No audit infrastructure"));
	}

	[Fact]
	public async Task Return_failure_for_unknown_control()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("UNKNOWN", CancellationToken.None).ConfigureAwait(false);

		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("Unknown control"));
	}

	[Fact]
	public void Return_control_description_for_sec_004()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var description = sut.GetControlDescription("SEC-004");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-004");
		description.Name.ShouldBe("Audit Logging");
		description.Type.ShouldBe(ControlType.Detective);
		description.Frequency.ShouldBe(ControlFrequency.Continuous);
	}

	[Fact]
	public void Return_control_description_for_sec_005()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var description = sut.GetControlDescription("SEC-005");

		description.ShouldNotBeNull();
		description.ControlId.ShouldBe("SEC-005");
		description.Name.ShouldBe("Security Monitoring");
	}

	[Fact]
	public void Return_null_description_for_unknown_control()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var description = sut.GetControlDescription("UNKNOWN");

		description.ShouldBeNull();
	}

	[Fact]
	public async Task Run_test_delegates_to_validation()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(new AuditIntegrityResult
			{
				IsValid = true,
				EventsVerified = 50,
				StartDate = DateTimeOffset.UtcNow.AddDays(-1),
				EndDate = DateTimeOffset.UtcNow,
				VerifiedAt = DateTimeOffset.UtcNow
			});

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("SEC-004", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.Outcome.ShouldBe(TestOutcome.NoExceptions);
		result.ItemsTested.ShouldBe(10);
		result.ExceptionsFound.ShouldBe(0);
	}
}
