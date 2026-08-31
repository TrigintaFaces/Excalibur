// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;
using Excalibur.Compliance;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Binds what the reported quantity is IN. A partition is the store's write-time chaining unit, so the same
/// field counts chains when the store chains and records when it does not; the result must carry which, and
/// the count must track the number of compromised units rather than being pinned at one.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "AuditLogging")]
public sealed class AuditChainVerifierUnitReportingShould
{
	private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset End = Start.AddDays(1);

	// LIVENESS: two compromised partitions report 2. Without this arm an implementation that always reported
	// 1 would satisfy every other assertion, which is exactly how a count pinned at one survives a suite.
	[Fact]
	public async Task CountEveryCompromisedPartition_NotJustTheFirst()
	{
		var result = await AuditChainVerifier.VerifyAsync(
			AlwaysBroken(),
			[Partition("chain-a-first"), Partition("chain-b-first")],
			Start,
			End,
			isHashChained: true,
			CancellationToken.None);

		result.Outcome.ShouldBe(AuditIntegrityOutcome.ViolationsDetected);
		result.CompromisedChainCount.ShouldBe(2, "each compromised chaining unit is counted");
	}

	// SAFETY (unit stability): the SAME trail with the SAME tampering reports a different quantity depending
	// on whether the store chains, so the result must carry which unit the quantity is in. Two altered
	// records inside one chain are one compromised chain; the same two records in an unchained store are two
	// compromised records. RED before the result carried the unit at all.
	[Fact]
	public async Task CarryTheUnitTheQuantityIsIn_BecauseItChangesWithTheStoresConfiguration()
	{
		var chained = await AuditChainVerifier.VerifyAsync(
			AlwaysBroken(),
			[Partition("record-1", "record-2")],
			Start,
			End,
			isHashChained: true,
			CancellationToken.None);

		var unchained = await AuditChainVerifier.VerifyAsync(
			AlwaysBroken(),
			[Partition("record-1"), Partition("record-2")],
			Start,
			End,
			isHashChained: false,
			CancellationToken.None);

		chained.IsHashChained.ShouldBeTrue();
		unchained.IsHashChained.ShouldBeFalse();

		chained.CompromisedChainCount.ShouldBe(1, "two altered records in one chain are one compromised chain");
		unchained.CompromisedChainCount.ShouldBe(2, "an unchained store makes each record its own unit");
	}

	private static AuditChainPartition Partition(params string[] eventIds) =>
		AuditChainPartition.FromList(
			anchorPriorTag: null,
			events: Array.ConvertAll(eventIds, Event),
			successor: null);

	private static AuditEvent Event(string eventId) =>
		new()
		{
			EventId = eventId,
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = Start,
			ActorId = "user-1",
		};

	private static IAuditIntegrityStrategy AlwaysBroken()
	{
		var strategy = A.Fake<IAuditIntegrityStrategy>();
		A.CallTo(() => strategy.VerifyChainAsync(
				A<IAsyncEnumerable<AuditChainLink>>._,
				A<string?>._,
				A<AuditChainLink?>._,
				A<CancellationToken>._))
			.ReturnsLazily(call => DrainThenReportBrokenAsync((IAsyncEnumerable<AuditChainLink>)call.Arguments[0]!));
		return strategy;
	}

	// The verifier names the broken record from the cursor it feeds the strategy, so a fake that never
	// enumerates would report an unnamed record and the arms would prove nothing about which record broke.
	private static async ValueTask<AuditChainVerificationResult> DrainThenReportBrokenAsync(
		IAsyncEnumerable<AuditChainLink> chain)
	{
		await foreach (var _ in chain.ConfigureAwait(false))
		{
		}

		return new AuditChainVerificationResult(IsValid: false, FirstBrokenIndex: 0, AuditChainBreak.ContentAltered);
	}
}
