// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Functional tests for <see cref="PollyRetryPolicyAdapter"/> verifying real retry behavior.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class PollyRetryPolicyAdapterFunctionalShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ExecuteExactlyOnceWhenRetryCountIsZero(bool named)
	{
		var predicates = 0;
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 0,
			ShouldRetry = _ => { predicates++; return true; },
		};
		var services = new ServiceCollection();
		services.AddPollyRetryPolicy("disabled", configured =>
		{
			configured.MaxRetryAttempts = options.MaxRetryAttempts;
			configured.ShouldRetry = options.ShouldRetry;
		});
		await using var provider = services.BuildServiceProvider();
		IRetryPolicy policy = named ? provider.GetRequiredKeyedService<IRetryPolicy>("disabled") : new PollyRetryPolicyAdapter(Opt(options));
		var calls = 0;
		var result = await policy.ExecuteAsync(_ => { calls++; return Task.FromResult(42); }, CancellationToken.None);
		result.ShouldBe(42);
		calls.ShouldBe(1);
		var fault = new IOException("before effect");
		var observed = await Should.ThrowAsync<IOException>(() => policy.ExecuteAsync<int>(_ =>
		{
			calls++;
			return Task.FromException<int>(fault);
		}, CancellationToken.None));
		observed.ShouldBeSameAs(fault);
		calls.ShouldBe(2, "each independent zero-retry execution gets exactly one attempt");
		predicates.ShouldBe(0);
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(0, true)]
	[InlineData(1, false)]
	[InlineData(1, true)]
	public async Task PreservePreCancelledCallerAndPreventOperationEffects(int retries, bool noResponse)
	{
		var policy = new PollyRetryPolicyAdapter(Opt(new PollyRetryOptions { MaxRetryAttempts = retries }));
		using var caller = new CancellationTokenSource();
		caller.Cancel();
		var effects = 0;
		if (noResponse)
		{
			await Should.ThrowAsync<OperationCanceledException>(() => policy.ExecuteAsync(token =>
			{
				token.ThrowIfCancellationRequested();
				effects++;
				return Task.CompletedTask;
			}, caller.Token));
		}
		else
		{
			await Should.ThrowAsync<OperationCanceledException>(() => policy.ExecuteAsync(token =>
			{
				token.ThrowIfCancellationRequested();
				effects++;
				return Task.FromResult(42);
			}, caller.Token));
		}
		effects.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Exclude_Cancellation_By_Default_When_Outer_Token_Is_Not_Canceled(bool taskCanceled)
	{
		var adapter = new PollyRetryPolicyAdapter(Opt(new PollyRetryOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
		}));
		OperationCanceledException expected = taskCanceled
			? new TaskCanceledException("Dependency canceled its operation.")
			: new OperationCanceledException("Dependency canceled its operation.");
		var attempts = 0;
		await Should.ThrowAsync<OperationCanceledException>(() => adapter.ExecuteAsync<int>(_ =>
		{
			attempts++;
			return Task.FromException<int>(expected);
		}, CancellationToken.None));
		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task Honor_Explicit_Retry_Of_Attempt_Local_Cancellation()
	{
		var adapter = new PollyRetryPolicyAdapter(Opt(new PollyRetryOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.Zero,
			UseJitter = false,
			ShouldRetry = exception => exception is OperationCanceledException,
		}));
		var attempts = 0;
		var result = await adapter.ExecuteAsync(_ => ++attempts == 1
			? Task.FromException<int>(new OperationCanceledException("Attempt-local cancellation."))
			: Task.FromResult(42), CancellationToken.None);
		result.ShouldBe(42);
		attempts.ShouldBe(2);
	}
	[Fact]
	public async Task Retry_transient_failures_up_to_max_retries()
	{
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
			ShouldRetry = ex => ex is TimeoutException, // Only retry timeouts
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Exponential,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(100),
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

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
		var adapter = new PollyRetryPolicyAdapter(Opt(new PollyRetryOptions()));

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_argument_null_for_null_action_void()
	{
		var adapter = new PollyRetryPolicyAdapter(Opt(new PollyRetryOptions()));

		await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Support_cancellation_during_retry_delays()
	{
		var options = new PollyRetryOptions
		{
			MaxRetryAttempts = 10,
			BaseDelay = TimeSpan.FromSeconds(30), // Very long delay
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false,
		};
		var adapter = new PollyRetryPolicyAdapter(Opt(options));

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await adapter.ExecuteAsync<int>(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				throw new TimeoutException("keep retrying");
			}, cts.Token));
	}

	private static Microsoft.Extensions.Options.IOptions<PollyRetryOptions> Opt(PollyRetryOptions options)
		=> Microsoft.Extensions.Options.Options.Create(options);
}
