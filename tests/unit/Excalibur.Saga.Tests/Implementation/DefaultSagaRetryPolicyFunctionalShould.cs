// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Functional tests for <see cref="DefaultSagaRetryPolicy"/> covering
/// factory methods, backoff calculations, and exception filtering.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultSagaRetryPolicyFunctionalShould
{
	[Fact]
	public void ExponentialBackoff_ShouldSetMultiplierToTwo()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff(maxAttempts: 5);

		// Assert
		policy.ShouldNotBeNull();
		policy.MaxAttempts.ShouldBe(5);
		policy.ShouldBeOfType<DefaultSagaRetryPolicy>();
		((DefaultSagaRetryPolicy)policy).BackoffMultiplier.ShouldBe(2);
	}

	[Fact]
	public void ExponentialBackoff_ShouldUseDefaultDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff();

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void ExponentialBackoff_ShouldAcceptCustomInitialDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.ExponentialBackoff(
			maxAttempts: 4,
			initialDelay: TimeSpan.FromMilliseconds(500));

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		policy.MaxAttempts.ShouldBe(4);
	}

	[Fact]
	public void FixedDelay_ShouldSetMultiplierToOne()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay(maxAttempts: 3);

		// Assert
		policy.ShouldBeOfType<DefaultSagaRetryPolicy>();
		((DefaultSagaRetryPolicy)policy).BackoffMultiplier.ShouldBe(1);
	}

	[Fact]
	public void FixedDelay_ShouldUseDefaultDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay();

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void FixedDelay_ShouldAcceptCustomDelay()
	{
		// Act
		var policy = DefaultSagaRetryPolicy.FixedDelay(
			maxAttempts: 10,
			delay: TimeSpan.FromSeconds(5));

		// Assert
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
		policy.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForOperationCancelledException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new OperationCanceledException()).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForTaskCanceledException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		// TaskCanceledException inherits from OperationCanceledException
		policy.ShouldRetry(new TaskCanceledException()).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForArgumentException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new ArgumentException("bad arg")).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsFalse_ForArgumentNullException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		// ArgumentNullException inherits from ArgumentException
		policy.ShouldRetry(new ArgumentNullException("param")).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForInvalidOperationException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new InvalidOperationException("transient")).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForTimeoutException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new TimeoutException()).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForHttpRequestException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new HttpRequestException("connection refused")).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_ReturnsTrue_ForIOException()
	{
		// Arrange
		var policy = new DefaultSagaRetryPolicy();

		// Act & Assert
		policy.ShouldRetry(new IOException("disk error")).ShouldBeTrue();
	}

	[Fact]
	public void DefaultPolicy_HasCorrectDefaults()
	{
		// Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		policy.MaxAttempts.ShouldBe(3);
		policy.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
		policy.BackoffMultiplier.ShouldBe(1);
	}

	[Fact]
	public void ImplementsISagaRetryPolicy()
	{
		// Act
		var policy = new DefaultSagaRetryPolicy();

		// Assert
		policy.ShouldBeAssignableTo<ISagaRetryPolicy>();
	}

	[Fact]
	public void ExponentialAndFixed_FactoryMethods_ReturnISagaRetryPolicy()
	{
		// Act
		var exponential = DefaultSagaRetryPolicy.ExponentialBackoff();
		var fixedDelay = DefaultSagaRetryPolicy.FixedDelay();

		// Assert
		exponential.ShouldBeAssignableTo<ISagaRetryPolicy>();
		fixedDelay.ShouldBeAssignableTo<ISagaRetryPolicy>();
	}
}
