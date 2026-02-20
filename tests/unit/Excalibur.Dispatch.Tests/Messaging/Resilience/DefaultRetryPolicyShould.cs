// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultRetryPolicyShould
{
	[Fact]
	public void Constructor_ThrowOnNullOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new DefaultRetryPolicy(null!));
	}

	[Fact]
	public async Task ExecuteAsync_WithResult_ReturnResult()
	{
		// Arrange
		var options = new RetryPolicyOptions { MaxRetryAttempts = 3 };
		var policy = new DefaultRetryPolicy(options);

		// Act
		var result = await policy.ExecuteAsync(
			ct => Task.FromResult(42),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithResult_ThrowOnNullAction()
	{
		// Arrange
		var options = new RetryPolicyOptions();
		var policy = new DefaultRetryPolicy(options);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithoutResult_CompleteSuccessfully()
	{
		// Arrange
		var options = new RetryPolicyOptions();
		var policy = new DefaultRetryPolicy(options);
		var executed = false;

		// Act
		await policy.ExecuteAsync(
			ct =>
			{
				executed = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_WithoutResult_ThrowOnNullAction()
	{
		// Arrange
		var options = new RetryPolicyOptions();
		var policy = new DefaultRetryPolicy(options);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_RetryOnTransientFailure()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act — fail twice, succeed on third attempt
		var result = await policy.ExecuteAsync(
			ct =>
			{
				attempts++;
				if (attempts < 3)
				{
					throw new InvalidOperationException("transient");
				}

				return Task.FromResult(99);
			},
			CancellationToken.None);

		// Assert
		result.ShouldBe(99);
		attempts.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_ThrowAfterMaxRetries()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 2,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(10),
		};
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act & Assert — always fails, should throw after max retries
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new InvalidOperationException("persistent failure");
				},
				CancellationToken.None));

		attempts.ShouldBe(2);
	}

	[Fact]
	public async Task ExecuteAsync_NeverRetryCancellationException()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act & Assert — OperationCanceledException should not be retried
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new OperationCanceledException();
				},
				CancellationToken.None));

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_NeverRetryTaskCanceledException()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act & Assert
		await Should.ThrowAsync<TaskCanceledException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new TaskCanceledException();
				},
				CancellationToken.None));

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_SkipNonRetriableExceptions()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(1),
		};
		options.NonRetriableExceptions.Add(typeof(ArgumentException));
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act & Assert — ArgumentException is non-retriable
		await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new ArgumentException("bad input");
				},
				CancellationToken.None));

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_OnlyRetryRetriableExceptions()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(10),
		};
		options.RetriableExceptions.Add(typeof(TimeoutException));
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act & Assert — InvalidOperationException is NOT in retriable list, should not retry
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new InvalidOperationException("not retriable");
				},
				CancellationToken.None));

		attempts.ShouldBe(1);
	}

	[Fact]
	public async Task ExecuteAsync_RetryOnlyRetriableExceptionTypes()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
			BaseDelay = TimeSpan.FromMilliseconds(1),
			MaxDelay = TimeSpan.FromMilliseconds(10),
		};
		options.RetriableExceptions.Add(typeof(TimeoutException));
		var policy = new DefaultRetryPolicy(options);
		var attempts = 0;

		// Act — TimeoutException IS in retriable list, should retry until max
		await Should.ThrowAsync<TimeoutException>(async () =>
			await policy.ExecuteAsync<int>(
				ct =>
				{
					attempts++;
					throw new TimeoutException("timed out");
				},
				CancellationToken.None));

		attempts.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_WithCustomBackoff_UseProvidedCalculator()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			MaxRetryAttempts = 3,
		};
		var backoff = A.Fake<IBackoffCalculator>();
		A.CallTo(() => backoff.CalculateDelay(A<int>._)).Returns(TimeSpan.FromMilliseconds(1));
		var policy = new DefaultRetryPolicy(options, backoff);
		var attempts = 0;

		// Act
		var result = await policy.ExecuteAsync(
			ct =>
			{
				attempts++;
				if (attempts < 3)
				{
					throw new InvalidOperationException("transient");
				}

				return Task.FromResult(true);
			},
			CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		A.CallTo(() => backoff.CalculateDelay(A<int>._)).MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public void ImplementIRetryPolicy()
	{
		// Arrange
		var options = new RetryPolicyOptions();
		var policy = new DefaultRetryPolicy(options);

		// Assert
		policy.ShouldBeAssignableTo<IRetryPolicy>();
	}
}
