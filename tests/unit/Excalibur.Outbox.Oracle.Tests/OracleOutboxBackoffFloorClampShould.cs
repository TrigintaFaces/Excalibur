// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Infrastructure;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Real-infrastructure locks on the property that a configured failure-backoff floor F is honoured on the
/// path the outbox processor actually PREFERS, not only on the one the guarantee contract describes.
/// </summary>
/// <remarks>
/// <para>
/// When a store advertises <see cref="IBackoffSchedulableOutboxStore"/> the processor stops calling
/// <c>MarkFailedAsync</c> and calls <c>MarkFailedWithBackoffAsync</c> instead, handing it a next-attempt
/// instant it computed itself. That instant used to be bound verbatim. The framework backoff calculator
/// yields roughly a second at the first attempt, so a consumer who configured a floor of several minutes got
/// a retry a second later: the floor was accepted and ignored, and the capability meant to REFINE the
/// schedule instead weakened the guarantee below what the same failure gets without it.
/// </para>
/// <para>
/// These arms mirror the provider-agnostic pair used by the SQL Server, Postgres, MongoDB and Redis locks.
/// They are restated here rather than inherited because that base lives in the integration test project,
/// which does not reference this package — and adding the reference would pull an Oracle container into a
/// shard that has none. The assertions and their reasoning are deliberately identical.
/// </para>
/// <para>
/// Oracle composes the gate on the SERVER clock — <c>SYSTIMESTAMP + NUMTODSINTERVAL(GREATEST(delay, F))</c>,
/// the same <c>SYSTIMESTAMP</c> the claim predicate compares against — so no dispatcher clock reaches the
/// persisted value at all. The two skew arms below are what make that a measured property rather than a
/// reading of the statement.
/// </para>
/// <para>
/// Never skipped. The operational root cause of an earlier Oracle defect was a skip-gated arm that never
/// ran; a skip here would re-open exactly that hole.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleOutboxBackoffFloorClampShould : IClassFixture<OracleOutboxStoreContainerFixture>
{
	/// <summary>The configured floor F. Long enough that an unclamped ~1s backoff is unambiguously inside it.</summary>
	private const int FloorSeconds = 30;

	/// <summary>A deliberately short floor, so a longer caller schedule is the binding constraint.</summary>
	private const int ShortFloorSeconds = 2;

	/// <summary>
	/// How long the liveness arm keeps asking for a message whose floor has elapsed before it calls the
	/// message lost.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Generous on purpose, and not a tolerance on the store's behaviour. The floor is measured on the
	/// SERVER's clock (<c>SYSTIMESTAMP</c>); any wait this test performs is measured on the TEST HOST's.
	/// Those are two different clocks, and on a loaded machine a containerised database's clock does not
	/// advance in step with the host's -- it stalls and then catches up. Measured on the machine this suite
	/// runs on, sampling a container's clock across fifty host-side 2.5 second waits: six of the fifty
	/// advanced the container clock by only 345 to 541 ms. A server whose clock moved 345 ms has not seen a
	/// two second floor elapse, however long the host waited.
	/// </para>
	/// <para>
	/// A single sample taken after a fixed sleep therefore asserts that the SERVER has seen the floor elapse
	/// when only the HOST has, and reports a store that is behaving correctly -- deferring a retry because
	/// its own clock says the floor has not passed -- as a message that never came back. Deferring is the
	/// safe direction for an at-least-once outbox, so the store is right and the sample is wrong. Polling
	/// asserts the same property without assuming the two clocks agree, and a store that genuinely strands
	/// the message still fails when this window expires.
	/// </para>
	/// </remarks>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(30);

	/// <summary>How often the liveness arm re-asks the store while the window is open.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(250);

	private readonly OracleOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="OracleOutboxBackoffFloorClampShould"/> class.</summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleOutboxBackoffFloorClampShould(OracleOutboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY. A computed backoff shorter than the configured floor must not make the message re-claimable
	/// before the floor elapses.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task NotReclaimBeforeTheFloor_WhenTheComputedBackoffIsShorterThanIt()
	{
		var ct = TestContext.Current.CancellationToken;
		var store = await CreateStoreAsync(FloorSeconds).ConfigureAwait(false);

		var schedulable = store.GetService(typeof(IBackoffSchedulableOutboxStore)) as IBackoffSchedulableOutboxStore;
		schedulable.ShouldNotBeNull(
			"this provider must advertise the backoff capability, otherwise the processor would never take " +
			"the path under test and this lock would be vacuous.");

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// Exactly what the processor computes at the first attempt: about one second out, from its own clock.
		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "boom", 1, DateTimeOffset.UtcNow.AddSeconds(1), ct).ConfigureAwait(false);

		// Well past the computed delay, nowhere near the floor.
		await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);

		var claimed = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		claimed.ShouldNotContain(
			m => m.Id == message.Id,
			$"a configured floor of {FloorSeconds}s must hold on the backoff path too. The computed delay was " +
			"about a second, so binding it verbatim makes the message re-claimable here — and this is the " +
			"path the processor PREFERS whenever the store advertises the capability, so the floor a " +
			"consumer configured is the one production ignores.");
	}

	/// <summary>
	/// LIVENESS. A computed backoff LONGER than the floor is still honoured, and the message does come back.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the clamp above is satisfied by discarding the caller schedule entirely and pinning
	/// every retry to exactly F, which would flatten the exponential curve the capability exists to apply.
	/// The second half then waits the short floor out, so the clamp is shown to DEFER the retry rather than
	/// cancel it.
	/// </remarks>
	[Fact]
	public async Task StillHonourALongerComputedBackoff_AndReturnTheMessageOnceItElapses()
	{
		var ct = TestContext.Current.CancellationToken;
		var store = await CreateStoreAsync(ShortFloorSeconds).ConfigureAwait(false);
		var schedulable = (IBackoffSchedulableOutboxStore)store.GetService(typeof(IBackoffSchedulableOutboxStore))!;

		var deferred = NewMessage();
		await store.StageMessageAsync(deferred, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// A computed delay well BEYOND the floor -- a later attempt on the exponential curve.
		await schedulable.MarkFailedWithBackoffAsync(
			deferred.Id, "boom", 5, DateTimeOffset.UtcNow.AddSeconds(30), ct).ConfigureAwait(false);

		await Task.Delay(TimeSpan.FromSeconds(ShortFloorSeconds + 2), ct).ConfigureAwait(false);

		var afterFloor = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		afterFloor.ShouldNotContain(
			m => m.Id == deferred.Id,
			"the floor is a LOWER bound, not the schedule. A computed backoff beyond F must still be " +
			"honoured, or the clamp has flattened the exponential curve onto a constant F.");

		// And a message whose schedule HAS elapsed is genuinely handed back, so neither bound strands it.
		var due = NewMessage();
		await store.StageMessageAsync(due, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		await schedulable.MarkFailedWithBackoffAsync(
			due.Id, "boom", 1, DateTimeOffset.UtcNow.AddSeconds(1), ct).ConfigureAwait(false);

		await Task.Delay(TimeSpan.FromSeconds(ShortFloorSeconds + 2), ct).ConfigureAwait(false);

		// Asked repeatedly rather than sampled once: see ReclaimWindow for why one sample after a fixed
		// wait cannot tell a store that is withholding the message from a store whose own clock has not
		// yet reached the floor.
		var returned = await WaitHelpers.WaitUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == due.Id),
			ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		returned.ShouldBeTrue(
			"once both the floor and the computed schedule have elapsed the message must be re-claimed. " +
			"A clamp that withheld it forever would satisfy the safety arm by dropping the message. This " +
			$"arm kept asking for {ReclaimWindow.TotalSeconds:0} seconds, which is far longer than any " +
			"clock stall observed on this host, so the message was never handed back.");
	}

	/// <summary>
	/// The skew this dispatcher's clock is driven to. An hour is far beyond any floor under test, so a gate
	/// that carries the skew is unmistakably distinguishable from one that does not.
	/// </summary>
	private static readonly TimeSpan DispatcherSkew = TimeSpan.FromHours(1);

	/// <summary>
	/// LIVENESS UNDER SKEW. A dispatcher whose clock runs an hour ahead of the database must still get the
	/// message back once the SERVER has seen the floor elapse — not an hour later.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// This is the arm the two-clock gate failed. The statement used to persist
	/// <c>GREATEST(:NextAttemptAt, SYSTIMESTAMP + F)</c>, composing an instant the DISPATCHER computed with a
	/// floor anchored on the DATABASE, while the claim predicate compared the result against
	/// <c>SYSTIMESTAMP</c>. One comparison, two machines. A dispatcher running ahead therefore wrote a gate an
	/// hour in the database's future and the message stayed invisible for the whole skew after its backoff had
	/// genuinely elapsed — a delivery stall bounded by nothing but the size of the skew, and one that no
	/// safety property notices, because a store that never hands a due message back violates none of them.
	/// </para>
	/// <para>
	/// The caller's instant is computed from the SAME skewed clock the store is given, which is the faithful
	/// model: in production the processor and the store are one process reading one system clock, and it is
	/// that clock, not the two of them separately, that is offset from the database. Subtracting two readings
	/// of it cancels the offset exactly, which is why the repair is a subtraction rather than a tolerance.
	/// </para>
	/// <para>
	/// RED on the pre-fix statement: restore <c>GREATEST(:NextAttemptAt, SYSTIMESTAMP + F)</c> and this arm
	/// polls for its whole window and never sees the message.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ReturnTheMessageOnTheServerFloor_WhenTheDispatcherClockRunsAheadOfTheDatabase()
	{
		var ct = TestContext.Current.CancellationToken;
		var clock = new SkewedClock(DispatcherSkew);
		var store = await CreateStoreAsync(ShortFloorSeconds, clock).ConfigureAwait(false);
		var schedulable = (IBackoffSchedulableOutboxStore)store.GetService(typeof(IBackoffSchedulableOutboxStore))!;

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		// What the processor computes at the first attempt, on this host's clock: about a second out.
		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "boom", 1, clock.GetUtcNow().AddSeconds(1), ct).ConfigureAwait(false);

		var returned = await WaitHelpers.WaitUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == message.Id),
			ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		returned.ShouldBeTrue(
			$"the floor is {ShortFloorSeconds}s and this arm kept asking for {ReclaimWindow.TotalSeconds:0}s, "
			+ $"so a message still withheld here is being held for the dispatcher's {DispatcherSkew.TotalHours:0}"
			+ "-hour skew rather than for its backoff. The gate must be measured entirely on the database's "
			+ "clock, which is the clock the claim predicate reads it back on.");
	}

	/// <summary>
	/// CURVE UNDER SKEW. A computed backoff longer than the floor is preserved as the DELAY it represents,
	/// neither flattened onto F nor inflated by the dispatcher's offset.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the one above is satisfied by discarding the caller's schedule and pinning every
	/// retry to exactly F, which would flatten the exponential curve the capability exists to apply. It is
	/// equally RED on the pre-fix statement, and for the same reason: an instant computed an hour ahead was
	/// persisted verbatim, so a twenty-second backoff became an hour-and-twenty-second one.
	/// </remarks>
	[Fact]
	public async Task PreserveALongerComputedBackoffAsADelay_WhenTheDispatcherClockRunsAheadOfTheDatabase()
	{
		var ct = TestContext.Current.CancellationToken;
		var clock = new SkewedClock(DispatcherSkew);
		var store = await CreateStoreAsync(ShortFloorSeconds, clock).ConfigureAwait(false);
		var schedulable = (IBackoffSchedulableOutboxStore)store.GetService(typeof(IBackoffSchedulableOutboxStore))!;

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();

		await schedulable.MarkFailedWithBackoffAsync(
			message.Id, "boom", 5, clock.GetUtcNow().Add(SkewedCurveDelay), ct).ConfigureAwait(false);

		// Well past the floor, nowhere near the caller's schedule: the curve, not F, is binding here.
		await Task.Delay(TimeSpan.FromSeconds(ShortFloorSeconds + 3), ct).ConfigureAwait(false);

		var early = (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false)).ToList();
		early.ShouldNotContain(
			m => m.Id == message.Id,
			"the floor is a LOWER bound, not the schedule. A computed backoff beyond F must still be "
			+ "honoured, or re-anchoring the delay has flattened the exponential curve onto a constant F.");

		var returned = await WaitHelpers.WaitUntilAsync(
			async () => (await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false))
				.Any(m => m.Id == message.Id),
			SkewedCurveDelay + ReclaimWindow,
			ReclaimPollInterval,
			ct).ConfigureAwait(false);

		returned.ShouldBeTrue(
			$"a {SkewedCurveDelay.TotalSeconds:0}-second computed backoff must elapse in "
			+ $"{SkewedCurveDelay.TotalSeconds:0} seconds of the DATABASE's time. A message still withheld "
			+ "after that plus the whole reclaim window is carrying the dispatcher's skew in its gate.");
	}

	/// <summary>The caller-computed delay used by the curve-under-skew arm: comfortably beyond the floor.</summary>
	private static readonly TimeSpan SkewedCurveDelay = TimeSpan.FromSeconds(15);

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue") { Id = Guid.NewGuid().ToString() };

	private async Task<IOutboxStore> CreateStoreAsync(int floorSeconds, TimeProvider? dispatcherClock = null)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — this real-infra backoff-floor lock is NEVER skipped. An "
			+ "earlier Oracle defect reached committed HEAD precisely because its arm was skip-gated and never "
			+ "ran; a skip here would re-open exactly that hole.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Oracle connection per access.
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return new OracleOutboxStore(
			db, options, NullLogger<OracleOutboxStore>.Instance, metrics: null, timeProvider: dispatcherClock);
	}

	/// <summary>
	/// A dispatcher clock that runs a fixed offset away from the real one. Everything else about the store is
	/// untouched, so the only difference between these arms and the ones above is what time this process
	/// thinks it is.
	/// </summary>
	private sealed class SkewedClock(TimeSpan offset) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + offset;
	}
}
