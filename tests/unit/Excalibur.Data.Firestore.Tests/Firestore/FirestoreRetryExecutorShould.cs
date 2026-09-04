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

		// DISPERSION is asserted in DrawEachBackoffFromARangeRatherThanAFixedValue, against the draw
		// itself. It used to be asserted here, from the elapsed wall time of 24 concurrent Task.Delay
		// calls, with the reasoning that "CPU load can only widen the observed spread, never narrow it".
		// A macOS runner produced a 4.95 ms spread where the draw is uniform over [0, 100] ms -- an
		// outcome with a probability around 1e-28 if the elapsed times tracked the draws, so they do
		// not: something in that environment collapses short delays toward each other. That makes the
		// elapsed time a proxy for the draw that the scheduler is free to break, and a property of a
		// pure function had been made to depend on the machine measuring it.
	}

	[Fact]
	public void DrawEachBackoffFromARangeRatherThanAFixedValue()
	{
		// The dispersion contract, asserted where it actually lives: the delay computation. A backoff
		// derived solely from the attempt number returns one value to every caller, so a herd that
		// collided once collides again on every wake. Drawing from a range is what separates them.
		const int herdSize = 24;

		var draws = Enumerable
			.Range(0, herdSize)
			.Select(_ => FirestoreRetryExecutor.NextDelay(TimeSpan.FromMilliseconds(100), attempt: 0).TotalMilliseconds)
			.ToArray();

		var spread = draws.Max() - draws.Min();

		// Uniform over [0, 100] with 24 draws: the expected spread is ~92 ms, and a spread below 20 ms
		// has a probability around 1e-28. This cannot flake on a slow machine because no clock is read.
		spread.ShouldBeGreaterThan(
			20d,
			$"each caller must draw its own backoff; observed draws: {string.Join(", ", draws.Order())}");

		// Liveness for the bound itself: a draw is inside the range it claims to sample, so the arm
		// above cannot be satisfied by a computation that returns wild values instead of dispersed ones.
		draws.ShouldAllBe(d => d >= 0 && d <= 100, "every draw must fall inside the ceiling it was given");
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
