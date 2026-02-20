// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Unit tests for <see cref="PoolHealth"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class PoolHealthShould
{
	#region Value Tests

	[Fact]
	public void Healthy_HasValueZero()
	{
		// Assert
		((int)PoolHealth.Healthy).ShouldBe(0);
	}

	[Fact]
	public void Warning_HasValueOne()
	{
		// Assert
		((int)PoolHealth.Warning).ShouldBe(1);
	}

	[Fact]
	public void Critical_HasValueTwo()
	{
		// Assert
		((int)PoolHealth.Critical).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void HasExpectedMemberCount()
	{
		// Arrange
		var values = Enum.GetValues<PoolHealth>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(PoolHealth.Healthy)]
	[InlineData(PoolHealth.Warning)]
	[InlineData(PoolHealth.Critical)]
	public void AllValues_AreDefined(PoolHealth health)
	{
		// Assert
		Enum.IsDefined(health).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void Healthy_ToStringReturnsExpected()
	{
		// Assert
		PoolHealth.Healthy.ToString().ShouldBe("Healthy");
	}

	[Fact]
	public void Warning_ToStringReturnsExpected()
	{
		// Assert
		PoolHealth.Warning.ToString().ShouldBe("Warning");
	}

	[Fact]
	public void Critical_ToStringReturnsExpected()
	{
		// Assert
		PoolHealth.Critical.ToString().ShouldBe("Critical");
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Healthy", PoolHealth.Healthy)]
	[InlineData("Warning", PoolHealth.Warning)]
	[InlineData("Critical", PoolHealth.Critical)]
	public void Parse_ReturnsExpectedValue(string input, PoolHealth expected)
	{
		// Act
		var result = Enum.Parse<PoolHealth>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithIgnoreCase_Succeeds()
	{
		// Act
		var result = Enum.Parse<PoolHealth>("critical", ignoreCase: true);

		// Assert
		result.ShouldBe(PoolHealth.Critical);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsHealthy()
	{
		// Arrange
		var defaultValue = default(PoolHealth);

		// Assert
		defaultValue.ShouldBe(PoolHealth.Healthy);
	}

	#endregion

	#region Severity Order Tests

	[Fact]
	public void StatusValues_AreInSeverityOrder()
	{
		// Assert - Healthy < Warning < Critical
		((int)PoolHealth.Healthy).ShouldBeLessThan((int)PoolHealth.Warning);
		((int)PoolHealth.Warning).ShouldBeLessThan((int)PoolHealth.Critical);
	}

	#endregion
}
