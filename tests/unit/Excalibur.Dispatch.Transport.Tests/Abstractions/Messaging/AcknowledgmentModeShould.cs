// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="AcknowledgmentMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class AcknowledgmentModeShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AcknowledgmentMode>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(AcknowledgmentMode.OnSuccess);
		values.ShouldContain(AcknowledgmentMode.Immediate);
		values.ShouldContain(AcknowledgmentMode.Manual);
	}

	[Fact]
	public void OnSuccess_HasExpectedValue()
	{
		// Assert
		((int)AcknowledgmentMode.OnSuccess).ShouldBe(0);
	}

	[Fact]
	public void Immediate_HasExpectedValue()
	{
		// Assert
		((int)AcknowledgmentMode.Immediate).ShouldBe(1);
	}

	[Fact]
	public void Manual_HasExpectedValue()
	{
		// Assert
		((int)AcknowledgmentMode.Manual).ShouldBe(2);
	}

	[Fact]
	public void OnSuccess_IsDefaultValue()
	{
		// Arrange
		AcknowledgmentMode defaultMode = default;

		// Assert
		defaultMode.ShouldBe(AcknowledgmentMode.OnSuccess);
	}

	[Theory]
	[InlineData(AcknowledgmentMode.OnSuccess)]
	[InlineData(AcknowledgmentMode.Immediate)]
	[InlineData(AcknowledgmentMode.Manual)]
	public void BeDefinedForAllValues(AcknowledgmentMode mode)
	{
		// Assert
		Enum.IsDefined(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, AcknowledgmentMode.OnSuccess)]
	[InlineData(1, AcknowledgmentMode.Immediate)]
	[InlineData(2, AcknowledgmentMode.Manual)]
	public void CastFromInt_ReturnsCorrectValue(int value, AcknowledgmentMode expected)
	{
		// Act
		var mode = (AcknowledgmentMode)value;

		// Assert
		mode.ShouldBe(expected);
	}
}
