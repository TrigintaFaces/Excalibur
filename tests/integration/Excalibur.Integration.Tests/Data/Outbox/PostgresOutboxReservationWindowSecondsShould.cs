// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// g9ba5p — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-Postgres regression lock: the outbox
/// reservation window (<see cref="PostgresOutboxStoreOptions.ReservationTimeout"/>) is measured in <b>SECONDS</b>,
/// end to end. The reserve SQL casts the value to a <c>' seconds'</c> interval
/// (<c>dispatcher_timeout = NOW() + (@ReservationTimeout || ' seconds')::interval</c>); the bug shipped
/// <c>' milliseconds'</c>, making the effective window 1000× too short (a configured 300 = 5 min became 300 ms).
/// </summary>
/// <remarks>
/// Property, not mechanism: reserve a message with a small SECONDS window, then prove (a) it is still reserved
/// well after that many MILLISECONDS have passed, and (b) it becomes re-claimable once that many SECONDS have
/// passed. Both facts together pin the unit to seconds — the first fails if the unit is milliseconds, the second
/// fails if the window never elapses at all.
/// <para>
/// <b>SAFETY (RED on the pre-fix <c>' milliseconds'</c> SQL):</b> ~300 ms after reserving with a 2-<i>second</i>
/// window, the row must still be reserved. Under the bug the window is 2 <i>milliseconds</i>, long expired by
/// 300 ms, so the row is re-claimable → the assertion fails. <b>LIVENESS:</b> after the full 2 s window elapses
/// the row must come back to the pool — proving the window is a real (finite, seconds-scale) interval, not the
/// effectively-infinite 300000 s a naïve "keep the old 300_000 literal" flip would produce.
/// </para>
/// <para>
/// Determinism (testing-patterns §1): the window is a real Postgres <c>NOW()</c>-anchored timestamp and the
/// waits are real wall-clock <see cref="Task.Delay(TimeSpan, CancellationToken)"/>s, so both advance on the same
/// clock. Margins are generous — the safety check at 300 ms sits 150× past the 2 ms bug window and 6× short of
/// the 2 s fix window; the liveness check at ~3.3 s sits 1.3 s past the 2 s window.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxReservationWindowSecondsShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const int ReservationWindowSeconds = 2;

	private readonly PostgresOutboxStoreContainerFixture _fixture;

	public PostgresOutboxReservationWindowSecondsShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task HonorTheReservationWindowInSeconds_NotMilliseconds()
	{
		var store = await CreateStoreAsync(ReservationWindowSeconds).ConfigureAwait(false);
		const string messageId = "g9ba5p-reservation-window-seconds";
		await StageAsync(store, messageId).ConfigureAwait(false);

		// Reserve — sets dispatcher_timeout = NOW() + 2 SECONDS (under the fix) / + 2 MILLISECONDS (under the bug).
		var firstClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		firstClaim.ShouldContain(m => m.Id == messageId, "the first claim must reserve the staged message");

		// SAFETY — RED on the pre-fix ' milliseconds' SQL. 300 ms after reserving, a 2-SECOND window is still wide
		// open, so the row must NOT be re-claimable. Under the bug the window is 2 ms — expired 298 ms ago — so the
		// row would be re-claimable here and this assertion fails, catching the 1000×-off unit.
		await Task.Delay(TimeSpan.FromMilliseconds(300), CancellationToken.None).ConfigureAwait(false);
		var midClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		midClaim.ShouldNotContain(m => m.Id == messageId,
			"300 ms after reserving, the 2-SECOND reservation window is still open, so the message must NOT be "
			+ "re-claimable. If it is, the reserve SQL treated ReservationTimeout as MILLISECONDS (a 2 ms window, "
			+ "already expired) — the g9ba5p 1000×-off bug.");

		// LIVENESS. Once the full 2-second window elapses (+margin) the reservation expires and the row returns to
		// the pool — proving the window is a finite seconds-scale interval that actually elapses, not an
		// effectively-infinite 300000 s (which a "keep 300_000, it's just long" mis-flip would produce).
		await Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);
		var afterExpiry = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		afterExpiry.ShouldContain(m => m.Id == messageId,
			"after the 2-second reservation window elapses the message must be re-claimable for retry; if it never "
			+ "comes back the window is not measured in seconds at all (or is absurdly large).");
	}

	private static async Task StageAsync(IOutboxStore store, string messageId) =>
		await store.StageMessageAsync(
			new OutboundMessage { Id = messageId, MessageType = "T", Payload = [1], Destination = "dest" },
			CancellationToken.None).ConfigureAwait(false);

	private async Task<PostgresOutboxStore> CreateStoreAsync(int reservationWindowSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available — real-infra reservation-window unit lock is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new PostgresOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			// A SHORT window (seconds) so expiry is observable within the test — the seam under test is the UNIT.
			ReservationTimeout = reservationWindowSeconds,
			MaxAttempts = 3,
		});

		return new PostgresOutboxStore(db, options, NullLogger<PostgresOutboxStore>.Instance);
	}
}
