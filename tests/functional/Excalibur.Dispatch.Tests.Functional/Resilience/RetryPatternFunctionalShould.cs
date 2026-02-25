// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional.Resilience;

/// <summary>
/// Functional tests for retry patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Resilience")]
[Trait("Feature", "Retry")]
public sealed class RetryPatternFunctionalShould : FunctionalTestBase
{
	[Fact]
	public async Task RetryOnTransientFailure()
	{
		// Arrange
		var attemptCount = 0;
		const int maxRetries = 3;
		var succeeded = false;
		var failUntilAttempt = 3;

		// Act - Simulate retry behavior
		for (var retry = 1; retry <= maxRetries + 1 && !succeeded; retry++)
		{
			attemptCount++;
			await Task.Delay(5).ConfigureAwait(false); // Intentional: simulates work between retry attempts

			// Succeed on third attempt
			if (attemptCount >= failUntilAttempt)
			{
				succeeded = true;
			}
		}

		// Assert
		attemptCount.ShouldBe(failUntilAttempt);
		succeeded.ShouldBeTrue();
	}

	[Fact]
	public void CalculateExponentialBackoff()
	{
		// Arrange
		var initialDelay = TimeSpan.FromMilliseconds(100);
		var multiplier = 2.0;
		var maxDelay = TimeSpan.FromSeconds(10);

		// Act - Calculate delays for each retry
		var delays = new List<TimeSpan>();
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var delay = TimeSpan.FromMilliseconds(initialDelay.TotalMilliseconds * Math.Pow(multiplier, attempt));
			if (delay > maxDelay)
			{
				delay = maxDelay;
			}

			delays.Add(delay);
		}

		// Assert
		delays[0].ShouldBe(TimeSpan.FromMilliseconds(100));
		delays[1].ShouldBe(TimeSpan.FromMilliseconds(200));
		delays[2].ShouldBe(TimeSpan.FromMilliseconds(400));
		delays[3].ShouldBe(TimeSpan.FromMilliseconds(800));
		delays[4].ShouldBe(TimeSpan.FromMilliseconds(1600));
	}

	[Fact]
	public void CalculateLinearBackoff()
	{
		// Arrange
		var initialDelay = TimeSpan.FromMilliseconds(100);
		var increment = TimeSpan.FromMilliseconds(50);

		// Act - Calculate linear delays
		var delays = new List<TimeSpan>();
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var delay = TimeSpan.FromMilliseconds(
				initialDelay.TotalMilliseconds + (increment.TotalMilliseconds * attempt));
			delays.Add(delay);
		}

		// Assert
		delays[0].ShouldBe(TimeSpan.FromMilliseconds(100));
		delays[1].ShouldBe(TimeSpan.FromMilliseconds(150));
		delays[2].ShouldBe(TimeSpan.FromMilliseconds(200));
		delays[3].ShouldBe(TimeSpan.FromMilliseconds(250));
		delays[4].ShouldBe(TimeSpan.FromMilliseconds(300));
	}

	[Fact]
	public void RespectMaxDelayLimit()
	{
		// Arrange
		var initialDelay = TimeSpan.FromSeconds(1);
		var multiplier = 3.0;
		var maxDelay = TimeSpan.FromSeconds(10);

		// Act - Calculate delays with max limit
		var delays = new List<TimeSpan>();
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var calculatedDelay = TimeSpan.FromSeconds(initialDelay.TotalSeconds * Math.Pow(multiplier, attempt));
			var actualDelay = calculatedDelay > maxDelay ? maxDelay : calculatedDelay;
			delays.Add(actualDelay);
		}

		// Assert
		delays[0].ShouldBe(TimeSpan.FromSeconds(1));
		delays[1].ShouldBe(TimeSpan.FromSeconds(3));
		delays[2].ShouldBe(TimeSpan.FromSeconds(9));
		delays[3].ShouldBe(TimeSpan.FromSeconds(10)); // Capped
		delays[4].ShouldBe(TimeSpan.FromSeconds(10)); // Capped
	}

	[Fact]
	public void TrackRetryAttempts()
	{
		// Arrange
		var retryAttempts = new List<(int attempt, DateTimeOffset timestamp, bool succeeded)>();
		const int maxRetries = 3;

		// Act - Simulate tracking
		for (var attempt = 1; attempt <= maxRetries; attempt++)
		{
			var succeeded = attempt == maxRetries;
			retryAttempts.Add((attempt, DateTimeOffset.UtcNow, succeeded));
		}

		// Assert
		retryAttempts.Count.ShouldBe(3);
		retryAttempts.Last().succeeded.ShouldBeTrue();
		retryAttempts.Take(2).ShouldAllBe(a => !a.succeeded);
	}

	[Fact]
	public async Task StopRetryingAfterMaxAttempts()
	{
		// Arrange
		var attemptCount = 0;
		const int maxRetries = 3;
		var succeeded = false;

		// Act - Simulate always-failing operation
		for (var retry = 1; retry <= maxRetries + 1 && !succeeded; retry++)
		{
			attemptCount++;
			await Task.Delay(5).ConfigureAwait(false); // Intentional: simulates work between retry attempts
			// Never succeed
		}

		// Assert
		attemptCount.ShouldBe(maxRetries + 1);
		succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task SucceedOnFirstAttemptWithoutRetry()
	{
		// Arrange
		var attemptCount = 0;
		const int maxRetries = 3;
		var succeeded = false;

		// Act - First attempt succeeds
		for (var retry = 1; retry <= maxRetries + 1 && !succeeded; retry++)
		{
			attemptCount++;
			await Task.Delay(5).ConfigureAwait(false); // Intentional: simulates work between retry attempts
			succeeded = true; // Succeed immediately
		}

		// Assert
		attemptCount.ShouldBe(1);
		succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ApplyJitterToBackoff()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var random = new Random(42); // Seed for reproducibility
		var jitterFactor = 0.2; // 20% jitter

		// Act - Calculate delays with jitter
		var delays = new List<TimeSpan>();
		for (var i = 0; i < 5; i++)
		{
			var jitter = (random.NextDouble() * 2 - 1) * jitterFactor;
			var actualDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * (1 + jitter));
			delays.Add(actualDelay);
		}

		// Assert - All delays should be within jitter range
		foreach (var delay in delays)
		{
			delay.TotalMilliseconds.ShouldBeInRange(
				baseDelay.TotalMilliseconds * (1 - jitterFactor),
				baseDelay.TotalMilliseconds * (1 + jitterFactor));
		}
	}

	[Fact]
	public void HandleSpecificExceptionTypes()
	{
		// Arrange
		var transientExceptions = new List<Type>
		{
			typeof(TimeoutException),
			typeof(HttpRequestException),
		};

		var nonTransientException = new InvalidOperationException();
		var transientException = new TimeoutException();

		// Act
		var shouldRetryTransient = transientExceptions.Contains(transientException.GetType());
		var shouldRetryNonTransient = transientExceptions.Contains(nonTransientException.GetType());

		// Assert
		shouldRetryTransient.ShouldBeTrue();
		shouldRetryNonTransient.ShouldBeFalse();
	}
}
