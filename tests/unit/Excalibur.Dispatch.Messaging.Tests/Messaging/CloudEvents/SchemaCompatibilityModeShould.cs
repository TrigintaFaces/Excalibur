// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Unit tests for <see cref="SchemaCompatibilityMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class SchemaCompatibilityModeShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<SchemaCompatibilityMode>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(SchemaCompatibilityMode.None);
		values.ShouldContain(SchemaCompatibilityMode.Forward);
		values.ShouldContain(SchemaCompatibilityMode.Backward);
		values.ShouldContain(SchemaCompatibilityMode.Full);
	}

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)SchemaCompatibilityMode.None).ShouldBe(0);
	}

	[Fact]
	public void Forward_HasExpectedValue()
	{
		// Assert
		((int)SchemaCompatibilityMode.Forward).ShouldBe(1);
	}

	[Fact]
	public void Backward_HasExpectedValue()
	{
		// Assert
		((int)SchemaCompatibilityMode.Backward).ShouldBe(2);
	}

	[Fact]
	public void Full_HasExpectedValue()
	{
		// Assert
		((int)SchemaCompatibilityMode.Full).ShouldBe(3);
	}

	[Fact]
	public void None_IsDefaultValue()
	{
		// Arrange
		SchemaCompatibilityMode defaultMode = default;

		// Assert
		defaultMode.ShouldBe(SchemaCompatibilityMode.None);
	}

	[Theory]
	[InlineData(SchemaCompatibilityMode.None)]
	[InlineData(SchemaCompatibilityMode.Forward)]
	[InlineData(SchemaCompatibilityMode.Backward)]
	[InlineData(SchemaCompatibilityMode.Full)]
	public void BeDefinedForAllValues(SchemaCompatibilityMode mode)
	{
		// Assert
		Enum.IsDefined(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, SchemaCompatibilityMode.None)]
	[InlineData(1, SchemaCompatibilityMode.Forward)]
	[InlineData(2, SchemaCompatibilityMode.Backward)]
	[InlineData(3, SchemaCompatibilityMode.Full)]
	public void CastFromInt_ReturnsCorrectValue(int value, SchemaCompatibilityMode expected)
	{
		// Act
		var mode = (SchemaCompatibilityMode)value;

		// Assert
		mode.ShouldBe(expected);
	}
}
