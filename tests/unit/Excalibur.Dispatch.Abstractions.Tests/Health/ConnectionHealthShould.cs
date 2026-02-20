// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Health;

namespace Excalibur.Dispatch.Abstractions.Tests.Health;

/// <summary>
/// Unit tests for <see cref="ConnectionHealth"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConnectionHealthShould
{
	[Fact]
	public void Healthy_ReturnsHealthyInstance()
	{
		// Act
		var health = ConnectionHealth.Healthy();

		// Assert
		health.IsHealthy.ShouldBeTrue();
		health.ErrorMessage.ShouldBeNull();
		health.CheckedAtTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Healthy_SetsResponseTime()
	{
		// Act
		var health = ConnectionHealth.Healthy(responseTimeMs: 42.5);

		// Assert
		health.ResponseTimeMs.ShouldBe(42.5);
	}

	[Fact]
	public void Unhealthy_ReturnsUnhealthyInstance()
	{
		// Act
		var health = ConnectionHealth.Unhealthy("Connection refused");

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.ErrorMessage.ShouldBe("Connection refused");
		health.CheckedAtTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Unhealthy_SetsResponseTime()
	{
		// Act
		var health = ConnectionHealth.Unhealthy("Timeout", responseTimeMs: 5000.0);

		// Assert
		health.ResponseTimeMs.ShouldBe(5000.0);
	}

	[Fact]
	public void StartHealthCheck_ReturnsInstanceWithTimingStarted()
	{
		// Act
		var health = ConnectionHealth.StartHealthCheck();

		// Assert
		health.IsHealthy.ShouldBeFalse(); // Not yet completed
		health.CheckedAtTicks.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CompleteAsHealthy_SetsIsHealthyToTrue()
	{
		// Arrange
		var health = ConnectionHealth.StartHealthCheck();

		// Act
		health.CompleteAsHealthy();

		// Assert
		health.IsHealthy.ShouldBeTrue();
		health.ResponseTimeMs.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void CompleteAsUnhealthy_SetsIsHealthyToFalseAndSetsError()
	{
		// Arrange
		var health = ConnectionHealth.StartHealthCheck();

		// Act
		health.CompleteAsUnhealthy("Database unavailable");

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.ErrorMessage.ShouldBe("Database unavailable");
		health.ResponseTimeMs.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void CheckedAt_ReturnsDateTimeFromTicks()
	{
		// Arrange
		var health = ConnectionHealth.Healthy();

		// Act
		var checkedAt = health.CheckedAt;

		// Assert
		checkedAt.Kind.ShouldBe(DateTimeKind.Utc);
		checkedAt.ShouldBeGreaterThan(DateTime.MinValue);
	}

	[Fact]
	public void Metadata_CanBeInitialized()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			["server"] = "db-primary",
			["latency"] = 15.2,
		};

		// Act
		var health = new ConnectionHealth { Metadata = metadata };

		// Assert
		health.Metadata.ShouldNotBeNull();
		health.Metadata["server"].ShouldBe("db-primary");
	}

	[Fact]
	public void CompleteAsHealthy_WithoutStopwatch_StillSetsIsHealthy()
	{
		// Arrange - default constructor without StartHealthCheck
		var health = new ConnectionHealth();

		// Act
		health.CompleteAsHealthy();

		// Assert
		health.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void CompleteAsUnhealthy_WithoutStopwatch_StillSetsError()
	{
		// Arrange
		var health = new ConnectionHealth();

		// Act
		health.CompleteAsUnhealthy("Error occurred");

		// Assert
		health.IsHealthy.ShouldBeFalse();
		health.ErrorMessage.ShouldBe("Error occurred");
	}
}
