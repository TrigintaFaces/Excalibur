// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// Unit tests for <see cref="DeliveryGuarantee"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class DeliveryGuaranteeShould
{
	#region Enum Value Tests

	[Fact]
	public void AtMostOnce_HasExpectedValue()
	{
		// Assert
		((int)DeliveryGuarantee.AtMostOnce).ShouldBe(0);
	}

	[Fact]
	public void AtLeastOnce_HasExpectedValue()
	{
		// Assert
		((int)DeliveryGuarantee.AtLeastOnce).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<DeliveryGuarantee>();

		// Assert
		values.ShouldContain(DeliveryGuarantee.AtMostOnce);
		values.ShouldContain(DeliveryGuarantee.AtLeastOnce);
	}

	[Fact]
	public void HasExactlyTwoValues()
	{
		// Arrange
		var values = Enum.GetValues<DeliveryGuarantee>();

		// Assert
		values.Length.ShouldBe(2);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(DeliveryGuarantee.AtMostOnce, "AtMostOnce")]
	[InlineData(DeliveryGuarantee.AtLeastOnce, "AtLeastOnce")]
	public void ToString_ReturnsExpectedValue(DeliveryGuarantee guarantee, string expected)
	{
		// Act & Assert
		guarantee.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("AtMostOnce", DeliveryGuarantee.AtMostOnce)]
	[InlineData("AtLeastOnce", DeliveryGuarantee.AtLeastOnce)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, DeliveryGuarantee expected)
	{
		// Act
		var result = Enum.Parse<DeliveryGuarantee>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsAtMostOnce()
	{
		// Arrange
		DeliveryGuarantee guarantee = default;

		// Assert
		guarantee.ShouldBe(DeliveryGuarantee.AtMostOnce);
	}

	#endregion
}
