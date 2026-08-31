// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Shouldly;

using Xunit;

namespace Tests.Shared.Conformance.Saga;

/// <summary>
/// Conformance kit for the retention contract of <see cref="ISagaStore.PurgeCompletedBeforeAsync"/>: the purge
/// keys on <see cref="SagaState.CompletedAt"/>, which is a <see cref="DateTimeOffset"/> — an <em>instant</em>,
/// not a wall-clock reading.
/// </summary>
/// <remarks>
/// <para>
/// Nothing in the framework assigns <c>CompletedAt</c>. It is a plain settable property on <c>SagaState</c> and
/// the value that reaches the store is whatever the consumer wrote — commonly <c>DateTimeOffset.Now</c>, which
/// carries the host's offset. The purge compares it against a threshold the cleanup service computes in UTC.
/// A store that persists the wall-clock and discards the offset therefore compares two different clocks.
/// </para>
/// <para>
/// The two arms below are the <b>two directions</b> that error can run, and they fail in opposite ways. A single
/// arm proves neither: a store that never purges anything passes the survival arm, and a store that purges
/// everything passes the purge arm. Only both together pin the contract.
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Western offset, not yet due — must survive.</b> An offset-discarding store reads the wall-clock as
///     <em>earlier</em> than the true instant, so a saga still inside its retention window falls below the
///     threshold and is <c>DELETE</c>d. This is data loss, and it is silent.
///   </item>
///   <item>
///     <b>Eastern offset, overdue — must be purged.</b> The same store reads the wall-clock as <em>later</em>
///     than the true instant, so a saga past its retention window never falls below any threshold and is
///     retained forever. Retention is inert, and that is silent too.
///   </item>
/// </list>
/// <para>
/// Both arms are stated in terms of a true instant expressed through a non-zero offset, because an instant
/// expressed at <c>+00:00</c> cannot distinguish a store that preserves the offset from one that throws it away.
/// A fixture that completes sagas with <c>DateTimeOffset.UtcNow</c> is green against both a correct store and a
/// broken one, which is why this defect survived the suite that already covered <c>PurgeCompletedBeforeAsync</c>.
/// </para>
/// <para>
/// To bind an implementation: derive, override <see cref="CreateStoreAsync"/> and <see cref="CleanupAsync"/>.
/// </para>
/// </remarks>
public abstract class SagaStoreRetentionConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// An arbitrary instant, fixed rather than sampled from the clock. The arms assert a boundary, and a
	/// boundary sampled from <c>UtcNow</c> would move between the save and the purge.
	/// </summary>
	private static readonly DateTimeOffset CompletionInstant =
		new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

	/// <summary>Gets the store under test.</summary>
	protected ISagaStore Store { get; private set; } = null!;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync() => Store = await CreateStoreAsync(new UntenantedContext()).ConfigureAwait(false);

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>Creates the store under test, resolving the supplied ambient tenant.</summary>
	/// <param name="ambientTenant">
	/// The tenant context the store must resolve. The tenant arm switches the resolved tenant on this
	/// instance between operations, so a store that captures the tenant once at construction and one that
	/// re-reads it per operation are both exercised the way a real host uses them.
	/// </param>
	/// <returns>The store under test.</returns>
	/// <remarks>
	/// This parameter is required rather than optional deliberately. A parameterless overload alongside it
	/// would let every existing fixture keep compiling while silently opting out of the tenant arm — the
	/// advertised-but-unwired shape applied to a conformance kit, where the arm reports a pass having
	/// asserted nothing.
	/// </remarks>
	protected abstract Task<ISagaStore> CreateStoreAsync(ITenantContext ambientTenant);

	/// <summary>Cleans up the store between tests.</summary>
	protected abstract Task CleanupAsync();

	/// <summary>
	/// ARM 1 — a saga completed <em>after</em> the retention threshold must survive the purge, and the offset it
	/// was completed with must not change that.
	/// </summary>
	/// <remarks>
	/// The saga completes at <c>12:00Z</c>, expressed by a consumer five hours west as <c>07:00-05:00</c>. The
	/// threshold is <c>11:00Z</c> — a full hour <em>before</em> completion, so the saga is nowhere near due.
	/// <para>
	/// A store that keeps the instant compares <c>12:00Z &lt; 11:00Z</c> → false → the row survives.
	/// A store that keeps the wall-clock compares <c>07:00 &lt; 11:00</c> → true → <b>the row is deleted</b>,
	/// four hours of retention short. The saga is gone and nothing reports it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task PurgeCompletedBeforeAsync_DoesNotPurgeASagaCompletedAfterTheThreshold_ExpressedInAWesternOffset()
	{
		var sagaId = Guid.NewGuid();
		var completedAt = CompletionInstant.ToOffset(TimeSpan.FromHours(-5));

		// Guard the fixture, not the store: if this ever fails the arm has stopped testing what it claims,
		// because an instant at +00:00 cannot tell a correct store from an offset-discarding one.
		completedAt.Offset.ShouldNotBe(TimeSpan.Zero, "the arm is meaningless at a zero offset");
		completedAt.UtcDateTime.ShouldBe(CompletionInstant.UtcDateTime);

		await SaveCompletedSagaAsync(sagaId, completedAt).ConfigureAwait(false);

		var threshold = CompletionInstant.AddHours(-1);
		var purged = await Store.PurgeCompletedBeforeAsync(threshold, CancellationToken.None).ConfigureAwait(false);

		purged.ShouldBe(
			0,
			"a saga completed an hour after the retention threshold is not eligible for purge; a store that "
			+ "discards the offset reads its completion as 07:00 and deletes it");

		var survivor = await Store.LoadAsync<RetentionSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		_ = survivor.ShouldNotBeNull(
			"the saga was still inside its retention window and must not have been deleted");
	}

	/// <summary>
	/// ARM 2 — a saga completed <em>before</em> the retention threshold must be purged, and the offset it was
	/// completed with must not save it.
	/// </summary>
	/// <remarks>
	/// The saga completes at <c>12:00Z</c>, expressed by a consumer five hours east as <c>17:00+05:00</c>. The
	/// threshold is <c>13:00Z</c> — an hour <em>after</em> completion, so the saga is overdue.
	/// <para>
	/// A store that keeps the instant compares <c>12:00Z &lt; 13:00Z</c> → true → the row is purged.
	/// A store that keeps the wall-clock compares <c>17:00 &lt; 13:00</c> → false → <b>the row is retained</b>,
	/// and will be retained against every future threshold it outruns. The retention policy quietly does nothing.
	/// </para>
	/// This is the arm that a store passing ARM 1 by never purging anything cannot also pass.
	/// </remarks>
	[Fact]
	public async Task PurgeCompletedBeforeAsync_PurgesASagaCompletedBeforeTheThreshold_ExpressedInAnEasternOffset()
	{
		var sagaId = Guid.NewGuid();
		var completedAt = CompletionInstant.ToOffset(TimeSpan.FromHours(5));

		completedAt.Offset.ShouldNotBe(TimeSpan.Zero, "the arm is meaningless at a zero offset");
		completedAt.UtcDateTime.ShouldBe(CompletionInstant.UtcDateTime);

		await SaveCompletedSagaAsync(sagaId, completedAt).ConfigureAwait(false);

		var threshold = CompletionInstant.AddHours(1);
		var purged = await Store.PurgeCompletedBeforeAsync(threshold, CancellationToken.None).ConfigureAwait(false);

		purged.ShouldBe(
			1,
			"a saga completed an hour before the retention threshold is overdue; a store that discards the "
			+ "offset reads its completion as 17:00 and retains it indefinitely");

		var purgedSaga = await Store.LoadAsync<RetentionSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		purgedSaga.ShouldBeNull("the saga was past its retention window and must have been deleted");
	}

	/// <summary>
	/// ARM 3 — the confined purge deletes the calling tenant's completed saga and leaves every other
	/// tenant's alone.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="ISagaStore.PurgeCompletedBeforeAsync"/> is tenant-confined; the estate-wide sweep is a
	/// separate operation. Nothing in the offset arms above distinguishes the two, so a provider whose
	/// confined purge omits its tenant term deletes every tenant's completed sagas and still passes the
	/// suite — the deletion is silent, and retention is exactly where nobody is watching.
	/// </para>
	/// <para>
	/// Both halves are asserted together and neither is sufficient. A store that purges nothing satisfies
	/// the survival half; a store that purges the estate satisfies the deletion half. Only the pair pins
	/// the operation to its own partition.
	/// </para>
	/// <para>
	/// One store with a switched ambient tenant, not two stores. Two stores would let an implementation
	/// pass by instance separation with no tenant predicate at all, which tests the fixture rather than
	/// the contract.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task PurgeCompletedBeforeAsync_PurgesOnlyTheCallingTenantsSagas()
	{
		var ambient = new SwitchableTenantContext();
		var store = await CreateStoreAsync(ambient).ConfigureAwait(false);

		var owningSagaId = Guid.NewGuid();
		var otherSagaId = Guid.NewGuid();
		var completedAt = CompletionInstant;
		var threshold = CompletionInstant.AddHours(1);

		ambient.SwitchTo("conformance-tenant-a");
		await SaveCompletedSagaAsync(store, owningSagaId, completedAt).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-b");
		await SaveCompletedSagaAsync(store, otherSagaId, completedAt).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-a");
		var purged = await store.PurgeCompletedBeforeAsync(threshold, CancellationToken.None).ConfigureAwait(false);

		// LIVENESS: the confined purge must actually do its job for the tenant that called it.
		purged.ShouldBe(
			1,
			"the calling tenant had exactly one overdue saga; a purge that reports 0 is inert, and a purge "
			+ "that reports 2 swept another tenant's data");

		ambient.SwitchTo("conformance-tenant-a");
		var owning = await store.LoadAsync<RetentionSagaState>(owningSagaId, CancellationToken.None).ConfigureAwait(false);
		owning.ShouldBeNull("the calling tenant's overdue saga must have been deleted");

		// SAFETY: the other tenant's identically-overdue saga must be untouched.
		ambient.SwitchTo("conformance-tenant-b");
		var other = await store.LoadAsync<RetentionSagaState>(otherSagaId, CancellationToken.None).ConfigureAwait(false);
		_ = other.ShouldNotBeNull(
			"another tenant's saga was equally overdue and must survive a confined purge; a purge missing "
			+ "its tenant term deletes the whole estate and reports success");
	}

	/// <summary>Resolves the reserved untenanted partition — a concrete term, never an absent one.</summary>
	private sealed class UntenantedContext : ITenantContext
	{
		public string? TenantId => TenantScope.UntenantedSentinel;

		public bool HasTenant => true;
	}

	/// <summary>An ambient tenant context whose resolved tenant the arm controls.</summary>
	private sealed class SwitchableTenantContext : ITenantContext
	{
		public string? TenantId { get; private set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);

		public void SwitchTo(string tenantId) => TenantId = tenantId;
	}

	private Task SaveCompletedSagaAsync(Guid sagaId, DateTimeOffset completedAt) =>
		SaveCompletedSagaAsync(Store, sagaId, completedAt);

	private static async Task SaveCompletedSagaAsync(ISagaStore store, Guid sagaId, DateTimeOffset completedAt)
	{
		await store.SaveAsync(
			new RetentionSagaState
			{
				SagaId = sagaId,
				Completed = true,
				CompletedAt = completedAt,
				Payload = "retention-conformance"
			},
			CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>A minimal completed saga. The only field under test is the one on <see cref="SagaState"/>.</summary>
	private sealed class RetentionSagaState : SagaState
	{
		public string Payload { get; set; } = string.Empty;
	}
}
