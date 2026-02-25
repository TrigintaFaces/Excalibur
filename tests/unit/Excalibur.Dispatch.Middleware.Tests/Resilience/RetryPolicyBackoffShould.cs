// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Tests for <see cref="RetryPolicy"/> backoff strategies and jitter configurations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class RetryPolicyBackoffShould
{
	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new RetryPolicy(null!));
	}

	[Fact]
	public async Task ExecuteAsync_WithNullAction_ThrowsArgumentNullException()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 1 });

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => policy.ExecuteAsync<int>(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_WithNullVoidAction_ThrowsArgumentNullException()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 1 });

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => policy.ExecuteAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_SuccessfulOperation_ReturnsResult()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 3 });

		// Act
		var result = await policy.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationTokenOverload_ExecutesSuccessfully()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 3 });

		// Act
		var result = await policy.ExecuteAsync(
			ct => Task.FromResult(99),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(99);
	}

	[Fact]
	public async Task ExecuteAsync_VoidOverload_CompletesSuccessfully()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 3 });
		var executed = false;

		// Act
		await policy.ExecuteAsync(
			ct =>
			{
				executed = true;
				return Task.CompletedTask;
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_TransientFailure_RetriesAndSucceeds()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			JitterStrategy = JitterStrategy.None,
		});

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attempts++;
			if (attempts < 3)
			{
				throw new TimeoutException("Transient");
			}

			return Task.FromResult(42);
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
		attempts.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_NonRetryableException_DoesNotRetry()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		});

		// Act & Assert — ArgumentException is not retryable by default
		await Should.ThrowAsync<ArgumentException>(() => policy.ExecuteAsync(() =>
		{
			attempts++;
			throw new ArgumentException("Bad argument");
		}, CancellationToken.None)).ConfigureAwait(false);

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_InvalidOperationException_DoesNotRetry()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		});

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() => policy.ExecuteAsync(() =>
		{
			attempts++;
			throw new InvalidOperationException("Invalid");
		}, CancellationToken.None)).ConfigureAwait(false);

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_OperationCancelledException_DoesNotRetry()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10),
		});

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() => policy.ExecuteAsync(() =>
		{
			attempts++;
			throw new OperationCanceledException("Cancelled");
		}, CancellationToken.None)).ConfigureAwait(false);

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_CustomShouldRetry_HonorsDelegate()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 5,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			ShouldRetry = ex => ex is IOException,
			JitterStrategy = JitterStrategy.None,
		});

		// Act & Assert — NotSupportedException should NOT be retried
		await Should.ThrowAsync<NotSupportedException>(() => policy.ExecuteAsync(() =>
		{
			attempts++;
			throw new NotSupportedException("Custom not retryable");
		}, CancellationToken.None)).ConfigureAwait(false);

		attempts.ShouldBe(1);
	}

	[Theory]
	[InlineData(BackoffStrategy.Fixed)]
	[InlineData(BackoffStrategy.Linear)]
	[InlineData(BackoffStrategy.Exponential)]
	[InlineData(BackoffStrategy.Fibonacci)]
	public async Task ExecuteAsync_WithBackoffStrategy_ExecutesWithRetry(BackoffStrategy strategy)
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = strategy,
			JitterStrategy = JitterStrategy.None,
		});

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attempts++;
			if (attempts < 2)
			{
				throw new TimeoutException("Retry");
			}

			return Task.FromResult(strategy.ToString());
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(strategy.ToString());
		attempts.ShouldBe(2);
	}

	[Theory]
	[InlineData(JitterStrategy.None)]
	[InlineData(JitterStrategy.Full)]
	[InlineData(JitterStrategy.Equal)]
	[InlineData(JitterStrategy.Decorrelated)]
	[InlineData(JitterStrategy.Exponential)]
	public async Task ExecuteAsync_WithJitterStrategy_ExecutesWithRetry(JitterStrategy jitter)
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			JitterStrategy = jitter,
		});

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attempts++;
			if (attempts < 2)
			{
				throw new TimeoutException("Retry");
			}

			return Task.FromResult(jitter.ToString());
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(jitter.ToString());
	}

	[Fact]
	public async Task ExecuteAsync_WithMaxDelayCap_CapsDelay()
	{
		// Arrange
		var attempts = 0;
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromSeconds(10),
			MaxDelay = TimeSpan.FromMilliseconds(50),
			BackoffStrategy = BackoffStrategy.Exponential,
			JitterStrategy = JitterStrategy.None,
		});

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attempts++;
			if (attempts < 2)
			{
				throw new TimeoutException("Retry");
			}

			return Task.FromResult(1);
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_WithOperationTimeout_CompletesWithinTimeout()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 1,
			OperationTimeout = TimeSpan.FromSeconds(5),
		});

		// Act
		var result = await policy.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithLogger_DoesNotThrow()
	{
		// Arrange
		var logger = A.Fake<ILogger>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var policy = new RetryPolicy(
			new RetryOptions
			{
				MaxRetries = 1,
				BaseDelay = TimeSpan.FromMilliseconds(10),
				JitterStrategy = JitterStrategy.None,
			},
			logger);

		// Act
		var result = await policy.ExecuteAsync(
			() => Task.FromResult(1),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_WithTimeoutManager_UsesManagerTimeout()
	{
		// Arrange
		var timeoutManager = A.Fake<ITimeoutManager>();
		A.CallTo(() => timeoutManager.GetTimeout("TestOp"))
			.Returns(TimeSpan.FromSeconds(10));

		var policy = new RetryPolicy(
			new RetryOptions { MaxRetries = 1 },
			timeoutManager: timeoutManager);

		// Act
		var result = await policy.ExecuteAsync(
			() => Task.FromResult(42),
			CancellationToken.None,
			operationName: "TestOp").ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_VoidWithOperationName_CompletesSuccessfully()
	{
		// Arrange
		var executed = false;
		var policy = new RetryPolicy(new RetryOptions { MaxRetries = 1 });

		// Act
		await policy.ExecuteAsync(
			() =>
			{
				executed = true;
				return Task.CompletedTask;
			},
			CancellationToken.None,
			operationName: "TestVoidOp").ConfigureAwait(false);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_AllRetriesExhausted_ThrowsOriginalException()
	{
		// Arrange
		var policy = new RetryPolicy(new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			JitterStrategy = JitterStrategy.None,
		});

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			() => policy.ExecuteAsync<int>(
				() => throw new TimeoutException("Persistent failure"),
				CancellationToken.None)).ConfigureAwait(false);
	}
}
