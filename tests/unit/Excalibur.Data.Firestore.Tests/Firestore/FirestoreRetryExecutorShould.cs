// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Data.Firestore;

using Grpc.Core;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreRetryExecutor"/>.
/// </summary>
/// <remarks>
/// The executor's callers arbitrate concurrent access to a SINGLE document, so its retry is only useful
/// if it separates racing callers from one another. A backoff computed solely from the attempt number
/// paces each caller correctly in isolation and still fails at the job, because every caller computes the
/// same number and they collide again on every wake. These arms bind the dispersion, not just the pause.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class FirestoreRetryExecutorShould : UnitTestBase
{
	private static RpcException Aborted() =>
		new(new Status(StatusCode.Aborted, "Transaction lock timeout."));

	[Fact]
	public async Task DisperseConcurrentRetriersRatherThanWakingThemTogether()
	{
		// Arrange — a herd of callers that each fail once with the contention status and then succeed, so
		// every one of them takes exactly one backoff. Each records how long its own backoff lasted.
		const int herdSize = 24;
		var waits = new ConcurrentBag<double>();

		async Task<bool> FailOnceThenSucceed(CancellationToken ct)
		{
			await Task.Yield();
			return true;
		}

		var tasks = new List<Task>(herdSize);

		for (var i = 0; i < herdSize; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				var attempted = false;
				var clock = Stopwatch.StartNew();

				_ = await FirestoreRetryExecutor.ExecuteAsync(
					async ct =>
					{
						if (!attempted)
						{
							attempted = true;
							clock.Restart();
							throw Aborted();
						}

						clock.Stop();
						return await FailOnceThenSucceed(ct).ConfigureAwait(false);
					},
					CancellationToken.None).ConfigureAwait(false);

				waits.Add(clock.Elapsed.TotalMilliseconds);
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert — LIVENESS: every caller got through. A backoff that dispersed the herd by starving it
		// would satisfy the spread assertion below on its own.
		waits.Count.ShouldBe(herdSize, "every retrying caller should complete");

		// Assert — DISPERSION: the waits must not all be the same value. A deterministic backoff puts every
		// caller's wait within scheduler noise of every other, so the spread collapses toward zero; drawing
		// from a range spreads them across it. The bound is far below the range the executor draws over and
		// far above scheduler noise, and CPU load can only widen the observed spread, never narrow it.
		var spread = waits.Max() - waits.Min();

		spread.ShouldBeGreaterThan(
			20d,
			$"concurrent retriers must not all wake at the same instant; observed waits: {string.Join(", ", waits.Order())}");
	}

	[Fact]
	public async Task ReturnTheResultWithoutRetryingWhenTheOperationSucceeds()
	{
		var calls = 0;

		var result = await FirestoreRetryExecutor.ExecuteAsync(
			ct =>
			{
				calls++;
				return Task.FromResult(42);
			},
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe(42);
		calls.ShouldBe(1, "a succeeding operation should be attempted exactly once");
	}

	[Fact]
	public async Task RetryATransientFailureAndReturnTheEventualResult()
	{
		var calls = 0;

		var result = await FirestoreRetryExecutor.ExecuteAsync(
			ct =>
			{
				calls++;
				return calls < 3 ? throw Aborted() : Task.FromResult(7);
			},
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe(7);
		calls.ShouldBe(3);
	}

	[Fact]
	public async Task SurfaceANonTransientFailureImmediately()
	{
		var calls = 0;

		_ = await Should.ThrowAsync<RpcException>(async () =>
			await FirestoreRetryExecutor.ExecuteAsync<bool>(
				ct =>
				{
					calls++;
					throw new RpcException(new Status(StatusCode.AlreadyExists, "exists"));
				},
				CancellationToken.None).ConfigureAwait(false));

		calls.ShouldBe(1, "a non-transient failure must not be retried");
	}

	[Fact]
	public async Task StopAfterTheBoundedNumberOfAttempts()
	{
		var calls = 0;

		var thrown = await Should.ThrowAsync<RpcException>(async () =>
			await FirestoreRetryExecutor.ExecuteAsync<bool>(
				ct =>
				{
					calls++;
					throw Aborted();
				},
				CancellationToken.None).ConfigureAwait(false));

		// The retry is bounded, and the failure that ends it reaches the caller as itself rather than as a
		// wrapper that hides which status ran out of patience.
		thrown.StatusCode.ShouldBe(StatusCode.Aborted);
		calls.ShouldBe(
			FirestoreRetryPolicy.Instance.MaxRetryAttempts + 1,
			"one initial attempt plus the policy's retries");
	}
}
