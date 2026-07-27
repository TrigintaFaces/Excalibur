// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

// bd-bh40cy (S887 REVIEW_CODE BLOCKING 1) — independent (author != implementer, TestsDeveloper) regression lock for
// the CDC mid-batch-demotion split-brain checkpoint bug, SA-ruled seam (msg 30049).
//
// THE DEFECT (committed HEAD). The fencing token was re-read PER checkpoint write via a live accessor
// (`() => _leaderElection.CurrentLeadership?.FencingToken`, CdcProcessor.cs:174). A demotion mid-batch — a
// lease-loss / GC-pause between the batch-START leadership gate (CdcProcessor.cs:245) and the batch-END checkpoint
// write (CdcChangeApplier.cs:284) — flipped that accessor to NULL. The state-store MERGE guard
// `WHEN MATCHED AND (@fencingToken IS NULL OR ...)` then evaluated UNCONDITIONALLY TRUE, so the demoted zombie's
// stale/divergent LSN write LANDED, and the manager guard `if (fencingToken.HasValue && rowsWritten == 0)` was
// FALSE (HasValue == false) → NO CdcLeadershipSupersededException. Undetected split-brain checkpoint corruption.
//
// THE FIX (SA seam, msg 30049 — make "null-means-demoted" inexpressible once fenced). The tenure token is captured
// ONCE at the batch-start gate (where CurrentLeadership is proven non-null when fencing is configured) and PINNED
// for the whole batch via `CdcCheckpointManager.SetBatchFencingToken(pinnedToken)`; every checkpoint write presents
// that pinned value, never a live re-read. `_leaderElection is null` → pinned `null` is the ONLY legitimate unfenced
// path. A mid-batch demotion does NOT mutate the pinned token, so the now-STALE pinned token loses the non-decreasing
// CAS (0 rows) and the `HasValue && rowsWritten == 0` guard fires → CdcLeadershipSupersededException. Correct stop.
//
// SEAM. This binds CdcCheckpointManager (the class that owns SetBatchFencingToken + the throw). The state store is a
// fake whose returned row-count is the INPUT to the manager's guard (the real MERGE CAS — @fencingToken semantics —
// is CdcStateStore's separate concern; here we assert the manager presents the PINNED token and throws on a fenced
// 0-row write). Non-vacuity is the captured-token assertion: a mutant that presents a live/null token instead of the
// pinned value (the pre-fix bug) fails BOTH the throw arm (null → HasValue false → no throw) AND the
// presented-token arms — so the lock genuinely catches the demotion-null bypass, not merely the happy path.
//
// SAFETY + LIVENESS (testing-patterns §3):
//   SAFETY  (regression) — a fenced batch whose checkpoint write loses the CAS (0 rows) → throws superseded, and the
//     store was presented the PINNED non-null token, never null. RED on the present-null mutant.
//   PINNED-NOT-LIVE — across TWO writes in one batch (a mid-batch demotion happens between them) the SAME pinned
//     token is presented both times; a null is NEVER presented. This is the exact property the live accessor broke.
//   LIVENESS — a fenced write that WINS the CAS (rowsWritten > 0) does NOT throw; the guard rejects only losers.
//   UNFENCED (single-instance preserved) — with a pinned NULL token (no leader election) a 0-row write does NOT
//     throw: the only legitimate unfenced path must not be turned into a false supersede.
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class CdcCheckpointManagerPinnedFencingTokenShould
{
	private static readonly byte[] Lsn = [0, 0, 0, 0, 0, 0, 0, 1];

	[Fact]
	public async Task Throw_superseded_presenting_the_pinned_token_when_a_fenced_write_loses_the_cas()
	{
		// SAFETY (regression). A newer leader holds a higher stored token, so the demoted instance's write loses the
		// non-decreasing CAS → the fake returns 0 rows. The manager must throw superseded — AND must have presented
		// the PINNED token (5), not a live-re-read null.
		var store = A.Fake<ISqlServerCdcStateStore>();
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.Returns(0);

		var manager = CreateManager(store);
		manager.SetBatchFencingToken(5L);

		_ = await Should.ThrowAsync<CdcLeadershipSupersededException>(
			async () => await manager.UpdateTableLastProcessedAsync("dbo_orders", Lsn, sequenceValue: null, commitTime: null, CancellationToken.None),
			"a fenced checkpoint write that loses the CAS (0 rows) is a demoted split-brain leader and MUST throw " +
			"CdcLeadershipSupersededException — the pinned non-null token makes the state-store guard fire, unlike the " +
			"pre-fix null which bypassed it.");

		// Non-vacuity: the store was presented the PINNED token 5, never null. A present-null mutant fails here AND
		// (fatally) never throws above.
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, 5L, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Present_the_same_pinned_token_across_writes_even_after_a_mid_batch_demotion()
	{
		// PINNED-NOT-LIVE — the exact property the live `CurrentLeadership?.FencingToken` accessor broke. The token is
		// pinned ONCE at batch start; a mid-batch demotion (which flips the live accessor to null) must NOT change the
		// value presented on a later write in the same batch. Both writes win the CAS here (fake → 1), so no throw.
		var store = A.Fake<ISqlServerCdcStateStore>();
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.Returns(1);

		var manager = CreateManager(store);
		manager.SetBatchFencingToken(7L);

		await manager.UpdateTableLastProcessedAsync("dbo_orders", Lsn, sequenceValue: null, commitTime: null, CancellationToken.None);
		// ...mid-batch demotion happens here (CurrentLeadership → null); the pinned token must be unaffected...
		await manager.UpdateTableLastProcessedAsync("dbo_items", Lsn, sequenceValue: null, commitTime: null, CancellationToken.None);

		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, 7L, A<CancellationToken>._))
			.MustHaveHappenedTwiceExactly();

		// A live re-read would have presented null on the post-demotion write — that must NEVER happen for a fenced batch.
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, (long?)null, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Not_throw_when_a_fenced_write_wins_the_cas()
	{
		// LIVENESS — the guard must reject ONLY losers. A fenced write that wins the CAS (rowsWritten > 0) proceeds
		// normally. Without this arm, a mutant that throws superseded on every fenced write would pass the safety arm.
		var store = A.Fake<ISqlServerCdcStateStore>();
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.Returns(1);

		var manager = CreateManager(store);
		manager.SetBatchFencingToken(9L);

		await Should.NotThrowAsync(
			async () => await manager.UpdateTableLastProcessedAsync("dbo_orders", Lsn, sequenceValue: null, commitTime: null, CancellationToken.None),
			"a fenced checkpoint write that WINS the CAS (rows > 0) is the current leader and must proceed — the " +
			"supersede guard fires only when the write loses (0 rows).");
	}

	[Fact]
	public async Task Not_supersede_an_unfenced_single_instance_write_even_on_zero_rows()
	{
		// UNFENCED (single-instance preserved). `_leaderElection is null` pins a NULL token — the ONLY legitimate
		// unfenced path. A 0-row write there is NOT a supersede (HasValue is false); turning it into one would break
		// every non-fencing (single-instance) deployment. This is the arm that stops the safety fix over-reaching.
		var store = A.Fake<ISqlServerCdcStateStore>();
		A.CallTo(() => store.UpdateLastProcessedPositionAsync(
				A<string>._, A<string>._, A<string>._, A<byte[]>._, A<byte[]?>._, A<DateTime?>._, A<long?>._, A<CancellationToken>._))
			.Returns(0);

		var manager = CreateManager(store);
		manager.SetBatchFencingToken(null);

		await Should.NotThrowAsync(
			async () => await manager.UpdateTableLastProcessedAsync("dbo_orders", Lsn, sequenceValue: null, commitTime: null, CancellationToken.None),
			"an UNFENCED (single-instance) write pins a null token; a 0-row result there must NOT be treated as a " +
			"leadership supersede — the guard fires only when a real fencing token is present.");
	}

	private static CdcCheckpointManager CreateManager(ISqlServerCdcStateStore store) =>
		new(A.Fake<IDatabaseOptions>(), A.Fake<ICdcRepository>(), store, NullLogger.Instance);
}
