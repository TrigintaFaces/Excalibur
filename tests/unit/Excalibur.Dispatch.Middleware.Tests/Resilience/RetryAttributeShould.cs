// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for RetryAttribute configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RetryAttributeShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.MaxAttempts.ShouldBe(3);
		attribute.BaseDelayMs.ShouldBe(1000);
		attribute.MaxDelayMs.ShouldBe(30000);
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
		attribute.JitterFactor.ShouldBe(0.1);
		attribute.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void MaxAttempts_CanBeCustomized()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { MaxAttempts = 5 };

		// Assert
		attribute.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void BaseDelayMs_CanBeCustomized()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { BaseDelayMs = 500 };

		// Assert
		attribute.BaseDelayMs.ShouldBe(500);
	}

	[Fact]
	public void MaxDelayMs_CanBeCustomized()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { MaxDelayMs = 60000 };

		// Assert
		attribute.MaxDelayMs.ShouldBe(60000);
	}

	[Fact]
	public void BackoffStrategy_CanBeChangedToFixed()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { BackoffStrategy = BackoffStrategy.Fixed };

		// Assert
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Fixed);
	}

	[Fact]
	public void BackoffStrategy_CanBeChangedToLinear()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { BackoffStrategy = BackoffStrategy.Linear };

		// Assert
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
	}

	[Fact]
	public void BackoffStrategy_CanBeChangedToExponentialWithJitter()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { BackoffStrategy = BackoffStrategy.ExponentialWithJitter };

		// Assert
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.ExponentialWithJitter);
	}

	[Fact]
	public void JitterFactor_CanBeCustomized()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { JitterFactor = 0.25 };

		// Assert
		attribute.JitterFactor.ShouldBe(0.25);
	}

	[Fact]
	public void UseJitter_CanBeDisabled()
	{
		// Arrange & Act
		var attribute = new RetryAttribute { UseJitter = false };

		// Assert
		attribute.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void CanBeAppliedToClass()
	{
		// Act
		var attributeUsage = typeof(RetryAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
		attributeUsage.AllowMultiple.ShouldBeFalse();
		attributeUsage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void CanBeRetrievedFromDecoratedClass()
	{
		// Arrange
		var messageType = typeof(TestRetryMessage);

		// Act
		var attribute = messageType
			.GetCustomAttributes(typeof(RetryAttribute), true)
			.OfType<RetryAttribute>()
			.FirstOrDefault();

		// Assert
		_ = attribute.ShouldNotBeNull();
		attribute.MaxAttempts.ShouldBe(5);
		attribute.BaseDelayMs.ShouldBe(200);
	}

	[Retry(MaxAttempts = 5, BaseDelayMs = 200)]
	private sealed class TestRetryMessage;
}
