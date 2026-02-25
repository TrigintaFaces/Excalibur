// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Pooling.Configuration;

/// <summary>
/// Unit tests for <see cref="ResetStrategy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pooling")]
[Trait("Priority", "0")]
public sealed class ResetStrategyShould
{
	#region Value Tests

	[Fact]
	public void Auto_HasValueZero()
	{
		// Assert
		((int)ResetStrategy.Auto).ShouldBe(0);
	}

	[Fact]
	public void SourceGenerated_HasValueOne()
	{
		// Assert
		((int)ResetStrategy.SourceGenerated).ShouldBe(1);
	}

	[Fact]
	public void Interface_HasValueTwo()
	{
		// Assert
		((int)ResetStrategy.Interface).ShouldBe(2);
	}

	[Fact]
	public void None_HasValueThree()
	{
		// Assert
		((int)ResetStrategy.None).ShouldBe(3);
	}

	[Fact]
	public void Disabled_HasValueFour()
	{
		// Assert
		((int)ResetStrategy.Disabled).ShouldBe(4);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<ResetStrategy>();

		// Assert
		values.Length.ShouldBe(5);
	}

	[Theory]
	[InlineData(ResetStrategy.Auto)]
	[InlineData(ResetStrategy.SourceGenerated)]
	[InlineData(ResetStrategy.Interface)]
	[InlineData(ResetStrategy.None)]
	[InlineData(ResetStrategy.Disabled)]
	public void AllValues_AreDefined(ResetStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void Auto_ToStringReturnsExpected()
	{
		// Assert
		ResetStrategy.Auto.ToString().ShouldBe("Auto");
	}

	[Fact]
	public void SourceGenerated_ToStringReturnsExpected()
	{
		// Assert
		ResetStrategy.SourceGenerated.ToString().ShouldBe("SourceGenerated");
	}

	[Fact]
	public void Interface_ToStringReturnsExpected()
	{
		// Assert
		ResetStrategy.Interface.ToString().ShouldBe("Interface");
	}

	[Fact]
	public void None_ToStringReturnsExpected()
	{
		// Assert
		ResetStrategy.None.ToString().ShouldBe("None");
	}

	[Fact]
	public void Disabled_ToStringReturnsExpected()
	{
		// Assert
		ResetStrategy.Disabled.ToString().ShouldBe("Disabled");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Auto", ResetStrategy.Auto)]
	[InlineData("SourceGenerated", ResetStrategy.SourceGenerated)]
	[InlineData("Interface", ResetStrategy.Interface)]
	[InlineData("None", ResetStrategy.None)]
	[InlineData("Disabled", ResetStrategy.Disabled)]
	public void Parse_ReturnsExpectedValue(string input, ResetStrategy expected)
	{
		// Act
		var result = Enum.Parse<ResetStrategy>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<ResetStrategy>("sourcegenerated", ignoreCase: true);

		// Assert
		result.ShouldBe(ResetStrategy.SourceGenerated);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsAuto()
	{
		// Arrange
		var defaultValue = default(ResetStrategy);

		// Assert
		defaultValue.ShouldBe(ResetStrategy.Auto);
	}

	#endregion
}
