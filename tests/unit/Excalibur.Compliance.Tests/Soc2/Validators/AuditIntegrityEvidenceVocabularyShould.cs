// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

namespace Excalibur.Compliance.Tests.Soc2.Validators;

/// <summary>
/// Binds the vocabulary of the generated compliance evidence. This is the line a consumer hands to an
/// external auditor, so it must name the unit the reported quantity is in and say what the verification did
/// not establish. A store that does not hash-chain must not be reported through the chained vocabulary at
/// all: "no compromised chains" would read as evidence against deletion, insertion and reordering, none of
/// which an unchained trail can test.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditIntegrityEvidenceVocabularyShould
{
	private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset End = Start.AddDays(1);

	// SAFETY (unit stability): the same trail with the same tampering renders in two different vocabularies
	// depending on whether the store chained, so an auditor is never handed the same words for two different
	// units. RED against the previous sentence, which said "N violation(s)" in both cases.
	[Fact]
	public async Task ReportAnUnchainedTrailInItsOwnVocabulary_NotThroughTheChainedOne()
	{
		var chained = await EvidenceForAsync(AuditIntegrityResult.ViolationsDetected(
			3, Start, End, "evt-2", "Hash chain broken at event 2",
			compromisedChainCount: 1, isHashChained: true));

		var unchained = await EvidenceForAsync(AuditIntegrityResult.ViolationsDetected(
			3, Start, End, "evt-2", "Content altered at event 2",
			compromisedChainCount: 2, isHashChained: false));

		chained.ShouldContain("chain(s) compromised");
		unchained.ShouldNotContain("chain(s) compromised", Case.Sensitive);
		unchained.ShouldContain("record(s) failed content verification");
		unchained.ShouldContain("NOT hash-chained");
		unchained.ShouldContain("deletion, insertion and reordering were not tested");
	}

	// SAFETY: an unchained PASS is the more dangerous half — nothing failed, so the line reads as assurance.
	// It must state what it did not establish, the way an unexercised window already does.
	[Fact]
	public async Task RefuseToPresentAnUnchainedPassAsTamperEvidence()
	{
		var evidence = await EvidenceForAsync(
			AuditIntegrityResult.Verified(3, Start, End, isHashChained: false));

		evidence.ShouldNotContain("Passed", Case.Sensitive);
		evidence.ShouldContain("NOT hash-chained");
		evidence.ShouldContain("no evidence against them");
	}

	// LIVENESS: a chained pass is still reported as a pass, and says what it covered. Without this arm the
	// safety arms above would be satisfied by a validator that never reported a pass at all.
	[Fact]
	public async Task StillReportAChainedPassAsAPass()
	{
		var evidence = await EvidenceForAsync(
			AuditIntegrityResult.Verified(3, Start, End, isHashChained: true));

		evidence.ShouldContain("Passed");
		evidence.ShouldContain("deletion, insertion and reordering were tested");
	}

	// AUDITOR LEGIBILITY (structural): the failure line names the record, so the reader can act on it without
	// reading the result object. FirstViolationEventId and ViolationDescription already carry both.
	[Fact]
	public async Task NameTheEarliestAlteredRecordInTheEvidenceLine()
	{
		var evidence = await EvidenceForAsync(AuditIntegrityResult.ViolationsDetected(
			9, Start, End, "evt-42", "Hash chain broken at event 42",
			compromisedChainCount: 2, isHashChained: true));

		evidence.ShouldContain("evt-42");
		evidence.ShouldContain("Hash chain broken at event 42");
		evidence.ShouldContain("2 audit chain(s) compromised");
	}

	private static async Task<string> EvidenceForAsync(AuditIntegrityResult integrityResult)
	{
		var auditLogger = A.Fake<IAuditLogger>();
		A.CallTo(() => auditLogger.VerifyIntegrityAsync(
				A<DateTimeOffset>._, A<DateTimeOffset>._, A<CancellationToken>._))
			.Returns(integrityResult);

		var sut = new AuditLogControlValidator(auditLogger, A.Fake<IAuditStore>());
		var result = await sut.ValidateAsync("SEC-004", CancellationToken.None).ConfigureAwait(false);

		return result.Evidence
			.Select(e => e.Description)
			.First(d => d.StartsWith("Audit log integrity verification:", StringComparison.Ordinal));
	}
}
