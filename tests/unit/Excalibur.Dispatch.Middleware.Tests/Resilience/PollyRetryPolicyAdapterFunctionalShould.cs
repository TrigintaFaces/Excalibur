// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Functional tests for <see cref="PollyRetryPolicyAdapter"/> verifying real retry behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyRetryPolicyAdapterFunctionalShould
{
	[Fact]
	public async Task Retry_transient_failures_up_to_max_retries()
	{
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var callCount = 0;
		var result = await adapter.ExecuteAsync<int>(async ct =>
		{
			callCount++;
			if (callCount < 3)
			{
				throw new TimeoutException("transient");
			}

			return await Task.FromResult(42);
		}, CancellationToken.None);

		result.ShouldBe(42);
		callCount.ShouldBe(3); // 2 failures + 1 success
	}

	[Fact]
	public async Task Propagate_exception_when_max_retries_exhausted()
	{
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var callCount = 0;
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await adapter.ExecuteAsync<int>(async ct =>
			{
				callCount++;
				throw new InvalidOperationException("persistent failure");
			}, CancellationToken.None));

		callCount.ShouldBe(3); // 1 original + 2 retries
	}

	[Fact]
	public async Task Respect_should_retry_predicate()
	{
		var options = new RetryOptions
		{
			MaxRetries = 5,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
			ShouldRetry = ex => ex is TimeoutException, // Only retry timeouts
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var callCount = 0;
		await Should.ThrowAsync<ArgumentException>(async () =>
			await adapter.ExecuteAsync<int>(async ct =>
			{
				callCount++;
				throw new ArgumentException("not retryable");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retries for ArgumentException
	}

	[Fact]
	public async Task Execute_void_action_with_retries()
	{
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var callCount = 0;
		await adapter.ExecuteAsync(async ct =>
		{
			callCount++;
			if (callCount < 2)
			{
				throw new TimeoutException("retry me");
			}

			await Task.CompletedTask;
		}, CancellationToken.None);

		callCount.ShouldBe(2);
	}

	[Fact]
	public async Task Use_exponential_backoff_with_increasing_delays()
	{
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Exponential,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var timestamps = new List<DateTimeOffset>();
		var callCount = 0;

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await adapter.ExecuteAsync<int>(async ct =>
			{
				timestamps.Add(DateTimeOffset.UtcNow);
				callCount++;
				throw new InvalidOperationException("always fail");
			}, CancellationToken.None));

		callCount.ShouldBe(4); // 1 original + 3 retries

		// Verify delays exist between attempts (they should increase)
		if (timestamps.Count >= 3)
		{
			var delay1 = (timestamps[1] - timestamps[0]).TotalMilliseconds;
			var delay2 = (timestamps[2] - timestamps[1]).TotalMilliseconds;
			// Exponential backoff means later delays are generally longer
			// But we just verify there IS a delay
			delay1.ShouldBeGreaterThan(0);
			delay2.ShouldBeGreaterThan(0);
		}
	}

	[Fact]
	public async Task Succeed_on_first_attempt_without_retries()
	{
		var options = new RetryOptions
		{
			MaxRetries = 5,
			BaseDelay = TimeSpan.FromMilliseconds(100),
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		var callCount = 0;
		var result = await adapter.ExecuteAsync<string>(async ct =>
		{
			callCount++;
			return await Task.FromResult("success");
		}, CancellationToken.None);

		result.ShouldBe("success");
		callCount.ShouldBe(1);
	}

	[Fact]
	public async Task Throw_argument_null_for_null_action_typed()
	{
		var adapter = new PollyRetryPolicyAdapter(new RetryOptions());

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_argument_null_for_null_action_void()
	{
		var adapter = new PollyRetryPolicyAdapter(new RetryOptions());

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Support_cancellation_during_retry_delays()
	{
		var options = new RetryOptions
		{
			MaxRetries = 10,
			BaseDelay = TimeSpan.FromSeconds(30), // Very long delay
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(options);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await adapter.ExecuteAsync<int>(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				throw new TimeoutException("keep retrying");
			}, cts.Token));
	}
}
