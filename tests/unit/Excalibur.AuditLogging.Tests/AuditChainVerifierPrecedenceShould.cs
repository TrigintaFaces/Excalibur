// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.AuditLogging;
using Excalibur.Compliance;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// A detected violation outranks an empty scope. The two are not competing descriptions of one run.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The verdict was decided by the order of two <c>if</c> statements over a mutable pair,
/// and both could be satisfied at once: a partition carrying no records but a successor whose link fails
/// leaves nothing examined AND records a real violation. The empty test came first, so the detection was
/// discarded and the run reported "nothing to look at".
/// </para>
/// <para>
/// <b>WHY THIS IS A PUBLISHED GUARANTEE AND NOT HYGIENE.</b> The successor pin exists precisely to catch a
/// truncated tail — records deleted from the end of the verified range. That is the one thing this ordering
/// could report as nothing-to-see, so the guarantee was advertised and, on that path, not kept.
/// </para>
/// <para>
/// <b>WHY IT IS STATED AS PRECEDENCE RATHER THAN AS AN ORDERING.</b> "I found a break" is knowledge; "there
/// was nothing to look at" is the absence of knowledge, and an absence may never suppress a finding. The fix
/// derives ONE verdict from the facts instead of returning early twice, which puts the empty answer inside
/// the branch where there is no failure — so no future reordering, however reasonable it looks, can put it
/// back in front of one. This arm names the property so the next reader knows the shape was load-bearing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "AuditLogging")]
public sealed class AuditChainVerifierPrecedenceShould
{
    /// <summary>
    /// Reports the break the caller asks it to report, and examines nothing. Hand-implemented rather than
    /// faked so the arm binds the verifier's reaction to a verification RESULT, not a mock's arrangement.
    /// </summary>
    private sealed class ReportsBreak(AuditChainBreak brokenKind) : IAuditIntegrityStrategy
    {
        public ValueTask<string> ComputeTagAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, CancellationToken cancellationToken) =>
            ValueTask.FromResult("tag");

        public ValueTask<bool> VerifyAsync(ReadOnlyMemory<byte> canonicalContent, string? priorTag, string tag, CancellationToken cancellationToken) =>
            ValueTask.FromResult(brokenKind == AuditChainBreak.None);

        public async ValueTask<AuditChainVerificationResult> VerifyChainAsync(
            IAsyncEnumerable<AuditChainLink> chain,
            string? anchorPriorTag,
            AuditChainLink? successor,
            CancellationToken cancellationToken)
        {
            // Drain the chain so the verifier's cursor sees exactly what a real strategy would: nothing.
            await foreach (var _ in chain.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
            }

            return brokenKind == AuditChainBreak.None
                ? new AuditChainVerificationResult(IsValid: true, FirstBrokenIndex: -1, AuditChainBreak.None)
                : new AuditChainVerificationResult(IsValid: false, FirstBrokenIndex: 0, brokenKind);
        }
    }

    private static AuditEvent Record(string eventId) =>
        new()
        {
            EventId = eventId,
            EventType = AuditEventType.Authentication,
            Action = "Login",
            Outcome = AuditOutcome.Success,
            Timestamp = DateTimeOffset.UtcNow,
            ActorId = "user-1",
        };

    private static Task<AuditIntegrityResult> VerifyAsync(IAuditIntegrityStrategy strategy, AuditChainPartition partition) =>
        AuditChainVerifier.VerifyAsync(
            strategy,
            [partition],
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            isHashChained: true,
            CancellationToken.None);

    /// <summary>
    /// SAFETY. The arm the ruling names: no records examined, a successor whose link is broken. RED against
    /// the previous ordering, which returned <c>NoEventsInScope</c> and dropped the violation.
    /// </summary>
    [Fact]
    public async Task ReportTheViolationWhenNothingWasExaminedButTheSuccessorLinkIsBroken()
    {
        var partition = AuditChainPartition.FromList(
            anchorPriorTag: null,
            events: [],
            successor: Record("evt-successor"));

        var result = await VerifyAsync(new ReportsBreak(AuditChainBreak.SuccessorLinkBroken), partition)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.ViolationsDetected,
            "the successor pin exists to catch records deleted from the END of the range, so reporting that "
            + "detection as an empty scope discards the only evidence of a truncated tail. An absence of "
            + "records examined is the absence of knowledge and must never outrank a finding");
    }

    /// <summary>
    /// LIVENESS. Without this arm the safety arm above is satisfied by a verifier that never reports an empty
    /// scope at all — a genuinely empty window must still be distinguishable from a verified one.
    /// </summary>
    [Fact]
    public async Task StillReportAnEmptyScopeWhenThereIsNoFailureAndNothingToExamine()
    {
        var partition = AuditChainPartition.FromList(
            anchorPriorTag: null,
            events: [],
            successor: null);

        var result = await VerifyAsync(new ReportsBreak(AuditChainBreak.None), partition).ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.NoEventsInScope,
            "an unexpectedly empty window may itself be evidence that records are not reaching the store, so "
            + "it stays distinct from a pass");
    }

    /// <summary>
    /// LIVENESS. An intact trail with records still verifies — the precedence must not turn every run into a
    /// violation.
    /// </summary>
    [Fact]
    public async Task StillVerifyAnIntactTrail()
    {
        var partition = AuditChainPartition.FromList(
            anchorPriorTag: null,
            events: [Record("evt-1"), Record("evt-2")],
            successor: null);

        var result = await VerifyAsync(new ReportsBreak(AuditChainBreak.None), partition).ConfigureAwait(false);

        result.Outcome.ShouldBe(AuditIntegrityOutcome.Verified);
        result.EventsVerified.ShouldBe(2);
    }

    /// <summary>
    /// SAFETY. A break among records that WERE examined is reported as it always was — the reordering must
    /// not have changed the ordinary path on the way to fixing the boundary one.
    /// </summary>
    [Fact]
    public async Task StillReportAViolationAmongRecordsThatWereExamined()
    {
        var partition = AuditChainPartition.FromList(
            anchorPriorTag: null,
            events: [Record("evt-1"), Record("evt-2")],
            successor: null);

        var result = await VerifyAsync(new ReportsBreak(AuditChainBreak.ContentAltered), partition)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(AuditIntegrityOutcome.ViolationsDetected);
    }
}
