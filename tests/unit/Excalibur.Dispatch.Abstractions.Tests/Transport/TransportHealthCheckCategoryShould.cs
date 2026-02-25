// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportHealthCheckCategory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportHealthCheckCategoryShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.None).ShouldBe(0);
	}

	[Fact]
	public void Connectivity_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.Connectivity).ShouldBe(1);
	}

	[Fact]
	public void Performance_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.Performance).ShouldBe(2);
	}

	[Fact]
	public void Resources_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.Resources).ShouldBe(4);
	}

	[Fact]
	public void Configuration_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.Configuration).ShouldBe(8);
	}

	[Fact]
	public void All_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthCheckCategory.All).ShouldBe(15);
	}

	#endregion

	#region Flags Attribute Tests

	[Fact]
	public void HasFlagsAttribute()
	{
		// Assert
		typeof(TransportHealthCheckCategory).GetCustomAttributes(typeof(FlagsAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void All_ContainsConnectivity()
	{
		// Assert
		TransportHealthCheckCategory.All.HasFlag(TransportHealthCheckCategory.Connectivity).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsPerformance()
	{
		// Assert
		TransportHealthCheckCategory.All.HasFlag(TransportHealthCheckCategory.Performance).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsResources()
	{
		// Assert
		TransportHealthCheckCategory.All.HasFlag(TransportHealthCheckCategory.Resources).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsConfiguration()
	{
		// Assert
		TransportHealthCheckCategory.All.HasFlag(TransportHealthCheckCategory.Configuration).ShouldBeTrue();
	}

	#endregion

	#region Flag Combination Tests

	[Fact]
	public void CanCombineConnectivityAndPerformance()
	{
		// Arrange
		var combined = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance;

		// Assert
		combined.HasFlag(TransportHealthCheckCategory.Connectivity).ShouldBeTrue();
		combined.HasFlag(TransportHealthCheckCategory.Performance).ShouldBeTrue();
		combined.HasFlag(TransportHealthCheckCategory.Resources).ShouldBeFalse();
	}

	[Fact]
	public void CanCombineMultipleFlags()
	{
		// Arrange
		var combined = TransportHealthCheckCategory.Connectivity |
		               TransportHealthCheckCategory.Performance |
		               TransportHealthCheckCategory.Resources;

		// Assert
		((int)combined).ShouldBe(7);
	}

	[Fact]
	public void CombiningAllFlags_EqualsAll()
	{
		// Arrange
		var combined = TransportHealthCheckCategory.Connectivity |
		               TransportHealthCheckCategory.Performance |
		               TransportHealthCheckCategory.Resources |
		               TransportHealthCheckCategory.Configuration;

		// Assert
		combined.ShouldBe(TransportHealthCheckCategory.All);
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void None_ToString_ReturnsNone()
	{
		// Assert
		TransportHealthCheckCategory.None.ToString().ShouldBe("None");
	}

	[Fact]
	public void SingleFlag_ToString_ReturnsFlagName()
	{
		// Assert
		TransportHealthCheckCategory.Connectivity.ToString().ShouldBe("Connectivity");
	}

	[Fact]
	public void CombinedFlags_ToString_ReturnsCommaSeparatedNames()
	{
		// Arrange
		var combined = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance;

		// Act
		var result = combined.ToString();

		// Assert
		result.ShouldContain("Connectivity");
		result.ShouldContain("Performance");
	}

	[Fact]
	public void All_ToString_ReturnsAll()
	{
		// Assert
		TransportHealthCheckCategory.All.ToString().ShouldBe("All");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		TransportHealthCheckCategory category = default;

		// Assert
		category.ShouldBe(TransportHealthCheckCategory.None);
	}

	#endregion

	#region Bitwise Operations Tests

	[Fact]
	public void BitwiseAnd_WithMatchingFlags_ReturnsCommonFlags()
	{
		// Arrange
		var a = TransportHealthCheckCategory.Connectivity | TransportHealthCheckCategory.Performance;
		var b = TransportHealthCheckCategory.Performance | TransportHealthCheckCategory.Resources;

		// Act
		var result = a & b;

		// Assert
		result.ShouldBe(TransportHealthCheckCategory.Performance);
	}

	[Fact]
	public void BitwiseNot_InvertsFlags()
	{
		// Arrange
		var category = TransportHealthCheckCategory.Connectivity;

		// Act - using XOR with All to get complement within valid range
		var complement = TransportHealthCheckCategory.All ^ category;

		// Assert
		complement.HasFlag(TransportHealthCheckCategory.Connectivity).ShouldBeFalse();
		complement.HasFlag(TransportHealthCheckCategory.Performance).ShouldBeTrue();
	}

	#endregion
}
