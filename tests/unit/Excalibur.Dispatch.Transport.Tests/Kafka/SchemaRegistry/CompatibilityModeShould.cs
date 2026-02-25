// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="CompatibilityMode"/> enum.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify enum values for schema compatibility modes matching Confluent Schema Registry.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class CompatibilityModeShould
{
	#region Enum Value Tests

	[Fact]
	public void DefineNoneAsZero()
	{
		// Assert
		((int)CompatibilityMode.None).ShouldBe(0);
	}

	[Fact]
	public void DefineBackwardAsOne()
	{
		// Assert
		((int)CompatibilityMode.Backward).ShouldBe(1);
	}

	[Fact]
	public void DefineBackwardTransitiveAsTwo()
	{
		// Assert
		((int)CompatibilityMode.BackwardTransitive).ShouldBe(2);
	}

	[Fact]
	public void DefineForwardAsThree()
	{
		// Assert
		((int)CompatibilityMode.Forward).ShouldBe(3);
	}

	[Fact]
	public void DefineForwardTransitiveAsFour()
	{
		// Assert
		((int)CompatibilityMode.ForwardTransitive).ShouldBe(4);
	}

	[Fact]
	public void DefineFullAsFive()
	{
		// Assert
		((int)CompatibilityMode.Full).ShouldBe(5);
	}

	[Fact]
	public void DefineFullTransitiveAsSix()
	{
		// Assert
		((int)CompatibilityMode.FullTransitive).ShouldBe(6);
	}

	#endregion

	#region Enum Count Tests

	[Fact]
	public void HaveSevenDefinedValues()
	{
		// Act
		var values = Enum.GetValues<CompatibilityMode>();

		// Assert
		values.Length.ShouldBe(7);
	}

	[Fact]
	public void ContainAllExpectedModes()
	{
		// Act
		var values = Enum.GetValues<CompatibilityMode>();

		// Assert
		values.ShouldContain(CompatibilityMode.None);
		values.ShouldContain(CompatibilityMode.Backward);
		values.ShouldContain(CompatibilityMode.BackwardTransitive);
		values.ShouldContain(CompatibilityMode.Forward);
		values.ShouldContain(CompatibilityMode.ForwardTransitive);
		values.ShouldContain(CompatibilityMode.Full);
		values.ShouldContain(CompatibilityMode.FullTransitive);
	}

	#endregion

	#region ToApiString Extension Tests

	[Theory]
	[InlineData(CompatibilityMode.None, "NONE")]
	[InlineData(CompatibilityMode.Backward, "BACKWARD")]
	[InlineData(CompatibilityMode.BackwardTransitive, "BACKWARD_TRANSITIVE")]
	[InlineData(CompatibilityMode.Forward, "FORWARD")]
	[InlineData(CompatibilityMode.ForwardTransitive, "FORWARD_TRANSITIVE")]
	[InlineData(CompatibilityMode.Full, "FULL")]
	[InlineData(CompatibilityMode.FullTransitive, "FULL_TRANSITIVE")]
	public void ToApiString_ReturnsCorrectValue(CompatibilityMode mode, string expected)
	{
		// Act
		var result = mode.ToApiString();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ToApiString_ThrowsForInvalidValue()
	{
		// Arrange
		var invalidMode = (CompatibilityMode)999;

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => invalidMode.ToApiString());
	}

	#endregion

	#region ParseCompatibilityMode Tests

	[Theory]
	[InlineData("NONE", CompatibilityMode.None)]
	[InlineData("BACKWARD", CompatibilityMode.Backward)]
	[InlineData("BACKWARD_TRANSITIVE", CompatibilityMode.BackwardTransitive)]
	[InlineData("FORWARD", CompatibilityMode.Forward)]
	[InlineData("FORWARD_TRANSITIVE", CompatibilityMode.ForwardTransitive)]
	[InlineData("FULL", CompatibilityMode.Full)]
	[InlineData("FULL_TRANSITIVE", CompatibilityMode.FullTransitive)]
	public void ParseCompatibilityMode_ParsesValidApiStrings(string apiValue, CompatibilityMode expected)
	{
		// Act
		var result = CompatibilityModeExtensions.ParseCompatibilityMode(apiValue);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("none", CompatibilityMode.None)]
	[InlineData("backward", CompatibilityMode.Backward)]
	[InlineData("full", CompatibilityMode.Full)]
	public void ParseCompatibilityMode_IsCaseInsensitive(string apiValue, CompatibilityMode expected)
	{
		// Act
		var result = CompatibilityModeExtensions.ParseCompatibilityMode(apiValue);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void ParseCompatibilityMode_ThrowsForInvalidValue()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			CompatibilityModeExtensions.ParseCompatibilityMode("INVALID"));
	}

	[Fact]
	public void ParseCompatibilityMode_ThrowsForNullValue()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			CompatibilityModeExtensions.ParseCompatibilityMode(null!));
	}

	#endregion

	#region Round-Trip Tests

	[Theory]
	[InlineData(CompatibilityMode.None)]
	[InlineData(CompatibilityMode.Backward)]
	[InlineData(CompatibilityMode.BackwardTransitive)]
	[InlineData(CompatibilityMode.Forward)]
	[InlineData(CompatibilityMode.ForwardTransitive)]
	[InlineData(CompatibilityMode.Full)]
	[InlineData(CompatibilityMode.FullTransitive)]
	public void ToApiString_AndParse_RoundTripsCorrectly(CompatibilityMode mode)
	{
		// Act
		var apiString = mode.ToApiString();
		var parsed = CompatibilityModeExtensions.ParseCompatibilityMode(apiString);

		// Assert
		parsed.ShouldBe(mode);
	}

	#endregion
}
