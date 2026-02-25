// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Unit tests for <see cref="DefaultSagaRetryPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class DefaultSagaRetryPolicyShould
{
	#region Default Values Tests

	[Fact]
	public void HaveDefaultMaxAttempts()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultRetryDelay()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HaveDefaultBackoffMultiplier()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		policy.BackoffMultiplier.ShouldBe(1);
	}

	#endregion Default Values Tests

	#region Property Initialization Tests

	[Fact]
	public void AllowMaxAttemptsToBeInitialized()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy { MaxAttempts = 5 };

		// Assert
		policy.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void AllowRetryDelayToBeInitialized()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy { RetryDelay = TimeSpan.FromSeconds(5) };

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowBackoffMultiplierToBeInitialized()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy { BackoffMultiplier = 2.5 };

		// Assert
		policy.BackoffMultiplier.ShouldBe(2.5);
	}

	#endregion Property Initialization Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementISagaRetryPolicy()
	{
		// Arrange & Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		_ = policy.ShouldBeAssignableTo<ISagaRetryPolicy>();
	}

	#endregion Interface Implementation Tests

	#region ShouldRetry Tests

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForOperationCanceledException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new OperationCanceledException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForTaskCanceledException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new TaskCanceledException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert - TaskCanceledException inherits from OperationCanceledException
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForArgumentException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new ArgumentException("Invalid argument");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForArgumentNullException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new ArgumentNullException("param");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert - ArgumentNullException inherits from ArgumentException
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForArgumentOutOfRangeException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new ArgumentOutOfRangeException("param");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert - ArgumentOutOfRangeException inherits from ArgumentException
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForInvalidOperationException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForTimeoutException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new TimeoutException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForHttpRequestException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new HttpRequestException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForIOException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new IOException();

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForGenericException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();
		var exception = new Exception("Generic error");

		// Act
		var result = policy.ShouldRetry(exception);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion ShouldRetry Tests

	#region ExponentialBackoff Factory Tests

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithDefaultValues()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff();

		// Assert
		policy.ShouldBeAssignableTo<ISagaRetryPolicy>();
		policy.MaxAttempts.ShouldBe(3);
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithCustomMaxAttempts()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff(maxAttempts: 5);

		// Assert
		policy.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithCustomInitialDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff(initialDelay: TimeSpan.FromMilliseconds(500));

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithBackoffMultiplierOfTwo()
	{
		// Act
		var policy = (DefaultSagaRetryPolicy)DefaultSagaRetryPolicy.ExponentialBackoff();

		// Assert
		policy.BackoffMultiplier.ShouldBe(2);
	}

	[Fact]
	public void ExponentialBackoff_ReturnsPolicy_WithAllCustomValues()
	{
		// Act
		var policy = (DefaultSagaRetryPolicy)DefaultSagaRetryPolicy.ExponentialBackoff(
			maxAttempts: 10,
			initialDelay: TimeSpan.FromSeconds(2));

		// Assert
		policy.MaxAttempts.ShouldBe(10);
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
		policy.BackoffMultiplier.ShouldBe(2);
	}

	#endregion ExponentialBackoff Factory Tests

	#region FixedDelay Factory Tests

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithDefaultValues()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay();

		// Assert
		policy.ShouldBeAssignableTo<ISagaRetryPolicy>();
		policy.MaxAttempts.ShouldBe(3);
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithCustomMaxAttempts()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay(maxAttempts: 7);

		// Assert
		policy.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithCustomDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay(delay: TimeSpan.FromSeconds(3));

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithBackoffMultiplierOfOne()
	{
		// Act
		var policy = (DefaultSagaRetryPolicy)DefaultSagaRetryPolicy.FixedDelay();

		// Assert
		policy.BackoffMultiplier.ShouldBe(1);
	}

	[Fact]
	public void FixedDelay_ReturnsPolicy_WithAllCustomValues()
	{
		// Act
		var policy = (DefaultSagaRetryPolicy)DefaultSagaRetryPolicy.FixedDelay(
			maxAttempts: 5,
			delay: TimeSpan.FromMilliseconds(200));

		// Assert
		policy.MaxAttempts.ShouldBe(5);
		policy.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		policy.BackoffMultiplier.ShouldBe(1);
	}

	#endregion FixedDelay Factory Tests
}
