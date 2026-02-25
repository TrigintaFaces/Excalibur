// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging.CloudEvents;

/// <summary>
/// Unit tests for <see cref="CloudEventMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class CloudEventModeShould
{
	[Fact]
	public void HaveTwoDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<CloudEventMode>();

		// Assert
		values.Length.ShouldBe(2);
		values.ShouldContain(CloudEventMode.Structured);
		values.ShouldContain(CloudEventMode.Binary);
	}

	[Fact]
	public void Structured_HasExpectedValue()
	{
		// Assert
		((int)CloudEventMode.Structured).ShouldBe(0);
	}

	[Fact]
	public void Binary_HasExpectedValue()
	{
		// Assert
		((int)CloudEventMode.Binary).ShouldBe(1);
	}

	[Fact]
	public void Structured_IsDefaultValue()
	{
		// Arrange
		CloudEventMode defaultMode = default;

		// Assert
		defaultMode.ShouldBe(CloudEventMode.Structured);
	}

	[Theory]
	[InlineData(CloudEventMode.Structured)]
	[InlineData(CloudEventMode.Binary)]
	public void BeDefinedForAllValues(CloudEventMode mode)
	{
		// Assert
		Enum.IsDefined(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, CloudEventMode.Structured)]
	[InlineData(1, CloudEventMode.Binary)]
	public void CastFromInt_ReturnsCorrectValue(int value, CloudEventMode expected)
	{
		// Act
		var mode = (CloudEventMode)value;

		// Assert
		mode.ShouldBe(expected);
	}
}
