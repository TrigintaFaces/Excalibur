// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The claim fence: a superseded writer may not advance the attestation, and an unsuperseded one must
/// still be able to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these arms exist, and what they catch that nothing else does.</b> Taking an erasure for
/// execution is already an atomic claim — exactly one caller of N moves the request out of scheduled.
/// What the claim did not carry was WHO holds it, so three writes that advance the attestation were
/// unconditional on ownership: the terminal status, the completion, and the certificate reference the
/// completion stamps. Under the lease that matters, because a lapsed claim is RE-OFFERED: two executors
/// then run one erasure and each writes its own signed completion over coverage it alone established.
/// The terminal state is a full-coverage certificate sitting beside a partial status, which is the
/// artifact a regulated consumer hands to an auditor.
/// </para>
/// <para>
/// <b>THE FIXTURE STATE IS THE WHOLE POINT.</b> A fresh request per arm only ever produces "nothing is
/// claimed", and in that state the fenced code and the unfenced code agree — so a suite built on fresh
/// requests is green against both and blind to the defect. Every arm below constructs a row that EXISTS
/// carrying a claim, and the lever is <c>claimDuration: TimeSpan.Zero</c>, which the contract admits for
/// exactly this reason: it is the only way a provider-agnostic test can produce an already-lapsed claim
/// without controlling a clock.
/// </para>
/// <para>
/// <b>Scope, stated so nobody inherits it wider.</b> These drive the in-memory store, so they bind the
/// CONTRACT every provider owes rather than any one provider's SQL. The equivalent assertions against
/// real PostgreSQL and SQL Server belong in the store conformance kit, where a server is available; they
/// are not exercised here and this file does not establish them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TheClaimFenceRefusesASupersededWriterShould
{
	private static InMemoryErasureStore NewStore() =>
		new(TestDataSubjectHasher.Instance,
			UntenantedContext.Instance,
			Options.Create(new TenantContextOptions()),
			TimeProvider.System);

	/// <summary>
	/// SAFETY, and the arm the fence exists for. RED when the identity term is dropped from the
	/// completion write: the superseded run's completion lands and stamps a certificate over coverage a
	/// different run established.
	/// </summary>
	[Fact]
	public async Task RefuseACompletionPresentingASupersededClaim()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		// Granted already-lapsed, which is what lets a second claim supersede it with no clock to wait on.
		var first = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false);
		first.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		// A re-claim from the claimed status: this is the recovery path, and it is what revokes.
		var second = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.InProgress, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		second.Outcome.ShouldBe(ErasureClaimOutcome.Granted);
		second.ClaimIdentity.ShouldNotBe(
			first.ClaimIdentity,
			"a re-claim must mint a FRESH identity; reusing one leaves the fence structurally present and "
			+ "semantically inert, and this is the only assertion here that catches that");

		var refused = await store
			.RecordCompletionAsync(requestId, 7, 70, Guid.NewGuid(), first.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		refused.ShouldBe(
			ErasureWriteOutcome.ClaimLost,
			"the first run was superseded, so its completion must be refused rather than overwrite the "
			+ "attestation of the run that now owns the request");

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldNotBe(ErasureRequestStatus.Completed);
		status.CertificateId.ShouldBeNull(
			"the certificate is the artifact a data subject and a regulator are shown; a superseded run "
			+ "must not attach one");
	}

	/// <summary>
	/// LIVENESS for the arm above. A fence that refused EVERY completion would satisfy it while being
	/// entirely inert, so this pins that the holder of the current claim still completes.
	/// </summary>
	[Fact]
	public async Task ApplyACompletionPresentingTheCurrentClaim()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var claim = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		claim.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var applied = await store
			.RecordCompletionAsync(requestId, 7, 70, Guid.NewGuid(), claim.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		applied.ShouldBe(ErasureWriteOutcome.Applied);
		(await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false))!
			.Status.ShouldBe(ErasureRequestStatus.Completed);
	}

	/// <summary>
	/// A LAPSE DOES NOT REVOKE; A RE-CLAIM REVOKES. RED if the expiry is tested in the fenced write: an
	/// executor whose run outlasted its own lease, but whom nobody superseded, would be refused its own
	/// completion — keys destroyed, erasure performed, and no certificate able to attest it.
	/// </summary>
	[Fact]
	public async Task StillApplyACompletionFromAHolderWhoseLeaseLapsedButWhomNobodySuperseded()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var claim = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false);
		claim.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		// Nobody re-claimed. The lease is gone and the identity the row carries is still ours.
		var applied = await store
			.RecordCompletionAsync(requestId, 1, 1, Guid.NewGuid(), claim.ClaimIdentity!, CancellationToken.None)
			.ConfigureAwait(false);

		applied.ShouldBe(
			ErasureWriteOutcome.Applied,
			"a lapse alone must not revoke: only another claimant actually taking the request does, or an "
			+ "erasure that already destroyed a subject's keys could never issue the certificate for it");
	}

	/// <summary>
	/// A second contender is told ALREADY CLAIMED, never that the status is wrong. The two oblige
	/// different things: a claimed request returns on its own when the claim lapses, and a request in the
	/// wrong status does not — so answering the latter makes a caller stop offering a request that IS
	/// coming back, which is the stranding this whole mechanism exists to remove.
	/// </summary>
	[Fact]
	public async Task TellASecondContenderTheRequestIsClaimedRatherThanInTheWrongStatus()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		// Granting advanced the status, so a status test taken FIRST would answer StatusMismatch here.
		var second = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		second.Outcome.ShouldBe(
			ErasureClaimOutcome.AlreadyClaimed,
			"custody must be tested before status, or a contender is told to stop offering a request that "
			+ "returns the moment the holder's claim lapses");
	}

	/// <summary>
	/// RECOVERY HAPPENS THROUGH THE CLAIM, NOT THROUGH A STATUS RESET, and this arm exists because the
	/// obvious alternative is wrong in a way that compiles.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A recovered request STAYS in the claimed status, carrying no live claim, and is re-offered by the
	/// lapse predicate — so the member that recovers it is the claim, taking it from the claimed status.
	/// Resetting it to scheduled is NOT the recovery path and the store refuses that write, which is
	/// correct and predates this change: scheduled is the state the claim succeeds from, so writing it
	/// over a request somebody may be executing re-offers work that is already running. That refusal is
	/// asserted by its own arm elsewhere in this suite.
	/// </para>
	/// <para>
	/// RED if the expiry comparison in the claim is dropped or inverted: a lapsed request would then
	/// either never be recoverable at all, or be claimable while a live holder is still working.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task LetARecovererTakeARequestWhoseClaimHasLapsed()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var abandoned = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false);
		abandoned.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		// The holder died here. Nothing reset the status, and nothing needs to.
		var recovered = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.InProgress, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		recovered.Outcome.ShouldBe(
			ErasureClaimOutcome.Granted,
			"an execution interrupted by a host shutdown must become claimable again once its lease "
			+ "elapses; before this, every query correctly excluded it and nothing ever offered it again");
		recovered.ClaimIdentity.ShouldNotBe(abandoned.ClaimIdentity);
	}

	/// <summary>
	/// The safety half of recovery: while the lease is LIVE, a recoverer is refused. Without this a
	/// recovery pass would take requests out from under instances that are still erasing.
	/// </summary>
	[Fact]
	public async Task RefuseARecovererWhileTheLeaseIsStillLive()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var refused = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.InProgress, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		refused.Outcome.ShouldBe(ErasureClaimOutcome.AlreadyClaimed);
	}

	/// <summary>
	/// The safety half of the arm above: while a claim is LIVE, a supervisor's no-claim write is refused.
	/// Without this, "release the retry budget and stand down" is never reachable and a supervisor could
	/// re-offer a request another instance is actively erasing.
	/// </summary>
	[Fact]
	public async Task RefuseASupervisorsReOfferWhileTheClaimIsStillLive()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var refused = await store
			.UpdateStatusAsync(requestId, ErasureRequestStatus.Scheduled, expectedStatus: null, errorMessage: null, claimIdentity: null, CancellationToken.None)
			.ConfigureAwait(false);

		refused.ShouldBe(ErasureWriteOutcome.ClaimLost);
	}

	/// <summary>
	/// Taking custody is expressible through EXACTLY ONE member. RED if the general status write is ever
	/// allowed to produce the claimed status: custody would then be grantable to a caller that has no
	/// identity to be given, and every fenced write it made would compare against nothing.
	/// </summary>
	[Fact]
	public async Task RefuseToTakeCustodyThroughTheGeneralStatusWrite()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.UpdateStatusAsync(
				requestId, ErasureRequestStatus.InProgress, expectedStatus: null, errorMessage: null,
				claimIdentity: null, CancellationToken.None)).ConfigureAwait(false);
	}

	/// <summary>
	/// RECOVERY IS ONLY REAL IF SOMETHING OFFERS THE REQUEST AGAIN. The claim being takeable is half of
	/// it; this arm binds the other half, because a lapsed request that no query returns is stranded just
	/// as completely as one with no lease at all.
	/// </summary>
	/// <remarks>
	/// RED if the lapse disjunct is dropped from the scheduled-request query: the request is claimable in
	/// principle and nothing ever hands it to anybody.
	/// </remarks>
	[Fact]
	public async Task OfferARequestWhoseExecutionClaimHasLapsed()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.Zero, CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var offered = await store.GetScheduledRequestsAsync(10, CancellationToken.None).ConfigureAwait(false);

		offered.Select(r => r.RequestId).ShouldContain(
			requestId,
			"an execution interrupted by a host shutdown must be offered again once its lease elapses; "
			+ "before this the row sat in the claimed status and every query correctly excluded it");
	}

	/// <summary>
	/// The safety half: a request under a LIVE claim is not offered, so a poll does not hand out work
	/// another instance is actively doing. A query that returned everything would satisfy the arm above
	/// while being useless.
	/// </summary>
	[Fact]
	public async Task NotOfferARequestWhoseExecutionClaimIsStillLive()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		var offered = await store.GetScheduledRequestsAsync(10, CancellationToken.None).ConfigureAwait(false);

		offered.Select(r => r.RequestId).ShouldNotContain(requestId);
	}

	/// <summary>
	/// The confirmation pass's discovery obeys the same rule, and it has to: this status is the only one
	/// that pass claims from, so offering a live-claimed request makes the loser report a defect on every
	/// poll while nothing is actually wrong.
	/// </summary>
	[Fact]
	public async Task NotOfferAConfirmationAnotherConfirmerAlreadyHolds()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		var executing = await store
			.ClaimForExecutionAsync(requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);
		executing.Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		// Handing off to the confirmation stage releases the executor's custody, which is what lets a
		// confirmer claim it at all.
		(await store.UpdateStatusAsync(requestId, ErasureRequestStatus.AwaitingKeyDestruction, expectedStatus: null, errorMessage: null, executing.ClaimIdentity, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBe(ErasureWriteOutcome.Applied);

		(await GetBackground(store).GetAwaitingKeyDestructionRequestsAsync(1, 10, CancellationToken.None)
			.ConfigureAwait(false)).Select(r => r.RequestId).ShouldContain(
			requestId, "a handed-off request carries no claim, so a confirmer must be offered it");

		(await store.ClaimForExecutionAsync(requestId, ErasureRequestStatus.AwaitingKeyDestruction, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false)).Outcome.ShouldBe(ErasureClaimOutcome.Granted);

		(await GetBackground(store).GetAwaitingKeyDestructionRequestsAsync(1, 10, CancellationToken.None)
			.ConfigureAwait(false)).Select(r => r.RequestId).ShouldNotContain(
			requestId, "a confirmer holds it now, so a second pass must not be handed the same request");
	}

	/// <summary>
	/// GRANT CUSTODY TO EXACTLY ONE OF N CONTENDERS THAT ARRIVE TOGETHER, and give that one an identity
	/// nobody else holds.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other arm in this suite drives the contenders in SEQUENCE, which measures the decision but
	/// not the race. The store's own guarantee is that the status test, the status write, the identity and
	/// the expiry become visible TOGETHER, and a sequential arm cannot distinguish a store that holds that
	/// from one that reads, yields, and writes.
	/// </para>
	/// <para>
	/// BOTH HALVES ARE ASSERTED, because each alone is satisfiable by a broken store. Exactly-one is the
	/// safety half and a store that refuses every claim satisfies it. At-least-one is the liveness half
	/// and a store that grants every claim satisfies it. The identities being distinct is the third: a
	/// store that grants once but derives the identity from the request id would pass both counts while
	/// handing a superseded caller an identity that still compares equal, which leaves the fence
	/// structurally present and semantically inert.
	/// </para>
	/// <para>
	/// This binds the IN-MEMORY store only, and it is deliberately outside the shared conformance kit: the
	/// kit's arms run against real engines where a racing arm would be timing-dependent and flaky. The
	/// in-memory store serialises custody with a per-request lock, which is STRONGER than the contract
	/// requires -- so a green here is evidence about this store and not about the contract. The SQL
	/// providers get the property from a single conditional UPDATE instead, and that is bound by the
	/// kit's exclusivity arm.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task GrantCustodyToExactlyOneOfManySimultaneousContenders()
	{
		var store = NewStore();
		var requestId = await ScheduleAsync(store).ConfigureAwait(false);

		const int Contenders = 32;

		// Released together, so the contenders are inside the claim at the same time rather than queued
		// behind each other's awaits. Without the barrier this is 32 sequential claims wearing a
		// Task.WhenAll, which is the shape that makes a racing arm look green and measure nothing.
		using var start = new SemaphoreSlim(0, Contenders);

		var attempts = Enumerable.Range(0, Contenders).Select(async _ =>
		{
			await start.WaitAsync(CancellationToken.None).ConfigureAwait(false);
			return await store.ClaimForExecutionAsync(
					requestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
				.ConfigureAwait(false);
		}).ToArray();

		start.Release(Contenders);

		var results = await Task.WhenAll(attempts).ConfigureAwait(false);

		var granted = results.Where(r => r.Outcome == ErasureClaimOutcome.Granted).ToArray();

		granted.Length.ShouldBe(
			1,
			"exactly one of " + Contenders + " simultaneous contenders may be granted custody; every "
			+ "additional grant is another instance that erases the same subject and signs its own "
			+ "completion certificate over coverage it did not establish");

		granted[0].ClaimIdentity.ShouldNotBeNullOrEmpty(
			"the winner must receive the identity it will present at every later attestation write");

		results.Where(r => r.Outcome != ErasureClaimOutcome.Granted)
			.ShouldAllBe(
				r => r.ClaimIdentity == null,
				"a refused contender must receive no identity; one handed out here would be accepted by "
				+ "the fence as proof of custody");

		// The losers are told the request is HELD, never that its status moved on. A status mismatch
		// obliges the caller to stop offering a request that is coming back when this lease elapses.
		results.Where(r => r.Outcome != ErasureClaimOutcome.Granted)
			.ShouldAllBe(
				r => r.Outcome == ErasureClaimOutcome.AlreadyClaimed,
				"a contender that lost the race must be refused as AlreadyClaimed, because granting the "
				+ "claim advances the status and a status-first test would answer StatusMismatch -- which "
				+ "tells the loser to abandon a request that is still live");
	}

	private static IErasureBackgroundQueryStore GetBackground(InMemoryErasureStore store) => store;

	private static async Task<Guid> ScheduleAsync(IErasureStore store)
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "subject-under-fence",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "compliance-admin",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddSeconds(-1), CancellationToken.None)
			.ConfigureAwait(false);

		return request.RequestId;
	}
}
