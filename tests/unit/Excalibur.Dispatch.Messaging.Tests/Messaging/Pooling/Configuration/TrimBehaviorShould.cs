// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Pooling.Configuration;

/// <summary>
/// Unit tests for <see cref="TrimBehavior"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pooling")]
[Trait("Priority", "0")]
public sealed class TrimBehaviorShould
{
	#region Value Tests

	[Fact]
	public void None_HasValueZero()
	{
		// Assert
		((int)TrimBehavior.None).ShouldBe(0);
	}

	[Fact]
	public void Fixed_HasValueOne()
	{
		// Assert
		((int)TrimBehavior.Fixed).ShouldBe(1);
	}

	[Fact]
	public void Adaptive_HasValueTwo()
	{
		// Assert
		((int)TrimBehavior.Adaptive).ShouldBe(2);
	}

	[Fact]
	public void Aggressive_HasValueThree()
	{
		// Assert
		((int)TrimBehavior.Aggressive).ShouldBe(3);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<TrimBehavior>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(TrimBehavior.None)]
	[InlineData(TrimBehavior.Fixed)]
	[InlineData(TrimBehavior.Adaptive)]
	[InlineData(TrimBehavior.Aggressive)]
	public void AllValues_AreDefined(TrimBehavior behavior)
	{
		// Assert
		Enum.IsDefined(behavior).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void None_ToStringReturnsExpected()
	{
		// Assert
		TrimBehavior.None.ToString().ShouldBe("None");
	}

	[Fact]
	public void Fixed_ToStringReturnsExpected()
	{
		// Assert
		TrimBehavior.Fixed.ToString().ShouldBe("Fixed");
	}

	[Fact]
	public void Adaptive_ToStringReturnsExpected()
	{
		// Assert
		TrimBehavior.Adaptive.ToString().ShouldBe("Adaptive");
	}

	[Fact]
	public void Aggressive_ToStringReturnsExpected()
	{
		// Assert
		TrimBehavior.Aggressive.ToString().ShouldBe("Aggressive");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("None", TrimBehavior.None)]
	[InlineData("Fixed", TrimBehavior.Fixed)]
	[InlineData("Adaptive", TrimBehavior.Adaptive)]
	[InlineData("Aggressive", TrimBehavior.Aggressive)]
	public void Parse_ReturnsExpectedValue(string input, TrimBehavior expected)
	{
		// Act
		var result = Enum.Parse<TrimBehavior>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<TrimBehavior>("adaptive", ignoreCase: true);

		// Assert
		result.ShouldBe(TrimBehavior.Adaptive);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		var defaultValue = default(TrimBehavior);

		// Assert
		defaultValue.ShouldBe(TrimBehavior.None);
	}

	#endregion
}
