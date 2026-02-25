// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="RetryOptions"/>.
/// </summary>
/// <remarks>
/// Tests the retry options class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class RetryOptionsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_MaxAttemptsIsThree()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void Default_BaseDelayIsOneSecond()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_MaxDelayIsThirtySeconds()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Default_BackoffStrategyIsExponential()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
	}

	[Fact]
	public void Default_BackoffMultiplierIsTwo()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.BackoffMultiplier.ShouldBe(2.0);
	}

	[Fact]
	public void Default_JitterFactorIsPointOne()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.JitterFactor.ShouldBe(0.1);
	}

	[Fact]
	public void Default_UseJitterIsTrue()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void Default_RetryableExceptionsIsEmpty()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.RetryableExceptions.ShouldBeEmpty();
	}

	[Fact]
	public void Default_NonRetryableExceptionsContainsArgumentException()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.NonRetryableExceptions.ShouldContain(typeof(ArgumentException));
	}

	[Fact]
	public void Default_NonRetryableExceptionsContainsArgumentNullException()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.NonRetryableExceptions.ShouldContain(typeof(ArgumentNullException));
	}

	[Fact]
	public void Default_NonRetryableExceptionsContainsInvalidOperationException()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.NonRetryableExceptions.ShouldContain(typeof(InvalidOperationException));
	}

	[Fact]
	public void Default_NonRetryableExceptionsHasThreeItems()
	{
		// Arrange & Act
		var options = new RetryOptions();

		// Assert
		options.NonRetryableExceptions.Count.ShouldBe(3);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MaxAttempts_CanBeSet()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.MaxAttempts = 5;

		// Assert
		options.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void MaxAttempts_CanBeSetToZero()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.MaxAttempts = 0;

		// Assert
		options.MaxAttempts.ShouldBe(0);
	}

	[Fact]
	public void MaxAttempts_CanBeSetToOne()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.MaxAttempts = 1;

		// Assert
		options.MaxAttempts.ShouldBe(1);
	}

	[Fact]
	public void BaseDelay_CanBeSet()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BaseDelay = TimeSpan.FromMilliseconds(500);

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void BaseDelay_CanBeSetToZero()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BaseDelay = TimeSpan.Zero;

		// Assert
		options.BaseDelay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void MaxDelay_CanBeSet()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.MaxDelay = TimeSpan.FromMinutes(5);

		// Assert
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void BackoffStrategy_CanBeSetToFixed()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BackoffStrategy = BackoffStrategy.Fixed;

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Fixed);
	}

	[Fact]
	public void BackoffStrategy_CanBeSetToLinear()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BackoffStrategy = BackoffStrategy.Linear;

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
	}

	[Fact]
	public void BackoffStrategy_CanBeSetToExponentialWithJitter()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BackoffStrategy = BackoffStrategy.ExponentialWithJitter;

		// Assert
		options.BackoffStrategy.ShouldBe(BackoffStrategy.ExponentialWithJitter);
	}

	[Fact]
	public void BackoffMultiplier_CanBeSet()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BackoffMultiplier = 1.5;

		// Assert
		options.BackoffMultiplier.ShouldBe(1.5);
	}

	[Fact]
	public void BackoffMultiplier_CanBeSetToZero()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.BackoffMultiplier = 0;

		// Assert
		options.BackoffMultiplier.ShouldBe(0);
	}

	[Fact]
	public void JitterFactor_CanBeSet()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.JitterFactor = 0.25;

		// Assert
		options.JitterFactor.ShouldBe(0.25);
	}

	[Fact]
	public void JitterFactor_CanBeSetToZero()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.JitterFactor = 0;

		// Assert
		options.JitterFactor.ShouldBe(0);
	}

	[Fact]
	public void UseJitter_CanBeSetToFalse()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.UseJitter = false;

		// Assert
		options.UseJitter.ShouldBeFalse();
	}

	#endregion

	#region RetryableExceptions Tests

	[Fact]
	public void RetryableExceptions_CanAddException()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));

		// Assert
		options.RetryableExceptions.ShouldContain(typeof(TimeoutException));
	}

	[Fact]
	public void RetryableExceptions_CanAddMultipleExceptions()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));
		_ = options.RetryableExceptions.Add(typeof(IOException));

		// Assert
		options.RetryableExceptions.Count.ShouldBe(2);
	}

	[Fact]
	public void RetryableExceptions_CanRemoveException()
	{
		// Arrange
		var options = new RetryOptions();
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));

		// Act
		_ = options.RetryableExceptions.Remove(typeof(TimeoutException));

		// Assert
		options.RetryableExceptions.ShouldNotContain(typeof(TimeoutException));
	}

	[Fact]
	public void RetryableExceptions_CanClear()
	{
		// Arrange
		var options = new RetryOptions();
		_ = options.RetryableExceptions.Add(typeof(TimeoutException));
		_ = options.RetryableExceptions.Add(typeof(IOException));

		// Act
		options.RetryableExceptions.Clear();

		// Assert
		options.RetryableExceptions.ShouldBeEmpty();
	}

	#endregion

	#region NonRetryableExceptions Tests

	[Fact]
	public void NonRetryableExceptions_CanAddException()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		_ = options.NonRetryableExceptions.Add(typeof(NotSupportedException));

		// Assert
		options.NonRetryableExceptions.ShouldContain(typeof(NotSupportedException));
	}

	[Fact]
	public void NonRetryableExceptions_CanRemoveException()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		_ = options.NonRetryableExceptions.Remove(typeof(ArgumentException));

		// Assert
		options.NonRetryableExceptions.ShouldNotContain(typeof(ArgumentException));
	}

	[Fact]
	public void NonRetryableExceptions_CanClear()
	{
		// Arrange
		var options = new RetryOptions();

		// Act
		options.NonRetryableExceptions.Clear();

		// Assert
		options.NonRetryableExceptions.ShouldBeEmpty();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new RetryOptions
		{
			MaxAttempts = 5,
			BaseDelay = TimeSpan.FromMilliseconds(500),
			MaxDelay = TimeSpan.FromMinutes(2),
			BackoffStrategy = BackoffStrategy.Linear,
			BackoffMultiplier = 1.5,
			JitterFactor = 0.2,
			UseJitter = false,
		};

		// Assert
		options.MaxAttempts.ShouldBe(5);
		options.BaseDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
		options.BackoffMultiplier.ShouldBe(1.5);
		options.JitterFactor.ShouldBe(0.2);
		options.UseJitter.ShouldBeFalse();
	}

	#endregion
}
