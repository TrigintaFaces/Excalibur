// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportHealthStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportHealthStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Healthy_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthStatus.Healthy).ShouldBe(0);
	}

	[Fact]
	public void Degraded_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthStatus.Degraded).ShouldBe(1);
	}

	[Fact]
	public void Unhealthy_HasExpectedValue()
	{
		// Assert
		((int)TransportHealthStatus.Unhealthy).ShouldBe(2);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<TransportHealthStatus>();

		// Assert
		values.ShouldContain(TransportHealthStatus.Healthy);
		values.ShouldContain(TransportHealthStatus.Degraded);
		values.ShouldContain(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public void HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<TransportHealthStatus>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(TransportHealthStatus.Healthy, "Healthy")]
	[InlineData(TransportHealthStatus.Degraded, "Degraded")]
	[InlineData(TransportHealthStatus.Unhealthy, "Unhealthy")]
	public void ToString_ReturnsExpectedValue(TransportHealthStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Healthy", TransportHealthStatus.Healthy)]
	[InlineData("Degraded", TransportHealthStatus.Degraded)]
	[InlineData("Unhealthy", TransportHealthStatus.Unhealthy)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, TransportHealthStatus expected)
	{
		// Act
		var result = Enum.Parse<TransportHealthStatus>(value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsHealthy()
	{
		// Arrange
		TransportHealthStatus status = default;

		// Assert
		status.ShouldBe(TransportHealthStatus.Healthy);
	}

	#endregion

	#region Comparison Tests

	[Fact]
	public void Healthy_IsLessThanDegraded()
	{
		// Assert
		(TransportHealthStatus.Healthy < TransportHealthStatus.Degraded).ShouldBeTrue();
	}

	[Fact]
	public void Degraded_IsLessThanUnhealthy()
	{
		// Assert
		(TransportHealthStatus.Degraded < TransportHealthStatus.Unhealthy).ShouldBeTrue();
	}

	#endregion
}
