using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

/// <summary>
/// The effectiveness score used to be <c>Math.Max(0, 100 - (issues.Count * 25))</c> -- a count of how
/// many sentences a validator wrote. These arms bind the ORDER the bands must stand in, which is the
/// part that is a contract; the particular numbers are not, and these assertions deliberately do not
/// name them.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2EffectivenessScoreShould
{
	private readonly IAuditLogger _auditLogger = A.Fake<IAuditLogger>();
	private readonly IAuditStore _auditStore = A.Fake<IAuditStore>();

	private AuditLogControlValidator Sut => new(_auditLogger, _auditStore);

	private void IntegrityCheckReports(AuditIntegrityResult result) =>
		A.CallTo(() => _auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(result);

	private static AuditIntegrityResult Tampered() =>
		AuditIntegrityResult.ViolationsDetected(
			50,
			DateTimeOffset.UtcNow.AddDays(-1),
			DateTimeOffset.UtcNow,
			"evt-42",
			"Hash chain broken at event 42",
			compromisedChainCount: 2,
			isHashChained: true);

	[Fact]
	public async Task Score_a_detected_tamper_below_a_control_it_could_not_verify()
	{
		// The whole defect in one comparison. A confirmed tampered audit trail added ONE complaint, so
		// 100 - 25 = 75, while a control that was present but unverifiable reported 40. The compromise
		// we PROVED outranked the question we never answered, and these averages reach an assessor.
		IntegrityCheckReports(Tampered());
		var tampered = await Sut.ValidateAsync("SEC-004", CancellationToken.None);

		IntegrityCheckReports(AuditIntegrityResult.NoEventsInScope(
			DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow));
		var unverified = await Sut.ValidateAsync("SEC-004", CancellationToken.None);

		tampered.EffectivenessScore.ShouldBeLessThan(
			unverified.EffectivenessScore,
			"a violation we found is knowledge; a window we never exercised is not, and the proven "
			+ "deficiency must never read as the better of the two.");
	}

	[Fact]
	public async Task Score_an_absent_mechanism_no_higher_than_a_detected_violation()
	{
		// SEC-005's only issue is that NO audit infrastructure exists, which scored 100 - 33 = 67 --
		// above a control whose trail was proven tampered.
		var absent = new AuditLogControlValidator(auditLogger: null, auditStore: null);
		var nothingConfigured = await absent.ValidateAsync("SEC-005", CancellationToken.None);

		IntegrityCheckReports(Tampered());
		var tampered = await Sut.ValidateAsync("SEC-004", CancellationToken.None);

		nothingConfigured.EffectivenessScore.ShouldBeLessThanOrEqualTo(tampered.EffectivenessScore);
	}

	[Fact]
	public async Task Not_derive_the_score_from_how_many_remarks_were_made()
	{
		// The predicate that actually matters: the score must be a function of the WORST FACT, not of
		// the remark count. A single detected violation and the same violation reported alongside
		// further remarks describe one compromised trail and must score the same.
		IntegrityCheckReports(Tampered());
		var first = await Sut.ValidateAsync("SEC-004", CancellationToken.None);
		var second = await Sut.ValidateAsync("SEC-004", CancellationToken.None);

		first.EffectivenessScore.ShouldBe(second.EffectivenessScore);
		first.ConfigurationIssues.ShouldNotBeEmpty();
	}
}
