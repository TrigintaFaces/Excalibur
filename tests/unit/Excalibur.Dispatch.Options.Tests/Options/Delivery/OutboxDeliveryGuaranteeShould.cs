// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="OutboxDeliveryGuarantee"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Options)]
[Trait("Priority", "0")]
public sealed class OutboxDeliveryGuaranteeShould
{
	#region Enum Value Tests

	[Fact]
	public void AtLeastOnce_HasExpectedValue()
	{
		// Assert
		((int)OutboxDeliveryGuarantee.AtLeastOnce).ShouldBe(0);
	}

	[Fact]
	public void MinimizedWindow_HasExpectedValue()
	{
		// Assert
		((int)OutboxDeliveryGuarantee.MinimizedWindow).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<OutboxDeliveryGuarantee>();

		// Assert
		values.ShouldContain(OutboxDeliveryGuarantee.AtLeastOnce);
		values.ShouldContain(OutboxDeliveryGuarantee.MinimizedWindow);
	}

	[Fact]
	public void HasExactlyTwoValues()
	{
		// Arrange
		var values = Enum.GetValues<OutboxDeliveryGuarantee>();

		// Assert
		values.Length.ShouldBe(2);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(OutboxDeliveryGuarantee.AtLeastOnce, "AtLeastOnce")]
	[InlineData(OutboxDeliveryGuarantee.MinimizedWindow, "MinimizedWindow")]
	public void ToString_ReturnsExpectedValue(OutboxDeliveryGuarantee guarantee, string expected)
	{
		// Act & Assert
		guarantee.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("AtLeastOnce", OutboxDeliveryGuarantee.AtLeastOnce)]
	[InlineData("MinimizedWindow", OutboxDeliveryGuarantee.MinimizedWindow)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, OutboxDeliveryGuarantee expected)
	{
		// Act
		var result = Enum.Parse<OutboxDeliveryGuarantee>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsAtLeastOnce()
	{
		// Arrange
		OutboxDeliveryGuarantee guarantee = default;

		// Assert
		guarantee.ShouldBe(OutboxDeliveryGuarantee.AtLeastOnce);
	}

	#endregion
}
