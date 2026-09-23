// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// The in-memory outbox fences the COMPLETION path — the failure report and the dead-letter transition —
/// not only the claim and the mark-sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> Fencing the mark-sent alone leaves the two transitions a FAILED delivery takes
/// unfenced, so a superseded tenure could report a failure, apply a backoff, or bury a message that the
/// live tenure then delivers successfully — leaving it simultaneously delivered and sitting unreplayed in
/// the dead-letter queue, where an operator draining the queue re-executes work that already succeeded.
/// The composition guard was taught to refuse a store that cannot fence those transitions, and this store
/// could not, so a host composing the in-memory outbox with a leader election refused to start — pointed
/// at the very store that guard recommends as the remedy.
/// </para>
/// <para>
/// <b>THE WIRING ARM IS NOT DECORATION.</b> This store recorded its claim in a private side-map and handed
/// back a message with no <c>DispatcherId</c>. The drain reads the claim identity it completes under from
/// the message it was handed, so a null one makes the claim-scoped completion unreachable <i>no matter
/// which capabilities the store advertises</i> — the capability would be inert while reading as present to
/// every guard that probes for it. <see cref="StampTheClaimIdentityOntoTheMessageItHandsBack"/> binds that,
/// and without it every other arm here would pass while the fix did nothing in production.
/// </para>
/// <para>
/// <b>SAFETY AND LIVENESS, PAIRED.</b> A store that refused every completion would satisfy every refusal
/// arm below and deliver nothing forever. Each refusal is therefore paired with an arm proving the
/// legitimate completion still applies, and with an assertion that the refused message was <i>not mutated</i>
/// — a refusal that silently wrote would be indistinguishable from one that did not.
/// </para>
/// <para>
/// <b>RED-on-mutant.</b> Remove the <c>FencingToken &lt; _fencingHighWaterMark</c> guard from
/// <c>MarkFailedAsync</c> and <see cref="RefuseAFailureReportFromASupersededTenure"/> goes RED. Remove it
/// from <c>MarkDeadLetteredAsync</c> and <see cref="RefuseADeadLetterFromASupersededTenure"/> goes RED.
/// Drop the claim comparison and <see cref="RefuseAFailureReportAgainstAClaimTheCallerNoLongerHolds"/> goes
/// RED. Return <c>Applied</c> for an already-sent message and
/// <see cref="RefuseADeadLetterForAMessageAlreadyDelivered"/> goes RED. Delete the <c>DispatcherId</c>
/// stamp and the wiring arm goes RED.
/// </para>
/// <para>
/// Deterministic: no wall-clock dependence, no Docker. The high-water starts at 0 and only ever advances.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class InMemoryOutboxFencedCompletionPathShould : IDisposable
{
	private const long SupersededTenure = 5;
	private const long LiveTenure = 10;

	private readonly InMemoryOutboxStore _store;

	public InMemoryOutboxFencedCompletionPathShould()
	{
		var options = Options.Create(new InMemoryOutboxOptions { MaxMessages = 10_000 });
		_store = new InMemoryOutboxStore(options, NullLogger<InMemoryOutboxStore>.Instance);
	}

	public void Dispose() => _store.Dispose();

	private async Task<string> StageAsync()
	{
		var msg = new OutboundMessage("test.message", [1], "dest");
		await _store.StageMessageAsync(msg, CancellationToken.None);
		return msg.Id;
	}

	/// <summary>Claims under a tenure and returns the claim identity the store stamped on the message.</summary>
	private async Task<string> ClaimUnderAsync(long fencingToken)
	{
		var claimed = (await _store.GetUnsentMessagesAsync(100, fencingToken, CancellationToken.None)).ToList();
		claimed.ShouldNotBeEmpty("the claim must yield the staged message, or the arm proves nothing");
		return claimed[0].DispatcherId!;
	}

	/// <summary>
	/// Whether the store reports this message in its FAILED set - a real public observable, used instead of
	/// a per-message status field the store does not expose.
	/// </summary>
	private async Task<bool> IsInTheFailedSetAsync(string messageId)
	{
		var failed = await _store.GetAllTenantsFailedMessagesAsync(
			maxRetries: int.MaxValue, olderThan: null, batchSize: 1000, CancellationToken.None);

		return failed.Any(m => string.Equals(m.Id, messageId, StringComparison.Ordinal));
	}

	// ---------------------------------------------------------------------------------------------
	// WIRING. Without this the capability is advertised and unreachable.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task StampTheClaimIdentityOntoTheMessageItHandsBack()
	{
		_ = await StageAsync();

		var claimed = (await _store.GetUnsentMessagesAsync(100, LiveTenure, CancellationToken.None)).ToList();

		claimed.ShouldNotBeEmpty();
		claimed[0].DispatcherId.ShouldNotBeNullOrWhiteSpace(
			"the drain reads the claim identity it completes under from the message it was handed; a null one "
			+ "makes the claim-scoped completion unreachable however many capabilities the store advertises");
	}

	[Fact]
	public void AdvertiseBothCompletionPathFencingCapabilities()
	{
		// Discovered the way the composition guard and the drain discover them - through GetService, never a
		// cast. A cast sees only the outermost type and answers "absent" through any decorator, so an arm
		// that casts would pass for a composition the guard rejects.
		// Through the IServiceProvider face, exactly as the guard and the drain reach it.
		IServiceProvider capabilities = _store;

		_ = capabilities.GetService(typeof(IFencedClaimScopedOutboxStore))
			.ShouldBeAssignableTo<IFencedClaimScopedOutboxStore>(
				"a host composing this store with a leader election refuses to start without it");

		_ = capabilities.GetService(typeof(IFencedDeadLetterableOutboxStore))
			.ShouldBeAssignableTo<IFencedDeadLetterableOutboxStore>(
				"a host composing this store with a leader election refuses to start without it");
	}

	// ---------------------------------------------------------------------------------------------
	// THE FAILURE REPORT: fence, claim, and the liveness that separates the fix from an over-fix.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task RefuseAFailureReportFromASupersededTenure()
	{
		var id = await StageAsync();
		var handover = await StageAsync();
		var claim = await ClaimUnderAsync(SupersededTenure);

		// A newer tenure takes over and advances the high-water, on a DIFFERENT message. The superseded
		// tenure has not noticed and still believes it holds the drain.
		await _store.MarkSentAsync(handover, LiveTenure, CancellationToken.None);

		var outcome = await _store.MarkFailedAsync(
			id, "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(SupersededTenure, claim), CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.FenceRefused,
			"a NEWER TENURE exists, so the caller must stop draining entirely rather than continue its batch");

		// SAFETY, stated as a STATE rather than a return value: a refusal that quietly wrote would return
		// FenceRefused and still have corrupted the row.
		(await IsInTheFailedSetAsync(id)).ShouldBeFalse(
			"a refused failure report must not have mutated the message the live tenure still owns");

		// LIVENESS on the same message: the row survived and the LIVE tenure can still complete it. Without
		// this arm a store that refused every failure report would pass everything above.
		(await _store.MarkFailedAsync(
			id, "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(LiveTenure, claim), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied,
				"the refusal must have cost the superseded caller its write and nothing else");
	}

	[Fact]
	public async Task ApplyAFailureReportFromTheLiveTenure()
	{
		// LIVENESS. A store that refused every completion would pass every refusal arm above and deliver
		// nothing forever.
		var id = await StageAsync();
		var claim = await ClaimUnderAsync(LiveTenure);

		var outcome = await _store.MarkFailedAsync(
			id, "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(LiveTenure, claim), CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.Applied);
		(await IsInTheFailedSetAsync(id)).ShouldBeTrue("the applied failure must be observable in the store");
	}

	[Fact]
	public async Task RefuseAFailureReportAgainstAClaimTheCallerNoLongerHolds()
	{
		// ORTHOGONAL to the fence, and that is the whole point of carrying both terms: no handover occurs
		// here at all. One process claims, hangs past its reservation, a later cycle re-claims the row under
		// the SAME token, and the first cycle then reports against a claim that is gone. A fence cannot
		// separate those two - the token is identical - so only the claim identity can.
		var id = await StageAsync();
		_ = await ClaimUnderAsync(LiveTenure);

		var outcome = await _store.MarkFailedAsync(
			id, "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(LiveTenure, "a-claim-this-store-never-issued"), CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.ClaimLost,
			"ROW-scoped: this one message moved on, and the caller carries on with the rest of its batch");

		(await IsInTheFailedSetAsync(id)).ShouldBeFalse("a refused report must not have written");
	}

	[Fact]
	public async Task RefuseAFailureReportForAMessageAlreadyDelivered()
	{
		// Terminal exclusion. Without it a failure reported after a successful send returns the message to
		// Failed - which IS in the claim predicate - so a delivered message is delivered again.
		var id = await StageAsync();
		var claim = await ClaimUnderAsync(LiveTenure);
		await _store.MarkSentAsync(id, LiveTenure, CancellationToken.None);

		var outcome = await _store.MarkFailedAsync(
			id, "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(LiveTenure, claim), CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.AlreadyTerminal,
			"the row is PRESENT and final - not absent, and not owned by someone else. This store keeps the "
			+ "row, so it can see the difference and must report it rather than pick a neighbouring value");

		(await IsInTheFailedSetAsync(id)).ShouldBeFalse(
			"a delivered message returned to Failed is back in the claim predicate, so it is delivered again");
	}

	[Fact]
	public async Task ReportAFailureAgainstAnAbsentMessageAsNotFoundRatherThanSilence()
	{
		var outcome = await _store.MarkFailedAsync(
			"no-such-message", "boom", retryCount: 1, nextAttemptAt: null,
			new OutboxWriteAuthority(LiveTenure, "any-claim"), CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.MessageNotFound,
			"a missing row means nothing is owed; reporting it as a lost claim would blame an owner that "
			+ "does not exist");
	}

	// ---------------------------------------------------------------------------------------------
	// THE DEAD-LETTER TRANSITION. This is the interleaving that costs an operator a duplicate execution.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task RefuseADeadLetterFromASupersededTenure()
	{
		var id = await StageAsync();
		var handover = await StageAsync();
		_ = await ClaimUnderAsync(SupersededTenure);

		// The handover: a newer tenure completes a different message and advances the high-water.
		await _store.MarkSentAsync(handover, LiveTenure, CancellationToken.None);

		var outcome = await _store.MarkDeadLetteredAsync(
			id, "max retries exceeded", SupersededTenure, CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.FenceRefused);

		// The row survives, which is what lets the live tenure still deliver it. The unfenced transition
		// destroys the row on a message-id match alone, so a superseded tenure reaching its attempt ceiling
		// would bury a message a successor still holds and has not delivered.
		// Proven by the live tenure still being able to apply its OWN terminal transition to the same
		// message: the row survived, which is what lets that tenure still deliver or resolve it.
		(await _store.MarkDeadLetteredAsync(id, "live tenure decides", LiveTenure, CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied,
				"a refused dead-letter must leave the message present and resolvable by the tenure that owns it");
	}

	[Fact]
	public async Task ApplyADeadLetterFromTheLiveTenure()
	{
		// LIVENESS for the dead-letter half.
		var id = await StageAsync();
		_ = await ClaimUnderAsync(LiveTenure);

		var outcome = await _store.MarkDeadLetteredAsync(
			id, "max retries exceeded", LiveTenure, CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.Applied);

		// Terminal, and observably so: a second dead-letter finds nothing left to transition.
		(await _store.MarkDeadLetteredAsync(id, "again", LiveTenure, CancellationToken.None))
			.ShouldNotBe(OutboxCompletionOutcome.Applied, "DeadLettered is terminal - it cannot be applied twice");
	}

	[Fact]
	public async Task RefuseADeadLetterForAMessageAlreadyDelivered()
	{
		// THE OUTCOME HERE IS LOAD-BEARING FOR A WRITE THAT ALREADY HAPPENED OUTSIDE THIS STORE. The drain
		// writes the external dead-letter entry BEFORE this mark, and withdraws it only when this call
		// answers with something other than Applied. Answering Applied for a SENT message would leave that
		// message simultaneously delivered and sitting in the dead-letter queue for an operator to replay.
		var id = await StageAsync();
		_ = await ClaimUnderAsync(LiveTenure);
		await _store.MarkSentAsync(id, LiveTenure, CancellationToken.None);

		var outcome = await _store.MarkDeadLetteredAsync(
			id, "max retries exceeded", LiveTenure, CancellationToken.None);

		outcome.ShouldBe(OutboxCompletionOutcome.AlreadyTerminal,
			"the drain withdraws its external dead-letter entry on any non-Applied outcome, and a delivered "
			+ "message must not be left actionable in the dead-letter queue. Asserting the EXACT member "
			+ "rather than not-Applied is deliberate: not-Applied is also satisfied by MessageNotFound, "
			+ "which is what this branch used to return and which is false of a row that is right there");
	}

	[Fact]
	public async Task AcceptAnEqualTokenOnBothCompletionMembers()
	{
		// The guard is strictly-less-than: an equal token is the SAME tenure and is still valid. A <= guard
		// would refuse a live tenure's own second completion and stall the drain.
		var first = await StageAsync();
		var second = await StageAsync();
		var claim = await ClaimUnderAsync(LiveTenure);

		(await _store.MarkFailedAsync(
			first, "boom", 1, null, new OutboxWriteAuthority(LiveTenure, claim), CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied);

		(await _store.MarkDeadLetteredAsync(second, "ceiling", LiveTenure, CancellationToken.None))
			.ShouldBe(OutboxCompletionOutcome.Applied);
	}
}
