using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

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
	public async Task Validate_audit_logging_with_verified_integrity()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(AuditIntegrityResult.Verified(
				100,
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow, isHashChained: true));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeTrue();
		result.EffectivenessScore.ShouldBe(100);
		result.Evidence.ShouldNotBeEmpty();

		// A window that WAS exercised must say so, must say over how many events, and must say what the pass
		// covers: a chained trail is the only case in which deletion, insertion and reordering were tested.
		IntegrityEvidence(result).ShouldBe(
			"Audit log integrity verification: Passed (100 events verified; the trail is hash-chained, so "
			+ "deletion, insertion and reordering were tested)");
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
	public async Task Fail_audit_logging_with_violations_detected()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(AuditIntegrityResult.ViolationsDetected(
				50,
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow,
				"evt-42",
				"Hash chain broken at event 42",
				compromisedChainCount: 2, isHashChained: true));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		result.IsEffective.ShouldBeFalse();
		result.ConfigurationIssues.ShouldContain(i => i.Contains("integrity check failed"));

		// The auditor reads this line, not the field, so it carries the UNIT and the consequence in words:
		// "2 violation(s)" reads as two altered records, and it is two broken chains.
		IntegrityEvidence(result).ShouldBe(
			"Audit log integrity verification: Failed. 2 audit chain(s) compromised across 50 records "
			+ "verified. The earliest altered record is evt-42 (Hash chain broken at event 42). Records "
			+ "following a break within a compromised chain cannot be independently verified, so the number "
			+ "of chains is not the number of altered records.");
	}

	/// <summary>
	/// The regression lock for the three-outcome integrity result. A verification window that contained no
	/// audit events establishes nothing about the hash chain, so the SOC 2 evidence record must report it as
	/// unexercised. Reporting it as a pass would place an assurance in front of an external auditor that
	/// nothing in the system ever earned.
	/// </summary>
	[Fact]
	public async Task Report_an_empty_verification_window_as_not_exercised_rather_than_passed()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(AuditIntegrityResult.NoEventsInScope(
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		var integrityEvidence = IntegrityEvidence(result);

		// Safety: the evidence must not claim the integrity check passed.
		integrityEvidence.ShouldContain("Not exercised", Case.Sensitive);
		integrityEvidence.ShouldNotContain("Passed", Case.Sensitive);
		integrityEvidence.ShouldContain("no evidence of audit log integrity");

		// Liveness: without an arm on the RESULT, the evidence assertions above would also be satisfied
		// by a validator that reported nothing at all. That concern was right and is kept.
		//
		// What this arm used to require was IsEffective == true, on the reasoning that an unexercised
		// window is not a control failure. The first half is correct and the conclusion does not follow:
		// "not a failure" is not "a pass". The evidence said the period provides no evidence of
		// integrity while the verdict said the control was effective at full score, and an auditor reads
		// the verdict.
		result.ControlId.ShouldBe("SEC-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.ConfigurationIssues.ShouldContain(i => i.Contains("not verified", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Handle_integrity_check_exception_gracefully()
	{
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Database unavailable"));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		// The exception is caught, and the logger being configured is genuinely established -- so
		// IsConfigured stays true. What the check did NOT establish is that the trail is intact: it
		// threw. This used to assert the control was effective, which reported a full pass for a
		// verification that failed to run.
		result.ControlId.ShouldBe("SEC-004");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_security_monitoring_with_audit_store()
	{
		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);

		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-005");
		// The mechanism is present and the result still says so -- IsConfigured stays true. What it no
		// longer says is that the CONTROL operated, because nothing here observed it operating.
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
		result.EffectivenessScore.ShouldBeInRange(1, 99);
		result.Evidence.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Validate_security_monitoring_with_logger_only()
	{
		var sut = new AuditLogControlValidator(_auditLogger, auditStore: null);

		var result = await sut.ValidateAsync("SEC-005", CancellationToken.None).ConfigureAwait(false);

		// This comment read "Still passes -- logger alone provides monitoring capability", which is the
		// substitution in one line: a monitoring CAPABILITY is not monitoring having been performed.
		result.ControlId.ShouldBe("SEC-005");
		result.IsConfigured.ShouldBeTrue();
		result.IsEffective.ShouldBeFalse();
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
			.Returns(AuditIntegrityResult.Verified(
				50,
				DateTimeOffset.UtcNow.AddDays(-1),
				DateTimeOffset.UtcNow, isHashChained: true));

		var sut = new AuditLogControlValidator(_auditLogger, _auditStore);
		var parameters = new ControlTestParameters { SampleSize = 10 };

		var result = await sut.RunTestAsync("SEC-004", parameters, CancellationToken.None).ConfigureAwait(false);

		result.ControlId.ShouldBe("SEC-004");
		// The verdict, not NotTested: a validator ran. What did not happen is SAMPLING, which the
		// item count and the null finding count below carry.
		result.Outcome.ShouldBe(TestOutcome.NotTested);
		// Zero, not the requested 10: RunTestAsync forwards a verdict and samples nothing. The
		// requested size stays available on Parameters, where it is a request rather than a measurement.
		result.ItemsTested.ShouldBe(0);
		// Null, not 0: no search ran, so there is no finding count. Zero would assert we looked.
		result.ExceptionsFound.ShouldBeNull();
	}

	/// <summary>
	/// Returns the description of the single evidence item recording the integrity verification outcome.
	/// </summary>
	private static string IntegrityEvidence(ControlValidationResult result) =>
		result.Evidence
			.Single(e => e.Description.StartsWith(
				"Audit log integrity verification:", StringComparison.Ordinal))
			.Description;
}
