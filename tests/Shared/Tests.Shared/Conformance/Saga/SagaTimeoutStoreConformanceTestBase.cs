// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

using Shouldly;

using Xunit;

namespace Tests.Shared.Conformance.Saga;

/// <summary>
/// Conformance kit for <see cref="ISagaTimeoutStore"/>. Every implementation must derive from this base.
/// </summary>
/// <remarks>
/// <para>
/// The delivery service passes its configured batch size to <see cref="ISagaTimeoutStore.ClaimDueTimeoutsAsync"/>
/// and does not trim the result itself. Bounding the batch is therefore the <b>store's</b> obligation, and it is
/// enforced by nothing else: an implementation that ignores <c>batchSize</c> causes an unbounded dispatch on
/// every poll cycle. That is a stampede, not a compile error, and no unit test of the delivery service can catch
/// it. This kit is the enforcement.
/// </para>
/// <para>
/// <see cref="ISagaTimeoutStore.ClaimDueTimeoutsAsync"/> also <em>leases</em>: a claimed timeout must not be
/// handed to a second claimer, or two delivery workers double-fire the same timeout. That contract is checked
/// here too, so a store cannot satisfy the batch bound by returning the same rows twice.
/// </para>
/// <para>
/// To bind an implementation: derive, override <see cref="CreateStoreAsync"/> and <see cref="CleanupAsync"/>.
/// </para>
/// </remarks>
public abstract class SagaTimeoutStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>Gets the store under test.</summary>
	protected ISagaTimeoutStore Store { get; private set; } = null!;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync() => Store = await CreateStoreAsync().ConfigureAwait(false);

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>Creates the store under test.</summary>
	protected abstract Task<ISagaTimeoutStore> CreateStoreAsync();

	/// <summary>Cleans up the store between tests.</summary>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// The store must return at most <c>batchSize</c> timeouts, even when more are due. The delivery service
	/// relies on this bound; it applies none of its own.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_ReturnsAtMostBatchSize_WhenMoreTimeoutsAreDue()
	{
		var asOf = DateTimeOffset.UtcNow;
		await ScheduleDueTimeoutsAsync(count: 5, asOf).ConfigureAwait(false);

		var claimed = await Store.ClaimDueTimeoutsAsync(asOf, batchSize: 2, CancellationToken.None)
			.ConfigureAwait(false);

		claimed.Count.ShouldBe(2, "the store must honor batchSize; the delivery service does not trim the result");
	}

	/// <summary>
	/// The bound must not be satisfied by starving the caller: when at least <c>batchSize</c> timeouts are due,
	/// the store returns exactly that many. Guards against an implementation that "honors" the bound by
	/// returning nothing.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_ReturnsExactlyBatchSize_WhenAtLeastThatManyAreDue()
	{
		var asOf = DateTimeOffset.UtcNow;
		await ScheduleDueTimeoutsAsync(count: 3, asOf).ConfigureAwait(false);

		var claimed = await Store.ClaimDueTimeoutsAsync(asOf, batchSize: 3, CancellationToken.None)
			.ConfigureAwait(false);

		claimed.Count.ShouldBe(3);
	}

	/// <summary>
	/// A claim is a lease: a second claimer must not receive a timeout the first already claimed. Without this,
	/// a store could satisfy the batch bound while two delivery workers double-fire the same timeout.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_DoesNotReturnAnAlreadyClaimedTimeout()
	{
		var asOf = DateTimeOffset.UtcNow;
		await ScheduleDueTimeoutsAsync(count: 4, asOf).ConfigureAwait(false);

		var first = await Store.ClaimDueTimeoutsAsync(asOf, batchSize: 2, CancellationToken.None)
			.ConfigureAwait(false);
		var second = await Store.ClaimDueTimeoutsAsync(asOf, batchSize: 2, CancellationToken.None)
			.ConfigureAwait(false);

		first.Count.ShouldBe(2);
		second.Count.ShouldBe(2);

		var firstIds = first.Select(t => t.TimeoutId).ToHashSet(StringComparer.Ordinal);
		var overlap = second.Where(t => firstIds.Contains(t.TimeoutId)).Select(t => t.TimeoutId).ToList();

		overlap.ShouldBeEmpty("a claimed timeout is leased and must never be handed to a second claimer");
	}

	/// <summary>
	/// The lease must hold under <em>concurrency</em>, not merely in sequence. Given N delivery workers
	/// draining the same due set at once, every due timeout is claimed by <b>exactly one</b> worker: no
	/// timeout is handed to two workers (safety — no double-fire) and no due timeout is dropped (liveness —
	/// each is delivered). The sequential <see cref="ClaimDueTimeoutsAsync_DoesNotReturnAnAlreadyClaimedTimeout"/>
	/// cannot see this: a non-atomic read-then-write store only double-claims when two claimers read the same
	/// due set <em>before</em> either records its claim, which requires them to run concurrently.
	/// </summary>
	/// <remarks>
	/// <b>RED-on-mutant:</b> split the store's atomic select-and-claim into a non-atomic read-then-write
	/// (SELECT the due batch, then persist the claims in a separate, unsynchronised step) ⇒ concurrent workers
	/// select overlapping due sets before either persists its claim ⇒ the union carries duplicate ids → RED.
	/// The in-process sibling <c>InMemorySagaTimeoutClaimDisjointShould</c> demonstrates the identical mutant
	/// against the in-memory backend; this arm binds the same contract on every provider store.
	/// </remarks>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_UnderConcurrentClaimers_ClaimsEachDueTimeoutExactlyOnce()
	{
		var asOf = DateTimeOffset.UtcNow;
		const int dueCount = 12;
		const int workers = 4;
		await ScheduleDueTimeoutsAsync(dueCount, asOf).ConfigureAwait(false);

		// N workers drain the SAME due set concurrently, each permitted to claim the whole batch.
		var claims = await Task.WhenAll(
			Enumerable.Range(0, workers).Select(_ =>
				Task.Run(async () =>
					(await Store.ClaimDueTimeoutsAsync(asOf, batchSize: dueCount, CancellationToken.None)
						.ConfigureAwait(false))
					.Select(t => t.TimeoutId).ToList())))
			.ConfigureAwait(false);

		var all = claims.SelectMany(ids => ids).ToList();

		// Safety: no timeout is claimed by more than one worker (no double-fire).
		all.Count.ShouldBe(
			all.Distinct(StringComparer.Ordinal).Count(),
			"a due timeout must be claimed by at most one concurrent worker — a duplicate id is a double-fire from a non-atomic claim");

		// Liveness: every due timeout is claimed by exactly one worker (none dropped).
		all.Distinct(StringComparer.Ordinal).Count().ShouldBe(
			dueCount,
			"every due timeout must be claimed by exactly one concurrent worker — a missing id is a dropped delivery");
	}

	/// <summary>
	/// A non-positive batch size is a programming error, not a request for an unbounded drain.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task ClaimDueTimeoutsAsync_RejectsNonPositiveBatchSize(int batchSize)
	{
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => Store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, batchSize, CancellationToken.None))
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Timeouts that are not yet due must never be claimed, regardless of the batch size.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_DoesNotClaimTimeoutsThatAreNotYetDue()
	{
		var asOf = DateTimeOffset.UtcNow;
		await ScheduleTimeoutAsync(dueAt: asOf.AddMinutes(10)).ConfigureAwait(false);

		var claimed = await Store.ClaimDueTimeoutsAsync(asOf, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false);

		claimed.ShouldBeEmpty();
	}

	/// <summary>
	/// <see cref="SagaTimeout.DueAt"/> is a <see cref="DateTimeOffset"/>: it denotes an instant, not a wall
	/// clock. A timeout one hour in the future must not be claimable now, whatever offset the caller used to
	/// express it. A store whose column type discards the offset stores the caller's wall-clock time and
	/// fires the timeout early by exactly that offset.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_DoesNotClaimAFutureTimeout_ExpressedInAWesternOffset()
	{
		var dueAt = DateTimeOffset.UtcNow.AddHours(1).ToOffset(TimeSpan.FromHours(-5));
		await ScheduleTimeoutAsync(dueAt).ConfigureAwait(false);

		var claimed = await Store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false);

		claimed.ShouldBeEmpty(
			"a timeout due one hour from now is not due now, regardless of the offset used to express it");
	}

	/// <summary>
	/// The mirror case: an overdue timeout expressed in an eastern offset is still overdue. A store that
	/// discards the offset holds it back by exactly that offset.
	/// </summary>
	[Fact]
	public async Task ClaimDueTimeoutsAsync_ClaimsAnOverdueTimeout_ExpressedInAnEasternOffset()
	{
		var dueAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToOffset(TimeSpan.FromHours(5));
		await ScheduleTimeoutAsync(dueAt).ConfigureAwait(false);

		var claimed = await Store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, batchSize: 10, CancellationToken.None)
			.ConfigureAwait(false);

		claimed.Count.ShouldBe(
			1,
			"a timeout that fell due five minutes ago is due now, regardless of the offset used to express it");
	}

	private async Task ScheduleDueTimeoutsAsync(int count, DateTimeOffset asOf)
	{
		for (var i = 0; i < count; i++)
		{
			// Stagger DueAt into the past so ordering is deterministic and every timeout is due at asOf.
			await ScheduleTimeoutAsync(dueAt: asOf.AddSeconds(-(count - i))).ConfigureAwait(false);
		}
	}

	private Task ScheduleTimeoutAsync(DateTimeOffset dueAt) =>
		Store.ScheduleTimeoutAsync(
			new SagaTimeout(
				TimeoutId: Guid.NewGuid().ToString(),
				SagaId: Guid.NewGuid().ToString(),
				SagaType: "ConformanceSaga",
				TimeoutType: "ConformanceTimeout",
				TimeoutData: null,
				DueAt: dueAt,
				ScheduledAt: dueAt.AddMinutes(-1)),
			CancellationToken.None);
}
