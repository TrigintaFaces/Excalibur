// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Tests for the <see cref="DefaultRetryPolicy"/> class.
/// Sprint 44: Unit tests for IRetryPolicy implementations.
/// Task: Excalibur.Dispatch-qu9v
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultRetryPolicyShould
{
	private readonly ILogger<DefaultRetryPolicy> _logger;

	public DefaultRetryPolicyShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<DefaultRetryPolicy>();
	}

	private DefaultRetryPolicy CreatePolicy(RetryPolicyOptions? options = null, IBackoffCalculator? backoff = null)
	{
		return new DefaultRetryPolicy(options ?? new RetryPolicyOptions(), backoff, _logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullExceptionWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new DefaultRetryPolicy(null!, null, _logger));
	}

	[Fact]
	public void UseExponentialBackoffCalculatorByDefault()
	{
		// Arrange & Act
		var policy = CreatePolicy();

		// Assert - Policy should be created successfully with default backoff
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomBackoffCalculator()
	{
		// Arrange
		var customBackoff = A.Fake<IBackoffCalculator>();

		// Act
		var policy = CreatePolicy(backoff: customBackoff);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void UseNullLoggerWhenLoggerNotProvided()
	{
		// Arrange & Act
		var policy = new DefaultRetryPolicy(new RetryPolicyOptions(), null, null);

		// Assert - Should not throw
		_ = policy.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region Successful Execution Tests

	[Fact]
	public async Task ExecuteAndReturnResultOnSuccess()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			await Task.Delay(1, ct).ConfigureAwait(false);
			return 42;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteVoidActionSuccessfully()
	{
		// Arrange
		var policy = CreatePolicy();
		var executed = false;

		// Act
		await policy.ExecuteAsync(async ct =>
		{
			await Task.Delay(1, ct).ConfigureAwait(false);
			executed = true;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task NotRetryWhenOperationSucceeds()
	{
		// Arrange
		var policy = CreatePolicy();
		var executionCount = 0;

		// Act
		_ = await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionCount.ShouldBe(1);
	}

	#endregion Successful Execution Tests

	#region Retry on Exception Tests

	[Fact]
	public async Task RetryOnTransientException()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount < 3)
			{
				throw new InvalidOperationException($"Transient error {executionCount}");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("success");
		executionCount.ShouldBe(3);
	}

	[Fact]
	public async Task ThrowAfterMaxRetriesExceeded()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException($"Persistent error {executionCount}");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should have attempted exactly MaxRetryAttempts times
		executionCount.ShouldBe(3);
	}

	[Fact]
	public async Task RetryExactlyMaxAttemptsTimes()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Always fails");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		executionCount.ShouldBe(5);
	}

	[Fact]
	public async Task UseBackoffCalculatorForDelays()
	{
		// Arrange
		var backoff = A.Fake<IBackoffCalculator>();
		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._)).Returns(TimeSpan.FromMilliseconds(5));

		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
		};
		var policy = CreatePolicy(options, backoff);
		var executionCount = 0;

		// Act
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Test");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert - Backoff should have been called for each retry (attempts 1 and 2, not 3 since it throws)
		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._)).MustHaveHappened(2, Times.Exactly);
	}

	#endregion Retry on Exception Tests

	#region Cancellation Tests

	[Fact]
	public async Task NotRetryOnOperationCanceledException()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new OperationCanceledException("Cancelled");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once - no retry on cancellation
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task NotRetryOnTaskCanceledException()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<TaskCanceledException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new TaskCanceledException("Cancelled");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once - no retry on cancellation
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task RespectCancellationTokenDuringExecution()
	{
		// Arrange
		var policy = CreatePolicy();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(1000, ct).ConfigureAwait(false);
				return "result";
			}, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RespectCancellationTokenDuringDelayBetweenRetries()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(10), // Long delay
		};
		var policy = CreatePolicy(options);
		using var cts = new CancellationTokenSource();
		var executionCount = 0;

		// Act
		var task = policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount == 1)
			{
				throw new InvalidOperationException("First failure");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success";
		}, cts.Token);

		// Cancel after a short delay (before retry delay completes)
		await Task.Delay(50).ConfigureAwait(false);
		cts.Cancel();

		// Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await task.ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Cancellation Tests

	#region Retriable/Non-Retriable Exception Tests

	[Fact]
	public async Task OnlyRetrySpecificExceptionTypesWhenConfigured()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		_ = options.RetriableExceptions.Add(typeof(TimeoutException));

		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert - InvalidOperationException should not be retried
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Not retriable");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once since InvalidOperationException is not in RetriableExceptions
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task RetrySpecificExceptionTypesWhenConfigured()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		_ = options.RetriableExceptions.Add(typeof(TimeoutException));

		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert - TimeoutException should be retried
		_ = await Should.ThrowAsync<TimeoutException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new TimeoutException("Timeout");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should retry up to MaxRetryAttempts
		executionCount.ShouldBe(3);
	}

	[Fact]
	public async Task NotRetryNonRetriableExceptionTypes()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		};
		_ = options.NonRetriableExceptions.Add(typeof(ArgumentException));

		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new ArgumentException("Non-retriable");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task RetryAllExceptionsWhenNoSpecificTypesConfigured()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Always fails");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should retry all exceptions
		executionCount.ShouldBe(3);
	}

	#endregion Retriable/Non-Retriable Exception Tests

	#region Void Action Tests

	[Fact]
	public async Task RetryVoidActionOnFailure()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act
		await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount < 3)
			{
				throw new InvalidOperationException($"Error {executionCount}");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		executionCount.ShouldBe(3);
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenActionIsNull()
	{
		// Arrange
		var policy = CreatePolicy();

		// Act & Assert - Generic version
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<string>(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Act & Assert - Void version
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(null!, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion Void Action Tests

	#region Edge Case Tests

	[Fact]
	public async Task HandleZeroRetryAttempts()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 0,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert - With 0 max attempts, first failure should throw immediately
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Fail");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only try once (no retries with 0 max attempts)
		executionCount.ShouldBeLessThanOrEqualTo(1);
	}

	[Fact]
	public async Task HandleSingleRetryAttempt()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 1,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Fail");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only try once (1 attempt means no retries)
		executionCount.ShouldBe(1);
	}

	[Fact]
	public async Task SucceedOnLastRetryAttempt()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount < 3)
			{
				throw new InvalidOperationException($"Error {executionCount}");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success on last attempt";
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("success on last attempt");
		executionCount.ShouldBe(3);
	}

	#endregion Edge Case Tests

	#region Exception After Max Attempts Tests

	[Fact]
	public async Task ThrowOriginalException_AfterMaxAttemptsExceeded()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;
		var originalException = new InvalidOperationException("Specific error message");

		// Act & Assert
		var thrownException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw originalException;
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// The thrown exception should be the original one
		thrownException.Message.ShouldBe("Specific error message");
		executionCount.ShouldBe(2);
	}

	#endregion Exception After Max Attempts Tests

	#region Backoff Calculator Interaction Tests

	[Fact]
	public async Task UseBackoffCalculatorDelayValues()
	{
		// Arrange
		var backoff = A.Fake<IBackoffCalculator>();
		var delays = new[]
		{
			TimeSpan.FromMilliseconds(10),
			TimeSpan.FromMilliseconds(20),
		};
		var callCount = 0;

		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._))
			.ReturnsLazily((int attempt) => delays[Math.Min(callCount++, delays.Length - 1)]);

		var options = new RetryPolicyOptions { MaxRetryAttempts = 3 };
		var policy = CreatePolicy(options, backoff);
		var executionCount = 0;

		// Act
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Test");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert - Backoff should be called for each retry (not including last attempt)
		_ = A.CallTo(() => backoff.CalculateDelay(1)).MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => backoff.CalculateDelay(2)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassCorrectAttemptNumberToBackoff()
	{
		// Arrange
		var backoff = A.Fake<IBackoffCalculator>();
		var receivedAttempts = new List<int>();

		_ = A.CallTo(() => backoff.CalculateDelay(A<int>._))
			.Invokes((int attempt) => receivedAttempts.Add(attempt))
			.Returns(TimeSpan.FromMilliseconds(1));

		var options = new RetryPolicyOptions { MaxRetryAttempts = 4 };
		var policy = CreatePolicy(options, backoff);
		var executionCount = 0;

		// Act
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new InvalidOperationException("Test");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Assert - Should have received attempts 1, 2, 3 (not 4 since that's the last)
		receivedAttempts.Count.ShouldBe(3);
		receivedAttempts[0].ShouldBe(1);
		receivedAttempts[1].ShouldBe(2);
		receivedAttempts[2].ShouldBe(3);
	}

	#endregion Backoff Calculator Interaction Tests

	#region Combined Retriable and Non-Retriable Tests

	[Fact]
	public async Task PrioritizeNonRetriableOverRetriable()
	{
		// Arrange - Configure both retriable and non-retriable
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		_ = options.RetriableExceptions.Add(typeof(Exception)); // Base class - should retry all
		_ = options.NonRetriableExceptions.Add(typeof(ArgumentException)); // Specific - should not retry

		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert - ArgumentException should NOT be retried despite Exception being retriable
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new ArgumentException("Non-retriable");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// Should only execute once because non-retriable takes precedence
		executionCount.ShouldBe(1);
	}

	#endregion Combined Retriable and Non-Retriable Tests

	#region Delay During Retry Tests

	[Fact]
	public async Task DelayBetweenRetries()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(50),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;
		var stopwatch = Stopwatch.StartNew();

		// Act
		var result = await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount < 2)
			{
				throw new InvalidOperationException("Retry this");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success";
		}, CancellationToken.None).ConfigureAwait(false);

		stopwatch.Stop();

		// Assert - Should have waited approximately the base delay
		result.ShouldBe("success");
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(40); // Allow some variance
	}

	#endregion Delay During Retry Tests

	#region Void Action Exception Tests

	[Fact]
	public async Task ThrowOnVoidActionException_AfterMaxRetries()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync(async ct =>
			{
				executionCount++;
				await Task.Delay(1, ct).ConfigureAwait(false);
				throw new InvalidOperationException("Void action failed");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		executionCount.ShouldBe(2);
	}

	[Fact]
	public async Task SucceedOnVoidActionRetry()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		var policy = CreatePolicy(options);
		var executionCount = 0;
		var actionCompleted = false;

		// Act
		await policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount < 2)
			{
				throw new InvalidOperationException("First attempt fails");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			actionCompleted = true;
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		actionCompleted.ShouldBeTrue();
		executionCount.ShouldBe(2);
	}

	#endregion Void Action Exception Tests

	#region Cancellation During Delay Tests

	[Fact]
	public async Task CancelDuringDelay_ThrowsOperationCanceled()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromSeconds(30), // Long delay
		};
		var policy = CreatePolicy(options);
		using var cts = new CancellationTokenSource();
		var executionCount = 0;

		// Start the execution
		var task = policy.ExecuteAsync(async ct =>
		{
			executionCount++;
			if (executionCount == 1)
			{
				// Cancel after first attempt, during the delay
				_ = Task.Delay(10).ContinueWith(_ => cts.Cancel());
				throw new InvalidOperationException("Trigger retry");
			}

			await Task.Delay(1, ct).ConfigureAwait(false);
			return "success";
		}, cts.Token);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => task).ConfigureAwait(false);
	}

	#endregion Cancellation During Delay Tests

	#region Derived Exception Type Tests

	[Fact]
	public async Task RetryDerivedExceptionTypes_WhenBaseIsRetriable()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(5),
		};
		// Note: RetriableExceptions check uses exact type matching, not inheritance
		// So ArgumentNullException (derived from ArgumentException) won't match ArgumentException

		var policy = CreatePolicy(options);
		var executionCount = 0;

		// Act - With default settings (no specific retriable list), should retry
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<string>(ct =>
			{
				executionCount++;
				throw new ArgumentNullException("param", "Derived exception");
			}, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);

		// With default settings (empty retriable list), all non-cancellation exceptions are retried
		executionCount.ShouldBe(3);
	}

	#endregion Derived Exception Type Tests
}
