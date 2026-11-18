// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the liveness half of Article 17(3): a legal hold DEFERS an erasure, so when the hold stops
/// blocking, something must return the request to the queue.
/// </summary>
/// <remarks>
/// <para>
/// <b>Before this pass existed the deferral was permanent.</b> A deferred request is not finished and the
/// controller's obligation to erase is not discharged, but nothing re-examined the hold — so releasing a
/// hold, or letting one lapse, left the request deferred forever with no signal to anyone. The erasure
/// was silently never done, which is the failure mode this whole subsystem exists to prevent.
/// </para>
/// <para>
/// <b>The pass grants ELIGIBILITY, never permission to erase.</b> It writes the scheduled status and
/// nothing else. That is why the safety arms below assert the STATUS rather than whether anything was
/// destroyed: the executor re-checks holds after it claims the request and again before each irreversible
/// act, so this pass cannot cause an erasure of held data even if it resumed wrongly. What it CAN cause,
/// if it resumed wrongly, is a claim cycle per poll forever, which is a liveness cost.
/// </para>
/// <para>
/// <b>Scope, stated so nobody inherits it wider.</b> These arms drive the in-memory store, so they bind
/// the CONTRACT every provider owes. The estate-wide discovery query each provider implements is NOT
/// exercised here — it needs a real server, and its home is the store conformance kit, where one
/// parameterised arm binds both background members across all three stores.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ADeferredErasureIsReturnedToTheQueueShould
{
	private static readonly LegalHoldInfo Hold = new()
	{
		HoldId = Guid.NewGuid(),
		Basis = LegalHoldBasis.LitigationHold,
		CaseReference = "CASE-1",
		CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
	};

	// LIVENESS, and the arm this pass exists for. RED before the pass existed: nothing re-examined the
	// hold, so the request stayed BlockedByLegalHold forever. RED again if the resume write is removed or
	// pointed at any status other than Scheduled.
	[Fact]
	public async Task Return_a_request_to_the_queue_once_no_hold_blocks_it()
	{
		var store = NewStore();
		var requestId = await DeferredRequestAsync(store).ConfigureAwait(false);

		var resumed = await BuildPass(store, LegalHoldCheckResult.NoHolds)
			.ReevaluateDeferredErasuresAsync(CancellationToken.None).ConfigureAwait(false);

		resumed.ShouldBe(1, "the pass must report what it resumed; a count of zero beside a resumed request "
			+ "makes the pass unobservable to an operator");

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status.ShouldNotBeNull();
		status!.Status.ShouldBe(
			ErasureRequestStatus.Scheduled,
			"a request a hold deferred must return to the executable state once no hold blocks it, or the "
			+ "erasure is never performed and nothing reports that it was not");
	}

	// SAFETY. A hold that still blocks must leave the request deferred. Without this the pass would
	// re-offer every held request on every poll, and each one would cost a claim cycle to refuse.
	[Fact]
	public async Task Leave_a_request_deferred_while_a_hold_still_blocks_it()
	{
		var store = NewStore();
		var requestId = await DeferredRequestAsync(store).ConfigureAwait(false);

		var resumed = await BuildPass(store, LegalHoldCheckResult.WithHolds([Hold]))
			.ReevaluateDeferredErasuresAsync(CancellationToken.None).ConfigureAwait(false);

		resumed.ShouldBe(0);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status!.Status.ShouldBe(
			ErasureRequestStatus.BlockedByLegalHold,
			"a hold that still blocks erasure must leave the request deferred");
	}

	// SAFETY, and the case the obvious implementation gets wrong. A PARTIAL hold names categories that
	// must be RETAINED, and neither mechanism the executor runs can exclude a category from destruction --
	// the subject key is scoped to the subject, and the event-store pass is whole-aggregate. So a partial
	// hold blocks this erasure exactly as a full one does. RED if the check reads only ErasureBlocked,
	// which is false for a partial hold.
	[Fact]
	public async Task Leave_a_request_deferred_while_a_hold_blocks_only_some_categories()
	{
		var store = NewStore();
		var requestId = await DeferredRequestAsync(store).ConfigureAwait(false);

		var check = LegalHoldCheckResult.WithPartialHolds([Hold], ["Invoices"]);
		check.ErasureBlocked.ShouldBeFalse(
			"the fixture's own precondition: a partial hold does NOT set ErasureBlocked, which is exactly "
			+ "why reading that property alone resumes a request the executor must refuse");
		check.IsPartiallyBlocked.ShouldBeTrue();

		var resumed = await BuildPass(store, check)
			.ReevaluateDeferredErasuresAsync(CancellationToken.None).ConfigureAwait(false);

		resumed.ShouldBe(0);

		var status = await store.GetStatusAsync(requestId, CancellationToken.None).ConfigureAwait(false);
		status!.Status.ShouldBe(
			ErasureRequestStatus.BlockedByLegalHold,
			"a hold that retains specific categories still blocks this erasure, because the per-subject key "
			+ "destruction and the whole-aggregate tombstone cannot exclude a retained category");
	}

	// SAFETY. The pass must not touch a request that is not deferred. A pass that wrote Scheduled over a
	// claimed request would re-offer work another executor owns, which is the harm the claim prevents.
	[Fact]
	public async Task Not_touch_a_request_that_no_hold_deferred()
	{
		var store = NewStore();

		var request = NewRequest();
		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None)
			.ConfigureAwait(false);
		// The arm's own assertion below says "a claimed request", so it takes a real claim. Taking
		// custody is no longer expressible as a status write, which is the point of the change.
		_ = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		var resumed = await BuildPass(store, LegalHoldCheckResult.NoHolds)
			.ReevaluateDeferredErasuresAsync(CancellationToken.None).ConfigureAwait(false);

		resumed.ShouldBe(0, "a claimed request is not deferred and is none of this pass's business");

		var status = await store.GetStatusAsync(request.RequestId, CancellationToken.None).ConfigureAwait(false);
		status!.Status.ShouldBe(
			ErasureRequestStatus.InProgress,
			"re-offering a request another executor owns is the harm the execution claim exists to prevent");
	}

	// THE END-TO-END ARM, and the one the acceptance criterion actually names: block by a hold, release
	// the hold, and assert the erasure COMPLETES rather than merely becoming executable again. The arms
	// above bind the pass; this one binds the lifecycle the pass exists to unblock, through the real
	// executor. RED before the pass existed: the request stays deferred and never completes.
	[Fact]
	public async Task Complete_an_erasure_whose_hold_was_released_rather_than_leaving_it_deferred()
	{
		using var keys = new InMemoryKeyManagementProvider(NullLogger<InMemoryKeyManagementProvider>.Instance);
		var harness = new ErasureLifecycleHarness(keys, keys);

		// The hold is in force when the erasure first runs, so the executor defers it.
		Hold_is(harness, LegalHoldCheckResult.WithHolds([Hold]));

		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("subject-released-hold").ConfigureAwait(false);

		(await harness.StatusOfAsync(requestId).ConfigureAwait(false)).ShouldBe(
			ErasureRequestStatus.BlockedByLegalHold,
			"the fixture's own precondition: without a deferral first, the resume below has nothing to act "
			+ "on and this arm would pass against a store that never deferred anything");

		// The hold is released. Nothing notifies this framework, which is why the pass re-READS rather
		// than waiting to be told.
		Hold_is(harness, LegalHoldCheckResult.NoHolds);

		var resumed = await new ErasureHoldReevaluationProcessor(
				harness.Store, harness.LegalHolds, NullLogger<ErasureHoldReevaluationProcessor>.Instance)
			.ReevaluateDeferredErasuresAsync(CancellationToken.None).ConfigureAwait(false);

		resumed.ShouldBe(1);

		// The pass grants eligibility; the EXECUTOR erases. This is the scheduler's next poll.
		_ = await harness.Service.ExecuteAsync(requestId, CancellationToken.None).ConfigureAwait(false);

		(await harness.StatusOfAsync(requestId).ConfigureAwait(false)).ShouldBe(
			ErasureRequestStatus.Completed,
			"an erasure whose legal hold has been released must actually be performed; a request that only "
			+ "becomes executable and is never executed leaves the obligation undischarged");
	}

	private static void Hold_is(ErasureLifecycleHarness harness, LegalHoldCheckResult result) =>
		A.CallTo(() => harness.LegalHolds.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(result));

	private static InMemoryErasureStore NewStore() =>
		new(TestDataSubjectHasher.Instance, UntenantedContext.Instance, Options.Create(new TenantContextOptions()), TimeProvider.System);

	private static ErasureRequest NewRequest() =>
		new()
		{
			RequestId = Guid.NewGuid(),
			DataSubjectId = $"user-{Guid.NewGuid():N}",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			RequestedBy = "test-admin",
			RequestedAt = DateTimeOffset.UtcNow
		};

	/// <summary>
	/// Puts one request into the deferred state THROUGH THE CLAIM, which is the only path production
	/// reaches it by.
	/// </summary>
	/// <remarks>
	/// Writing the status directly would succeed even against a store whose conditional write is broken,
	/// so the fixture would be blind to a provider that refuses the transition in production.
	/// </remarks>
	private static async Task<Guid> DeferredRequestAsync(InMemoryErasureStore store)
	{
		var request = NewRequest();
		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddMinutes(-5), CancellationToken.None)
			.ConfigureAwait(false);

		// The arm's own words below are "the claim must succeed" and "from inside the claimed window",
		// so it now takes a real claim and presents its identity -- which is what makes the second write
		// genuinely inside the window rather than merely after the first one.
		var claim = await store.ClaimForExecutionAsync(
				request.RequestId, ErasureRequestStatus.Scheduled, TimeSpan.FromMinutes(5), CancellationToken.None)
			.ConfigureAwait(false);

		claim.Outcome.ShouldBe(
			ErasureClaimOutcome.Granted, "the claim must succeed on a freshly scheduled request");

		(await store.UpdateStatusAsync(request.RequestId, ErasureRequestStatus.BlockedByLegalHold, expectedStatus: null, "Legal hold active", claim.ClaimIdentity, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBe(
			ErasureWriteOutcome.Applied, "the deferral is recorded from inside the claimed window");

		return request.RequestId;
	}

	private static IErasureHoldReevaluationProcessor BuildPass(
		InMemoryErasureStore store, LegalHoldCheckResult check)
	{
		var holds = A.Fake<ILegalHoldService>();
		_ = A.CallTo(() => holds.CheckHoldsByHashAsync(
				A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(check));

		return new ErasureHoldReevaluationProcessor(
			store, holds, NullLogger<ErasureHoldReevaluationProcessor>.Instance);
	}
}
