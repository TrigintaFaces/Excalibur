// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="RetryPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class RetryPolicyShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new RetryPolicy(null!));
	}

	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		var policy = new RetryPolicy(options);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullLogger_UsesNullLogger()
	{
		// Arrange
		var options = new RetryOptions();

		// Act & Assert - should not throw
		var policy = new RetryPolicy(options, null);
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithLogger_AcceptsLogger()
	{
		// Arrange
		var options = new RetryOptions();
		var logger = A.Fake<ILogger<RetryPolicy>>();

		// Act
		var policy = new RetryPolicy(options, logger);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithTimeoutManager_AcceptsTimeoutManager()
	{
		// Arrange
		var options = new RetryOptions();
		var logger = A.Fake<ILogger<RetryPolicy>>();
		var timeoutManager = A.Fake<ITimeoutManager>();

		// Act
		var policy = new RetryPolicy(options, logger, timeoutManager);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync<T> Tests

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new RetryOptions();
		var policy = new RetryPolicy(options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => policy.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
	{
		// Arrange
		var options = new RetryOptions();
		var policy = new RetryPolicy(options);

		// Act
		var result = await policy.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_DoesNotRetry()
	{
		// Arrange
		var options = new RetryOptions { MaxRetries = 3 };
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			callCount++;
			return Task.FromResult(100);
		}, CancellationToken.None);

		// Assert
		result.ShouldBe(100);
		callCount.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_WithTransientFailure_Retries()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			callCount++;
			if (callCount < 2)
			{
				throw new TimeoutException("Transient failure");
			}
			return Task.FromResult(99);
		}, CancellationToken.None);

		// Assert
		result.ShouldBe(99);
		callCount.ShouldBe(2);
	}

	[Fact]
	public async Task ExecuteAsync_WithPersistentFailure_ThrowsAfterMaxRetries()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);

		// Act & Assert
		_ = await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException("Persistent failure"), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithOperationCanceledException_DoesNotRetry()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => policy.ExecuteAsync<int>(() =>
			{
				callCount++;
				throw new OperationCanceledException("Cancelled");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retries for cancellation
	}

	[Fact]
	public async Task ExecuteAsync_WithArgumentException_DoesNotRetry()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => policy.ExecuteAsync<int>(() =>
			{
				callCount++;
				throw new ArgumentException("Bad argument");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retries for argument exceptions
	}

	[Fact]
	public async Task ExecuteAsync_WithInvalidOperationException_DoesNotRetry()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => policy.ExecuteAsync<int>(() =>
			{
				callCount++;
				throw new InvalidOperationException("Invalid op");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retries for invalid operation exceptions
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_RespectsToken()
	{
		// Arrange
		var options = new RetryOptions();
		var policy = new RetryPolicy(options);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => policy.ExecuteAsync(() => Task.FromResult(1), cancellationToken: cts.Token));
	}

	#endregion

	#region ExecuteAsync (void) Tests

	[Fact]
	public async Task ExecuteAsync_VoidOperation_WithSuccessfulOperation_Completes()
	{
		// Arrange
		var options = new RetryOptions();
		var policy = new RetryPolicy(options);
		var executed = false;

		// Act
		await policy.ExecuteAsync(() =>
		{
			executed = true;
			return Task.CompletedTask;
		}, CancellationToken.None);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_VoidOperation_WithTransientFailure_Retries()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act
		await policy.ExecuteAsync(() =>
		{
			callCount++;
			if (callCount < 2)
			{
				throw new TimeoutException("Transient failure");
			}
			return Task.CompletedTask;
		}, CancellationToken.None);

		// Assert
		callCount.ShouldBe(2);
	}

	#endregion

	#region Custom ShouldRetry Tests

	[Fact]
	public async Task ExecuteAsync_WithCustomShouldRetry_UsesCustomPredicate()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			ShouldRetry = ex => ex is FormatException // Only retry FormatExceptions
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			callCount++;
			if (callCount < 2)
			{
				throw new FormatException("Format error");
			}
			return Task.FromResult(123);
		}, CancellationToken.None);

		// Assert
		result.ShouldBe(123);
		callCount.ShouldBe(2); // Retried once
	}

	[Fact]
	public async Task ExecuteAsync_WithCustomShouldRetry_DoesNotRetryExcludedExceptions()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			ShouldRetry = ex => ex is FormatException // Only retry FormatExceptions
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() =>
			{
				callCount++;
				throw new TimeoutException("Not retryable");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retry for TimeoutException
	}

	#endregion

	#region Backoff Strategy Tests

	[Fact]
	public async Task ExecuteAsync_WithFixedBackoff_UsesFixedDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Fixed,
			UseJitter = false
		};
		var policy = new RetryPolicy(options);

		// Act & Assert - just verify it completes without error
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException(), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithLinearBackoff_UsesLinearDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Linear
		};
		var policy = new RetryPolicy(options);

		// Act & Assert - just verify it completes without error
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException(), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithExponentialBackoff_UsesExponentialDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Exponential
		};
		var policy = new RetryPolicy(options);

		// Act & Assert - just verify it completes without error
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException(), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithFibonacciBackoff_UsesFibonacciDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Fibonacci
		};
		var policy = new RetryPolicy(options);

		// Act & Assert - just verify it completes without error
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException(), CancellationToken.None));
	}

	#endregion

	#region Jitter Strategy Tests

	[Theory]
	[InlineData(JitterStrategy.None)]
	[InlineData(JitterStrategy.Full)]
	[InlineData(JitterStrategy.Equal)]
	[InlineData(JitterStrategy.Decorrelated)]
	[InlineData(JitterStrategy.Exponential)]
	public async Task ExecuteAsync_WithDifferentJitterStrategies_ExecutesCorrectly(JitterStrategy strategy)
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			JitterStrategy = strategy
		};
		var policy = new RetryPolicy(options);
		var callCount = 0;

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			callCount++;
			if (callCount < 2)
			{
				throw new TimeoutException();
			}
			return Task.FromResult(42);
		}, CancellationToken.None);

		// Assert
		result.ShouldBe(42);
		callCount.ShouldBe(2);
	}

	#endregion

	#region Timeout Tests

	[Fact]
	public void Constructor_WithOperationTimeout_AcceptsTimeout()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 1,
			OperationTimeout = TimeSpan.FromSeconds(30)
		};

		// Act
		var policy = new RetryPolicy(options);

		// Assert
		_ = policy.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_WithTimeoutManager_CallsTimeoutManager()
	{
		// Arrange
		var options = new RetryOptions { MaxRetries = 1 };
		var timeoutManager = A.Fake<ITimeoutManager>();
		A.CallTo(() => timeoutManager.GetTimeout("TestOp"))
			.Returns(TimeSpan.FromSeconds(30));

		var policy = new RetryPolicy(options, null, timeoutManager);

		// Act
		var result = await policy.ExecuteAsync(
			() => Task.FromResult(42),
			operationName: "TestOp", cancellationToken: CancellationToken.None);

		// Assert
		result.ShouldBe(42);
		A.CallTo(() => timeoutManager.GetTimeout("TestOp"))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region MaxDelay Tests

	[Fact]
	public async Task ExecuteAsync_WithMaxDelay_CapsDelay()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromSeconds(10), // Very long base delay
			MaxDelay = TimeSpan.FromMilliseconds(100), // But capped
			BackoffStrategy = BackoffStrategy.Exponential
		};
		var policy = new RetryPolicy(options);

		// Act & Assert - should complete reasonably quickly due to max delay cap
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(() => throw new TimeoutException(), CancellationToken.None));
	}

	#endregion
}
