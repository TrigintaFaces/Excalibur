// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="PollyRetryPolicyAdapter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class PollyRetryPolicyAdapterShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PollyRetryPolicyAdapter(null!));
	}

	[Fact]
	public void Constructor_WithValidOptions_CreatesInstance()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		var adapter = new PollyRetryPolicyAdapter(options);

		// Assert
		_ = adapter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullLogger_UsesNullLogger()
	{
		// Arrange
		var options = new RetryOptions();

		// Act & Assert - should not throw
		var adapter = new PollyRetryPolicyAdapter(options, null);
		_ = adapter.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithLogger_AcceptsLogger()
	{
		// Arrange
		var options = new RetryOptions();
		var logger = A.Fake<ILogger<PollyRetryPolicyAdapter>>();

		// Act
		var adapter = new PollyRetryPolicyAdapter(options, logger);

		// Assert
		_ = adapter.ShouldNotBeNull();
	}

	#endregion

	#region ExecuteAsync<TResult> Tests

	[Fact]
	public async Task ExecuteAsync_WithNullAction_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithSuccessfulOperation_ReturnsResult()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);

		// Act
		var result = await adapter.ExecuteAsync(_ => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
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
		var adapter = new PollyRetryPolicyAdapter(options);
		var callCount = 0;

		// Act
		var result = await adapter.ExecuteAsync(_ =>
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
		var adapter = new PollyRetryPolicyAdapter(options);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => adapter.ExecuteAsync<int>(_ => throw new InvalidOperationException("Persistent failure"), CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithCancellationToken_PassesTokenToAction()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);
		using var cts = new CancellationTokenSource();
		CancellationToken capturedToken = default;

		// Act
		_ = await adapter.ExecuteAsync(ct =>
		{
			capturedToken = ct;
			return Task.FromResult(1);
		}, cts.Token);

		// Assert
		capturedToken.ShouldNotBe(CancellationToken.None);
	}

	#endregion

	#region ExecuteAsync (void) Tests

	[Fact]
	public async Task ExecuteAsync_VoidAction_WithNullAction_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => adapter.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_VoidAction_WithSuccessfulOperation_Completes()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);
		var executed = false;

		// Act
		await adapter.ExecuteAsync(_ =>
		{
			executed = true;
			return Task.CompletedTask;
		}, CancellationToken.None);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_VoidAction_WithTransientFailure_Retries()
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 3,
			BaseDelay = TimeSpan.FromMilliseconds(10)
		};
		var adapter = new PollyRetryPolicyAdapter(options);
		var callCount = 0;

		// Act
		await adapter.ExecuteAsync(_ =>
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
		var adapter = new PollyRetryPolicyAdapter(options);
		var callCount = 0;

		// Act
		var result = await adapter.ExecuteAsync(_ =>
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
		var adapter = new PollyRetryPolicyAdapter(options);
		var callCount = 0;

		// Act & Assert
		_ = await Should.ThrowAsync<TimeoutException>(
			() => adapter.ExecuteAsync<int>(_ =>
			{
				callCount++;
				throw new TimeoutException("Not retryable");
			}, CancellationToken.None));

		callCount.ShouldBe(1); // No retry for TimeoutException
	}

	#endregion

	#region Backoff Strategy Tests

	[Theory]
	[InlineData(BackoffStrategy.Fixed)]
	[InlineData(BackoffStrategy.Linear)]
	[InlineData(BackoffStrategy.Exponential)]
	[InlineData(BackoffStrategy.Fibonacci)]
	public async Task ExecuteAsync_WithDifferentBackoffStrategies_ExecutesCorrectly(BackoffStrategy strategy)
	{
		// Arrange
		var options = new RetryOptions
		{
			MaxRetries = 2,
			BaseDelay = TimeSpan.FromMilliseconds(10),
			BackoffStrategy = strategy
		};
		var adapter = new PollyRetryPolicyAdapter(options);
		var callCount = 0;

		// Act
		var result = await adapter.ExecuteAsync(_ =>
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

	#region IRetryPolicy Interface Tests

	[Fact]
	public void ImplementsIRetryPolicy()
	{
		// Arrange
		var options = new RetryOptions();
		var adapter = new PollyRetryPolicyAdapter(options);

		// Assert
		adapter.ShouldBeAssignableTo<IRetryPolicy>();
	}

	#endregion
}
