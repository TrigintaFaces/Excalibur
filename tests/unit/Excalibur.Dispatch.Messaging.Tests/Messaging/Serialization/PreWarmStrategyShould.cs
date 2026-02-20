// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Unit tests for <see cref="PreWarmStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class PreWarmStrategyShould
{
	#region Value Tests

	[Fact]
	public void ThreadLocal_HasValueZero()
	{
		// Assert
		((int)PreWarmStrategy.ThreadLocal).ShouldBe(0);
	}

	[Fact]
	public void Global_HasValueOne()
	{
		// Assert
		((int)PreWarmStrategy.Global).ShouldBe(1);
	}

	[Fact]
	public void Balanced_HasValueTwo()
	{
		// Assert
		((int)PreWarmStrategy.Balanced).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<PreWarmStrategy>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(PreWarmStrategy.ThreadLocal)]
	[InlineData(PreWarmStrategy.Global)]
	[InlineData(PreWarmStrategy.Balanced)]
	public void AllValues_AreDefined(PreWarmStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void ThreadLocal_ToStringReturnsExpected()
	{
		// Assert
		PreWarmStrategy.ThreadLocal.ToString().ShouldBe("ThreadLocal");
	}

	[Fact]
	public void Global_ToStringReturnsExpected()
	{
		// Assert
		PreWarmStrategy.Global.ToString().ShouldBe("Global");
	}

	[Fact]
	public void Balanced_ToStringReturnsExpected()
	{
		// Assert
		PreWarmStrategy.Balanced.ToString().ShouldBe("Balanced");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("ThreadLocal", PreWarmStrategy.ThreadLocal)]
	[InlineData("Global", PreWarmStrategy.Global)]
	[InlineData("Balanced", PreWarmStrategy.Balanced)]
	public void Parse_ReturnsExpectedValue(string input, PreWarmStrategy expected)
	{
		// Act
		var result = Enum.Parse<PreWarmStrategy>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<PreWarmStrategy>("balanced", ignoreCase: true);

		// Assert
		result.ShouldBe(PreWarmStrategy.Balanced);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsThreadLocal()
	{
		// Arrange
		var defaultValue = default(PreWarmStrategy);

		// Assert
		defaultValue.ShouldBe(PreWarmStrategy.ThreadLocal);
	}

	#endregion
}
