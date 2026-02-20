// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Timing;

/// <summary>
/// Unit tests for <see cref="OperationComplexity"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Timing")]
[Trait("Priority", "0")]
public sealed class OperationComplexityShould
{
	#region Enum Value Tests

	[Fact]
	public void Simple_HasExpectedValue()
	{
		// Assert
		((int)OperationComplexity.Simple).ShouldBe(0);
	}

	[Fact]
	public void Normal_HasExpectedValue()
	{
		// Assert
		((int)OperationComplexity.Normal).ShouldBe(1);
	}

	[Fact]
	public void Complex_HasExpectedValue()
	{
		// Assert
		((int)OperationComplexity.Complex).ShouldBe(2);
	}

	[Fact]
	public void Heavy_HasExpectedValue()
	{
		// Assert
		((int)OperationComplexity.Heavy).ShouldBe(3);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<OperationComplexity>();

		// Assert
		values.ShouldContain(OperationComplexity.Simple);
		values.ShouldContain(OperationComplexity.Normal);
		values.ShouldContain(OperationComplexity.Complex);
		values.ShouldContain(OperationComplexity.Heavy);
	}

	[Fact]
	public void HasExactlyFourValues()
	{
		// Arrange
		var values = Enum.GetValues<OperationComplexity>();

		// Assert
		values.Length.ShouldBe(4);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(OperationComplexity.Simple, "Simple")]
	[InlineData(OperationComplexity.Normal, "Normal")]
	[InlineData(OperationComplexity.Complex, "Complex")]
	[InlineData(OperationComplexity.Heavy, "Heavy")]
	public void ToString_ReturnsExpectedValue(OperationComplexity complexity, string expected)
	{
		// Act & Assert
		complexity.ToString().ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsSimple()
	{
		// Arrange
		OperationComplexity complexity = default;

		// Assert
		complexity.ShouldBe(OperationComplexity.Simple);
	}

	#endregion

	#region Comparison Tests

	[Fact]
	public void Simple_IsLessThanNormal()
	{
		// Assert
		(OperationComplexity.Simple < OperationComplexity.Normal).ShouldBeTrue();
	}

	[Fact]
	public void Normal_IsLessThanComplex()
	{
		// Assert
		(OperationComplexity.Normal < OperationComplexity.Complex).ShouldBeTrue();
	}

	[Fact]
	public void Complex_IsLessThanHeavy()
	{
		// Assert
		(OperationComplexity.Complex < OperationComplexity.Heavy).ShouldBeTrue();
	}

	#endregion
}
