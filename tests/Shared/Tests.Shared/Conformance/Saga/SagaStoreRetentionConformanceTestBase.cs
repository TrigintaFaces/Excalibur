// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	public async ValueTask InitializeAsync() => Store = await CreateStoreAsync().ConfigureAwait(false);

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <summary>Creates the store under test.</summary>
	protected abstract Task<ISagaStore> CreateStoreAsync();

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

	private async Task SaveCompletedSagaAsync(Guid sagaId, DateTimeOffset completedAt)
	{
		await Store.SaveAsync(
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
