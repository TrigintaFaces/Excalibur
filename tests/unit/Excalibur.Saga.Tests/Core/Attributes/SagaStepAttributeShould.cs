// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Attributes;

namespace Excalibur.Saga.Tests.Core.Attributes;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaStepAttributeShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var sut = new SagaStepAttribute();

		// Assert
		sut.StepName.ShouldBeNull();
		sut.Order.ShouldBe(0);
		sut.TimeoutSeconds.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var sut = new SagaStepAttribute
		{
			StepName = "ReserveInventory",
			Order = 1,
			TimeoutSeconds = 30,
		};

		// Assert
		sut.StepName.ShouldBe("ReserveInventory");
		sut.Order.ShouldBe(1);
		sut.TimeoutSeconds.ShouldBe(30);
	}

	[Fact]
	public void BeApplicableToMethodsOnly()
	{
		// Arrange
		var attributeUsage = typeof(SagaStepAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		// Assert
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Method);
		attributeUsage.AllowMultiple.ShouldBeFalse();
		attributeUsage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void AcceptNullStepName()
	{
		// Arrange & Act
		var sut = new SagaStepAttribute { StepName = null };

		// Assert
		sut.StepName.ShouldBeNull();
	}
}
