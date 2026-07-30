// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// orna3m — independent (author≠impl) NON-SKIPPED real-Postgres regression lock: <c>MarkFailedAsync</c> must
/// <b>atomically unreserve</b> a failed message (clear <c>dispatcher_id</c>/<c>dispatcher_timeout</c>) so it
/// returns to the pool and is immediately re-claimable for retry — rather than staying pinned to the dead
/// dispatcher until the coarse reservation timeout elapses (the S698 leased-until-timeout bug).
/// </summary>
/// <remarks>
/// Property, not mechanism (per the ruling): reserve → fail → a fresh claim re-reserves it. The store is
/// built with a deliberately LONG <c>ReservationTimeout</c> (5 min) so the reservation cannot expire during
/// the test — the only way the message becomes re-claimable within the test window is the fix clearing the
/// lease.
/// <para>
/// <b>Non-vacuity / RED on the pre-fix impl:</b> the middle claim (after reserve, before fail) asserts the
/// message is NOT re-claimable while reserved — proving the reservation gate is live — so the final claim's
/// success is meaningful. On the pre-fix impl (<c>MarkFailedAsync</c> leaves <c>dispatcher_id</c> set) the
/// final claim excludes the still-leased row → RED. On the fix (lease cleared) it re-reserves → GREEN. A
/// second fact pins the exact column the fix changed.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxStoreMarkFailedUnreservesShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxStoreMarkFailedUnreservesShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ReturnAFailedMessageToThePool_SoAFreshClaimReReservesIt()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "orna3m-markfailed-unreserve";
		await StageAsync(store, messageId).ConfigureAwait(false);

		// Claim 1 — reserves the message (sets dispatcher_id + a 5-minute dispatcher_timeout).
		var firstClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		firstClaim.ShouldContain(m => m.Id == messageId, "the first claim must reserve the staged message");

		// Claim 2 (before fail) — the reservation gate MUST exclude the still-leased row. This proves the claim
		// query actually gates on reservation, so the final claim's success is a real re-reserve, not a store
		// that always returns everything.
		var secondClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		secondClaim.ShouldNotContain(m => m.Id == messageId,
			"a reserved message (dispatcher_timeout 5 min in the future) must not be re-claimable before it is failed/unreserved");

		// Fail it (below the retry ceiling — stays a retryable failed message, not dead-lettered).
		await store.MarkFailedAsync(messageId, "transient error", 1, CancellationToken.None).ConfigureAwait(false);

		// Claim 3 — the fix cleared the lease, so once the visibility floor elapses the failed message is
		// back in the pool and re-reservable. Polled, not slept: the floor is stamped from the SERVER clock,
		// so a fixed client-side delay would race it. The bound is generous relative to F (1s) and failing
		// it means the row never returned to the pool at all.
		//
		// RED on the pre-fix impl: dispatcher_id stayed set, so the row is excluded by the reservation gate
		// for the whole 5-minute ReservationTimeout and no amount of waiting inside this window recovers it.
		var reclaimed = await WaitForReclaimAsync(store, messageId, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
		reclaimed.ShouldBeTrue(
			"after MarkFailedAsync the message must be unreserved and re-claimable for retry once the failure "
			+ "floor elapses; if it is still leased to the failed dispatcher, MarkFailed did not clear "
			+ "dispatcher_id (the S698 bug)");
	}

	[Fact]
	public async Task ClearDispatcherReservationColumns_OnMarkFailed()
	{
		// Corroborating fact — pins the exact columns the fix clears (the mechanism behind the property above).
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string messageId = "orna3m-markfailed-columns";
		await StageAsync(store, messageId).ConfigureAwait(false);

		_ = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false); // reserve
		(await DispatcherIdAsync(messageId).ConfigureAwait(false))
			.ShouldNotBeNull("the claim must have reserved the row (dispatcher_id set)");

		await store.MarkFailedAsync(messageId, "transient error", 1, CancellationToken.None).ConfigureAwait(false);

		(await DispatcherIdAsync(messageId).ConfigureAwait(false))
			.ShouldBeNull("MarkFailedAsync must clear dispatcher_id so the message returns to the pool");
	}

	/// <summary>
	/// Polls the drain until <paramref name="messageId"/> is claimable, or the bound elapses.
	/// </summary>
	/// <remarks>
	/// The failure floor is anchored to the database's clock, so the moment it lifts is not knowable from
	/// here. Polling reaches the state as soon as it is true instead of guessing a delay that is either
	/// flaky or slow.
	/// </remarks>
	private static async Task<bool> WaitForReclaimAsync(IOutboxStore store, string messageId, TimeSpan within)
	{
		var deadline = DateTimeOffset.UtcNow + within;

		while (DateTimeOffset.UtcNow < deadline)
		{
			var claim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (claim.Any(m => m.Id == messageId))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None).ConfigureAwait(false);
		}

		return false;
	}

	private static async Task StageAsync(IOutboxStore store, string messageId) =>
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest" },
			CancellationToken.None).ConfigureAwait(false);

	private async Task<PostgresOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra MarkFailed-unreserve lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			// 5 minutes (ReservationTimeout is in SECONDS): the reservation cannot expire during the test, so the
			// only way the row becomes re-claimable is the fix clearing the lease on MarkFailed.
			ReservationTimeout = 300,
			MaxAttempts = 3,
			// The failure-anchored visibility floor F, at its 1-second minimum. A failed message is
			// deliberately NOT immediately re-claimable — MarkFailed stamps next_attempt_at = NOW() + F so
			// the plain failure path cannot hot-loop the drain. This lock is about the LEASE being cleared,
			// not about the floor being absent, so F is pinned to its smallest legal value and waited out
			// rather than left at the 30-second default (which would exclude the re-claim below entirely).
			FailureBackoffFloorSeconds = 1,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}

	private async Task<string?> DispatcherIdAsync(string messageId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);
		return await connection.ExecuteScalarAsync<string?>(
			$"SELECT dispatcher_id FROM {_fixture.SchemaName}.{_fixture.OutboxTableName} WHERE message_id = @Id",
			new { Id = messageId }).ConfigureAwait(false);
	}
}
