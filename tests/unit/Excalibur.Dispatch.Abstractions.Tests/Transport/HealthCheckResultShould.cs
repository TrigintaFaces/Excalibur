// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="HealthCheckResult"/> and <see cref="HealthCheckStatus"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HealthCheckResultShould
{
	#region Constructor Tests (bool overload)

	[Fact]
	public void Constructor_WithTrue_CreatesHealthyResult()
	{
		// Act
		var result = new HealthCheckResult(isHealthy: true);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("Healthy");
		result.Data.ShouldNotBeNull();
		result.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithFalse_CreatesUnhealthyResult()
	{
		// Act
		var result = new HealthCheckResult(isHealthy: false);

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("Unhealthy");
	}

	[Fact]
	public void Constructor_WithDescription_SetsDescription()
	{
		// Act
		var result = new HealthCheckResult(isHealthy: true, description: "All systems operational");

		// Assert
		result.Description.ShouldBe("All systems operational");
	}

	[Fact]
	public void Constructor_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["connections"] = 5 };

		// Act
		var result = new HealthCheckResult(isHealthy: true, data: data);

		// Assert
		result.Data.ShouldContainKey("connections");
		result.Data["connections"].ShouldBe(5);
	}

	#endregion

	#region Constructor Tests (status overload)

	[Fact]
	public void Constructor_WithHealthyStatus_SetsIsHealthyTrue()
	{
		// Act
		var result = new HealthCheckResult(HealthCheckStatus.Healthy, "OK");

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
		result.Description.ShouldBe("OK");
	}

	[Fact]
	public void Constructor_WithDegradedStatus_SetsIsHealthyFalse()
	{
		// Act
		var result = new HealthCheckResult(HealthCheckStatus.Degraded, "High latency");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldBe("High latency");
	}

	[Fact]
	public void Constructor_WithUnhealthyStatus_SetsIsHealthyFalse()
	{
		// Act
		var result = new HealthCheckResult(HealthCheckStatus.Unhealthy, "Connection lost");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
	}

	[Fact]
	public void Constructor_WithNullDescription_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new HealthCheckResult(HealthCheckStatus.Healthy, null!));
	}

	#endregion

	#region Factory Method Tests

	[Fact]
	public void Healthy_FactoryMethod_CreatesHealthyResult()
	{
		// Act
		var result = HealthCheckResult.Healthy();

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Status.ShouldBe(HealthCheckStatus.Healthy);
	}

	[Fact]
	public void Healthy_FactoryMethod_WithDescription_SetsDescription()
	{
		// Act
		var result = HealthCheckResult.Healthy("All good");

		// Assert
		result.Description.ShouldBe("All good");
	}

	[Fact]
	public void Healthy_FactoryMethod_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["uptime"] = "24h" };

		// Act
		var result = HealthCheckResult.Healthy(data: data);

		// Assert
		result.Data.ShouldContainKey("uptime");
	}

	[Fact]
	public void Degraded_FactoryMethod_CreatesDegradedResult()
	{
		// Act
		var result = HealthCheckResult.Degraded("Slow responses");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Degraded);
		result.Description.ShouldBe("Slow responses");
	}

	[Fact]
	public void Unhealthy_FactoryMethod_CreatesUnhealthyResult()
	{
		// Act
		var result = HealthCheckResult.Unhealthy("Service down");

		// Assert
		result.IsHealthy.ShouldBeFalse();
		result.Status.ShouldBe(HealthCheckStatus.Unhealthy);
		result.Description.ShouldBe("Service down");
	}

	#endregion

	#region HealthCheckStatus Enum Tests

	[Fact]
	public void HealthCheckStatus_HasCorrectValues()
	{
		// Assert
		((int)HealthCheckStatus.Healthy).ShouldBe(0);
		((int)HealthCheckStatus.Degraded).ShouldBe(1);
		((int)HealthCheckStatus.Unhealthy).ShouldBe(2);
	}

	#endregion
}
