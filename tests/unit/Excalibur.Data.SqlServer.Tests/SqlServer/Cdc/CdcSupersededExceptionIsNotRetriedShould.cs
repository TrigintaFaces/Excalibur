// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

// bd-cmww5i (S887 REVIEW_CODE P1-a) — independent (author != implementer, TestsDeveloper) regression lock: the CDC
// leadership-supersede signal must be NON-RETRYABLE end-to-end. Load-bearing for bh40cy — the pinned-token fix is
// worthless if the CdcLeadershipSupersededException it produces is then RETRIED by the batch resilience policy,
// because a demoted split-brain leader would just SPIN re-attempting the checkpoint write.
//
// THE SEAM. CdcChangeApplier wraps the checkpoint write in `batchPolicy.ExecuteAsync(...)` where
// `batchPolicy = _policyFactory.GetComprehensivePolicy()` (CdcChangeApplier.cs:182,290). The shipped policy
// (SqlDataAccessPolicyFactory.GetComprehensivePolicy = WrapAsync(circuitBreaker, waitAndRetry)) retries ONLY a
// transient allow-list: `Handle<SqlException>(IsTransient).Or<TimeoutException>().Or<InvalidOperationException>(…timeout…)`.
// CdcLeadershipSupersededException is a plain `Exception` (not in the allow-list), so it propagates on the FIRST
// attempt — the demoted leader stops instead of spinning. This lock binds "not retried" STRUCTURALLY rather than by
// a file:line read of the predicate: a future edit widening the allow-list to include the supersede exception (or
// making it derive from a handled type) re-introduces the spin and turns this RED.
//
// SAFETY + non-vacuity (testing-patterns §3):
//   SAFETY (no-spin) — an operation that throws CdcLeadershipSupersededException through the REAL comprehensive
//     policy is invoked EXACTLY ONCE and the exception propagates. RED on a mutant that adds the exception to the
//     retry allow-list (it would then be invoked retryCount+1 times).
//   LIVENESS (the policy really DOES retry) — a handled transient (TimeoutException) IS retried, so the once-only
//     result above is a real property of the exclusion, not a no-op policy that never retries anything. Without
//     this arm the safety assertion would pass even against a policy with retries disabled entirely.
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class CdcSupersededExceptionIsNotRetriedShould
{
	[Fact]
	public async Task Not_retry_a_leadership_supersede_signal_through_the_comprehensive_batch_policy()
	{
		// SAFETY (no-spin). The real shipped batch policy the CDC applier uses.
		var policy = CreateFactory().GetComprehensivePolicy();
		var invocations = 0;

		_ = await Should.ThrowAsync<CdcLeadershipSupersededException>(async () =>
			await policy.ExecuteAsync(async () =>
			{
				invocations++;
				await Task.Yield();
				throw new CdcLeadershipSupersededException();
			}));

		invocations.ShouldBe(
			1,
			"CdcLeadershipSupersededException is a terminal demotion signal — the batch resilience policy must NOT "
			+ "retry it. Retrying would spin a demoted split-brain leader re-attempting a checkpoint write that can "
			+ "only be rejected identically. A count > 1 means the retry allow-list was widened to include it "
			+ "(the exact P1-a regression this lock forbids).");
	}

	[Fact]
	public async Task Still_retry_a_handled_transient_so_the_no_spin_result_is_meaningful()
	{
		// LIVENESS / non-vacuity. A handled transient (TimeoutException) IS retried — proving the policy is a real
		// retry policy, so "supersede invoked exactly once" is a genuine exclusion and not a policy that never
		// retries. Fails on the first attempt then succeeds, so exactly one retry occurs (invocations == 2).
		var policy = CreateFactory().GetComprehensivePolicy();
		var invocations = 0;

		await policy.ExecuteAsync(async () =>
		{
			invocations++;
			await Task.Yield();
			if (invocations == 1)
			{
				throw new TimeoutException("transient — should be retried by the comprehensive policy");
			}
		});

		invocations.ShouldBe(
			2,
			"a handled transient (TimeoutException) MUST be retried by the comprehensive policy — this proves the "
			+ "policy genuinely retries, so the supersede exclusion above is a real property, not a no-op policy.");
	}

	private static SqlDataAccessPolicyFactory CreateFactory() =>
		new(NullLogger<SqlDataAccessPolicyFactory>.Instance);
}
