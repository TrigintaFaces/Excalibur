// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="PoolHealthStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class PoolHealthStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Healthy_HasExpectedValue()
	{
		// Assert
		((int)PoolHealthStatus.Healthy).ShouldBe(0);
	}

	[Fact]
	public void Degraded_HasExpectedValue()
	{
		// Assert
		((int)PoolHealthStatus.Degraded).ShouldBe(1);
	}

	[Fact]
	public void Unhealthy_HasExpectedValue()
	{
		// Assert
		((int)PoolHealthStatus.Unhealthy).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<PoolHealthStatus>();

		// Assert
		values.ShouldContain(PoolHealthStatus.Healthy);
		values.ShouldContain(PoolHealthStatus.Degraded);
		values.ShouldContain(PoolHealthStatus.Unhealthy);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<PoolHealthStatus>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(PoolHealthStatus.Healthy, "Healthy")]
	[InlineData(PoolHealthStatus.Degraded, "Degraded")]
	[InlineData(PoolHealthStatus.Unhealthy, "Unhealthy")]
	public void ToString_ReturnsExpectedValue(PoolHealthStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Healthy", PoolHealthStatus.Healthy)]
	[InlineData("Degraded", PoolHealthStatus.Degraded)]
	[InlineData("Unhealthy", PoolHealthStatus.Unhealthy)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, PoolHealthStatus expected)
	{
		// Act
		var result = Enum.Parse<PoolHealthStatus>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsHealthy()
	{
		// Arrange
		PoolHealthStatus status = default;

		// Assert
		status.ShouldBe(PoolHealthStatus.Healthy);
	}

	#endregion
}
