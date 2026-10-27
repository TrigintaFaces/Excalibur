// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Options.Resilience;

/// <summary>
/// Unit tests for <see cref="RetryAttribute"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RetryAttributeShould
{
	[Fact]
	public void HaveDefaultMaxAttemptsOfThree()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveDefaultBaseDelayOfOneThousandMs()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.BaseDelayMs.ShouldBe(1000);
	}

	[Fact]
	public void HaveDefaultMaxDelayOfThirtyThousandMs()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.MaxDelayMs.ShouldBe(30000);
	}

	[Fact]
	public void HaveDefaultBackoffStrategyOfExponential()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
	}


	[Fact]
	public void HaveUseJitterDisabledByDefault()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.UseJitter.ShouldBeFalse();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	[InlineData(100)]
	public void AllowSettingMaxAttempts(int maxAttempts)
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.MaxRetryAttempts = maxAttempts;

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(maxAttempts);
	}

	[Theory]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(1000)]
	[InlineData(5000)]
	public void AllowSettingBaseDelayMs(int baseDelay)
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.BaseDelayMs = baseDelay;

		// Assert
		attribute.BaseDelayMs.ShouldBe(baseDelay);
	}

	[Theory]
	[InlineData(1000)]
	[InlineData(10000)]
	[InlineData(30000)]
	[InlineData(60000)]
	public void AllowSettingMaxDelayMs(int maxDelay)
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.MaxDelayMs = maxDelay;

		// Assert
		attribute.MaxDelayMs.ShouldBe(maxDelay);
	}

	[Theory]
	[InlineData(BackoffStrategy.Constant)]
	[InlineData(BackoffStrategy.Linear)]
	[InlineData(BackoffStrategy.Exponential)]
	public void AllowSettingBackoffStrategy(BackoffStrategy strategy)
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.BackoffStrategy = strategy;

		// Assert
		attribute.BackoffStrategy.ShouldBe(strategy);
	}


	[Fact]
	public void AllowSettingUseJitter()
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.UseJitter = false;

		// Assert
		attribute.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void BeAssignableToAttributeBase()
	{
		// Arrange & Act
		var attribute = new RetryAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	[Fact]
	public void HaveAttributeUsageForClassOnly()
	{
		// Arrange & Act
		var attributeUsage = typeof(RetryAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void NotAllowMultipleInstances()
	{
		// Arrange & Act
		var attributeUsage = typeof(RetryAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.AllowMultiple.ShouldBeFalse();
	}

	[Fact]
	public void BeInheritable()
	{
		// Arrange & Act
		var attributeUsage = typeof(RetryAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void SupportPropertyInitialization()
	{
		// Arrange & Act
		var attribute = new RetryAttribute
		{
			MaxRetryAttempts = 5,
			BaseDelayMs = 500,
			MaxDelayMs = 60000,
			BackoffStrategy = BackoffStrategy.Linear,
			UseJitter = false,
		};

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(5);
		attribute.BaseDelayMs.ShouldBe(500);
		attribute.MaxDelayMs.ShouldBe(60000);
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Linear);
		attribute.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void AllowZeroMaxAttempts()
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.MaxRetryAttempts = 0;

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(0);
	}

	[Fact]
	public void AllowZeroBaseDelay()
	{
		// Arrange
		var attribute = new RetryAttribute();

		// Act
		attribute.BaseDelayMs = 0;

		// Assert
		attribute.BaseDelayMs.ShouldBe(0);
	}


	[Fact]
	public void SimulateTypicalAggressiveRetryConfiguration()
	{
		// Arrange & Act - Aggressive retry for important operations
		var attribute = new RetryAttribute
		{
			MaxRetryAttempts = 10,
			BaseDelayMs = 100,
			MaxDelayMs = 5000,
			BackoffStrategy = BackoffStrategy.Exponential,
			UseJitter = true,
		};

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(10);
		attribute.BaseDelayMs.ShouldBe(100);
		attribute.MaxDelayMs.ShouldBe(5000);
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Exponential);
		attribute.UseJitter.ShouldBeTrue("the arm sets it explicitly; this is not the default");
	}

	[Fact]
	public void SimulateTypicalConservativeRetryConfiguration()
	{
		// Arrange & Act - Conservative retry for non-critical operations
		var attribute = new RetryAttribute
		{
			MaxRetryAttempts = 2,
			BaseDelayMs = 5000,
			MaxDelayMs = 10000,
			BackoffStrategy = BackoffStrategy.Constant,
			UseJitter = false,
		};

		// Assert
		attribute.MaxRetryAttempts.ShouldBe(2);
		attribute.BaseDelayMs.ShouldBe(5000);
		attribute.MaxDelayMs.ShouldBe(10000);
		attribute.BackoffStrategy.ShouldBe(BackoffStrategy.Constant);
		attribute.UseJitter.ShouldBeFalse();
	}

	[Fact]
	public void BeRetrievableFromDecoratedClass()
	{
		// Arrange & Act
		var attribute = typeof(SampleRetryDecoratedClass)
			.GetCustomAttributes(typeof(RetryAttribute), false)
			.Cast<RetryAttribute>()
			.FirstOrDefault();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.MaxRetryAttempts.ShouldBe(5);
		attribute.BaseDelayMs.ShouldBe(500);
	}

	[Retry(MaxRetryAttempts = 5, BaseDelayMs = 500)]
	private sealed class SampleRetryDecoratedClass
	{
	}
}
