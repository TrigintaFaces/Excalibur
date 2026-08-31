// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

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
/// 300 ms, so the row is re-claimable → the assertion fails. <b>LIVENESS:</b> once the 2 s window elapses the
/// row must come back to the pool — proving the window is a real (finite, seconds-scale) interval, not the
/// effectively-infinite 300000 s a naïve "keep the old 300_000 literal" flip would produce.
/// </para>
/// <para>
/// <b>Determinism (testing-patterns §1) — why neither arm is a fixed sleep.</b> The window is a database-side
/// <c>NOW() + N SECONDS</c> deadline, and the two arms are fragile in OPPOSITE directions against it: safety
/// needs its check to land <i>before</i> expiry, liveness needs its check <i>after</i>. No single fixed delay
/// satisfies both under load, which is how this lock false-failed in a full CI shard run (the liveness arm —
/// the message had not returned within one fixed 3 s sleep). So:
/// <list type="bullet">
/// <item>
/// <b>Liveness POLLS</b> <see cref="IOutboxStore.GetUnsentMessagesAsync"/> until the message reappears, under a
/// bounded budget (<see cref="LivenessPollBudget"/>). A slow machine costs extra polls, never a red.
/// </item>
/// <item>
/// <b>Safety measures its own precondition.</b> It records the ACTUAL elapsed wall time from before the reserve
/// to after the check. That is a conservative upper bound on time-since-window-start (the window is anchored at
/// <c>NOW()</c> <i>during</i> the reserve, i.e. at or after the mark). If the row came back re-claimable but
/// that bound had already reached the window, the arm <b>could not discriminate</b> a 1000×-off unit from a
/// legitimately-expired window — so it fails as INCONCLUSIVE, naming that it could not run, rather than
/// reporting a defect that may not exist. It only reports the g9ba5p defect when it is certain the check landed
/// inside the window.
/// </item>
/// </list>
/// Both arms and both original assertion messages are intact — neither is relaxed into something that cannot
/// fail. Under the pre-fix <c>' milliseconds'</c> SQL the safety arm still goes RED (the ~350 ms upper bound is
/// far inside the 2 s intended window, so the inconclusive guard does not fire); under an absurdly-large window
/// the liveness arm still goes RED (nothing returns within the poll budget).
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Postgres")]
[Trait("Component", "Core")]
public sealed class PostgresOutboxReservationWindowSecondsShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const int ReservationWindowSeconds = 2;

	/// <summary> The configured reservation window, as the safety arm's discrimination threshold. </summary>
	private static readonly TimeSpan ReservationWindow = TimeSpan.FromSeconds(ReservationWindowSeconds);

	/// <summary> Nominal safety-arm wait: 150× past the 2 ms bug window, 6× short of the 2 s fix window. </summary>
	private static readonly TimeSpan SafetyCheckDelay = TimeSpan.FromMilliseconds(300);

	/// <summary>
	/// Bounded budget for the liveness poll — ~7× the 2 s window, so CI-load jitter costs polls, not a red.
	/// </summary>
	private static readonly TimeSpan LivenessPollBudget = TimeSpan.FromSeconds(15);

	private static readonly TimeSpan LivenessPollInterval = TimeSpan.FromMilliseconds(250);

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

		// Mark BEFORE the reserve is issued. The window is anchored at the server's NOW() *during* the reserve,
		// i.e. at or after this mark — so elapsed-from-here is a conservative UPPER bound on how much of the
		// window has burned, which is the direction that keeps the inconclusive guard honest.
		var sinceReserveIssued = Stopwatch.StartNew();

		// Reserve — sets dispatcher_timeout = NOW() + 2 SECONDS (under the fix) / + 2 MILLISECONDS (under the bug).
		var firstClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		firstClaim.ShouldContain(m => m.Id == messageId, "the first claim must reserve the staged message");

		// SAFETY — RED on the pre-fix ' milliseconds' SQL. 300 ms after reserving, a 2-SECOND window is still wide
		// open, so the row must NOT be re-claimable. Under the bug the window is 2 ms — expired 298 ms ago — so the
		// row would be re-claimable here and this assertion fails, catching the 1000×-off unit.
		await Task.Delay(SafetyCheckDelay, CancellationToken.None).ConfigureAwait(false);
		var midClaim = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();
		var safetyCheckElapsed = sinceReserveIssued.Elapsed;

		// The arm's own precondition: it can only distinguish "the unit is milliseconds" from "the window
		// legitimately expired while this machine was busy" if the check demonstrably landed INSIDE the window.
		// If it did not, refuse to render a verdict — an inconclusive arm must not be reported as a product defect.
		if (midClaim.Exists(m => m.Id == messageId) && safetyCheckElapsed >= ReservationWindow)
		{
			Assert.Fail(
				$"INCONCLUSIVE — the SAFETY arm could not run, and this is NOT a product-defect report. The "
				+ $"reserve→check round trip took {safetyCheckElapsed.TotalMilliseconds:F0} ms, which already "
				+ $"reaches the configured {ReservationWindow.TotalMilliseconds:F0} ms window, so a re-claimable "
				+ $"row here is equally explained by a legitimately-expired window (host stall / CI load) and by "
				+ $"the g9ba5p milliseconds bug. The arm cannot discriminate; re-run on a less loaded host. The "
				+ $"LIVENESS arm below is unaffected.");
		}

		midClaim.ShouldNotContain(m => m.Id == messageId,
			"300 ms after reserving, the 2-SECOND reservation window is still open, so the message must NOT be "
			+ "re-claimable. If it is, the reserve SQL treated ReservationTimeout as MILLISECONDS (a 2 ms window, "
			+ "already expired) — the g9ba5p 1000×-off bug. "
			+ $"(Measured reserve→check elapsed: {safetyCheckElapsed.TotalMilliseconds:F0} ms — well inside the "
			+ $"{ReservationWindow.TotalMilliseconds:F0} ms window, so this arm DID discriminate.)");

		// LIVENESS. Once the 2-second window elapses the reservation expires and the row returns to the pool —
		// proving the window is a finite seconds-scale interval that actually elapses, not an effectively-infinite
		// 300000 s (which a "keep 300_000, it's just long" mis-flip would produce). POLLED, not slept: the arm
		// needs its observation AFTER expiry, so a fixed sleep can only ever be too short under load — the failure
		// mode actually observed in CI. Extra latency costs polls; only a window that never elapses costs a red.
		var sincePollingStarted = Stopwatch.StartNew();
		var pollAttempts = 0;
		List<OutboundMessage> afterExpiry = [];

		while (sincePollingStarted.Elapsed < LivenessPollBudget)
		{
			pollAttempts++;
			afterExpiry = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
				.ToList();

			if (afterExpiry.Exists(m => m.Id == messageId))
			{
				break;
			}

			await Task.Delay(LivenessPollInterval, CancellationToken.None).ConfigureAwait(false);
		}

		afterExpiry.ShouldContain(m => m.Id == messageId,
			"after the 2-second reservation window elapses the message must be re-claimable for retry; if it never "
			+ "comes back the window is not measured in seconds at all (or is absurdly large). "
			+ $"(Polled {pollAttempts} time(s) over {sincePollingStarted.Elapsed.TotalSeconds:F1} s of a "
			+ $"{LivenessPollBudget.TotalSeconds:F0} s budget — {ReservationWindow.TotalSeconds:F0} s was the "
			+ $"configured window, so this is not impatience.)");
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
