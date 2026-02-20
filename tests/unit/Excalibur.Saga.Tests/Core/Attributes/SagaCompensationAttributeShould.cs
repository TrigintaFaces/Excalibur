// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Attributes;

namespace Excalibur.Saga.Tests.Core.Attributes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaCompensationAttributeShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute();

		// Assert
		sut.ForStep.ShouldBe(string.Empty);
		sut.Order.ShouldBe(0);
		sut.MaxRetries.ShouldBe(-1);
		sut.Strategy.ShouldBe(CompensationStrategy.Default);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute
		{
			ForStep = "ReserveInventory",
			Order = 1,
			MaxRetries = 3,
			Strategy = CompensationStrategy.Retry,
		};

		// Assert
		sut.ForStep.ShouldBe("ReserveInventory");
		sut.Order.ShouldBe(1);
		sut.MaxRetries.ShouldBe(3);
		sut.Strategy.ShouldBe(CompensationStrategy.Retry);
	}

	[Fact]
	public void BeApplicableToMethodsOnly()
	{
		// Arrange
		var attributeUsage = typeof(SagaCompensationAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		// Assert
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Method);
		attributeUsage.AllowMultiple.ShouldBeFalse();
		attributeUsage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void DefaultMaxRetriesIndicatesUseOfGlobalSetting()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute();

		// Assert - -1 means defer to AdvancedSagaOptions.MaxRetryAttempts
		sut.MaxRetries.ShouldBe(-1);
	}

	[Fact]
	public void DefaultStrategyIndicatesUseOfGlobalSetting()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute();

		// Assert - Default means defer to AdvancedSagaOptions.EnableAutoCompensation
		sut.Strategy.ShouldBe(CompensationStrategy.Default);
	}

	[Fact]
	public void AllowZeroMaxRetries()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute { MaxRetries = 0 };

		// Assert
		sut.MaxRetries.ShouldBe(0);
	}

	[Fact]
	public void SupportManualInterventionStrategy()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute
		{
			Strategy = CompensationStrategy.ManualIntervention,
		};

		// Assert
		sut.Strategy.ShouldBe(CompensationStrategy.ManualIntervention);
	}

	[Fact]
	public void SupportSkipStrategy()
	{
		// Arrange & Act
		var sut = new SagaCompensationAttribute
		{
			Strategy = CompensationStrategy.Skip,
		};

		// Assert
		sut.Strategy.ShouldBe(CompensationStrategy.Skip);
	}
}
