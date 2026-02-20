// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for <see cref="ChannelMode"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
[Trait("Priority", "0")]
public sealed class ChannelModeShould
{
	#region Value Tests

	[Fact]
	public void Unbounded_HasValueZero()
	{
		// Assert
		((int)ChannelMode.Unbounded).ShouldBe(0);
	}

	[Fact]
	public void Bounded_HasValueOne()
	{
		// Assert
		((int)ChannelMode.Bounded).ShouldBe(1);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<ChannelMode>();

		// Assert
		values.Length.ShouldBe(2);
	}

	[Theory]
	[InlineData(ChannelMode.Unbounded)]
	[InlineData(ChannelMode.Bounded)]
	public void AllValues_AreDefined(ChannelMode mode)
	{
		// Assert
		Enum.IsDefined(mode).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void Unbounded_ToStringReturnsExpected()
	{
		// Assert
		ChannelMode.Unbounded.ToString().ShouldBe("Unbounded");
	}

	[Fact]
	public void Bounded_ToStringReturnsExpected()
	{
		// Assert
		ChannelMode.Bounded.ToString().ShouldBe("Bounded");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Unbounded", ChannelMode.Unbounded)]
	[InlineData("Bounded", ChannelMode.Bounded)]
	public void Parse_ReturnsExpectedValue(string input, ChannelMode expected)
	{
		// Act
		var result = Enum.Parse<ChannelMode>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_IsCaseSensitive()
	{
		// Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<ChannelMode>("unbounded"));
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<ChannelMode>("unbounded", ignoreCase: true);

		// Assert
		result.ShouldBe(ChannelMode.Unbounded);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsUnbounded()
	{
		// Arrange
		var defaultValue = default(ChannelMode);

		// Assert
		defaultValue.ShouldBe(ChannelMode.Unbounded);
	}

	#endregion
}
