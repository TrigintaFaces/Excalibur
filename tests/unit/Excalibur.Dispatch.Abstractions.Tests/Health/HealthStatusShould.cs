// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Health;

namespace Excalibur.Dispatch.Abstractions.Tests.Health;

/// <summary>
/// Unit tests for <see cref="HealthStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Health")]
[Trait("Priority", "0")]
public sealed class HealthStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Healthy_HasExpectedValue()
	{
		// Assert
		((int)HealthStatus.Healthy).ShouldBe(0);
	}

	[Fact]
	public void Unhealthy_HasExpectedValue()
	{
		// Assert
		((int)HealthStatus.Unhealthy).ShouldBe(1);
	}

	[Fact]
	public void Unknown_HasExpectedValue()
	{
		// Assert
		((int)HealthStatus.Unknown).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<HealthStatus>();

		// Assert
		values.ShouldContain(HealthStatus.Healthy);
		values.ShouldContain(HealthStatus.Unhealthy);
		values.ShouldContain(HealthStatus.Unknown);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<HealthStatus>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(HealthStatus.Healthy, "Healthy")]
	[InlineData(HealthStatus.Unhealthy, "Unhealthy")]
	[InlineData(HealthStatus.Unknown, "Unknown")]
	public void ToString_ReturnsExpectedValue(HealthStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Healthy", HealthStatus.Healthy)]
	[InlineData("Unhealthy", HealthStatus.Unhealthy)]
	[InlineData("Unknown", HealthStatus.Unknown)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, HealthStatus expected)
	{
		// Act
		var result = Enum.Parse<HealthStatus>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("healthy")]
	[InlineData("HEALTHY")]
	[InlineData("unhealthy")]
	[InlineData("UNHEALTHY")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<HealthStatus>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<HealthStatus>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsHealthy()
	{
		// Arrange
		HealthStatus status = default;

		// Assert
		status.ShouldBe(HealthStatus.Healthy);
	}

	#endregion
}
