// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.ConnectionPooling;

/// <summary>
/// Unit tests for <see cref="ConnectionPoolStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ConnectionPooling")]
[Trait("Priority", "0")]
public sealed class ConnectionPoolStatisticsShould
{
	#region Default Values Tests

	[Fact]
	public void Default_PoolNameIsEmpty()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.PoolName.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_ConnectionTypeIsEmpty()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.ConnectionType.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_TotalConnectionsCreatedIsZero()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.TotalConnectionsCreated.ShouldBe(0);
	}

	[Fact]
	public void Default_CurrentConnectionsIsZero()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.CurrentConnections.ShouldBe(0);
	}

	[Fact]
	public void Default_ActiveConnectionsIsZero()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.ActiveConnections.ShouldBe(0);
	}

	[Fact]
	public void Default_AvailableConnectionsIsZero()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.AvailableConnections.ShouldBe(0);
	}

	[Fact]
	public void Default_MaxConnectionsIsZero()
	{
		// Arrange & Act
		var stats = new ConnectionPoolStatistics();

		// Assert
		stats.MaxConnections.ShouldBe(0);
	}

	[Fact]
	public void Default_CapturedAtIsSet()
	{
		// Arrange & Act
		var before = DateTime.UtcNow;
		var stats = new ConnectionPoolStatistics();
		var after = DateTime.UtcNow;

		// Assert
		stats.CapturedAt.ShouldBeGreaterThanOrEqualTo(before);
		stats.CapturedAt.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void PoolName_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.PoolName = "TestPool";

		// Assert
		stats.PoolName.ShouldBe("TestPool");
	}

	[Fact]
	public void ConnectionType_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.ConnectionType = "SqlConnection";

		// Assert
		stats.ConnectionType.ShouldBe("SqlConnection");
	}

	[Fact]
	public void TotalConnectionsCreated_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.TotalConnectionsCreated = 100;

		// Assert
		stats.TotalConnectionsCreated.ShouldBe(100);
	}

	[Fact]
	public void CurrentConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.CurrentConnections = 50;

		// Assert
		stats.CurrentConnections.ShouldBe(50);
	}

	[Fact]
	public void ActiveConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.ActiveConnections = 30;

		// Assert
		stats.ActiveConnections.ShouldBe(30);
	}

	[Fact]
	public void AvailableConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.AvailableConnections = 20;

		// Assert
		stats.AvailableConnections.ShouldBe(20);
	}

	[Fact]
	public void MaxConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.MaxConnections = 100;

		// Assert
		stats.MaxConnections.ShouldBe(100);
	}

	[Fact]
	public void MinConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.MinConnections = 5;

		// Assert
		stats.MinConnections.ShouldBe(5);
	}

	[Fact]
	public void TotalAcquisitions_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.TotalAcquisitions = 1000;

		// Assert
		stats.TotalAcquisitions.ShouldBe(1000);
	}

	[Fact]
	public void PoolHits_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.PoolHits = 900;

		// Assert
		stats.PoolHits.ShouldBe(900);
	}

	[Fact]
	public void PoolMisses_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.PoolMisses = 100;

		// Assert
		stats.PoolMisses.ShouldBe(100);
	}

	[Fact]
	public void AcquisitionFailures_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.AcquisitionFailures = 5;

		// Assert
		stats.AcquisitionFailures.ShouldBe(5);
	}

	[Fact]
	public void HealthCheckFailures_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.HealthCheckFailures = 3;

		// Assert
		stats.HealthCheckFailures.ShouldBe(3);
	}

	[Fact]
	public void ExpiredConnections_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.ExpiredConnections = 10;

		// Assert
		stats.ExpiredConnections.ShouldBe(10);
	}

	[Fact]
	public void AverageAcquisitionTime_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.AverageAcquisitionTime = TimeSpan.FromMilliseconds(50);

		// Assert
		stats.AverageAcquisitionTime.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void MaxAcquisitionTime_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.MaxAcquisitionTime = TimeSpan.FromMilliseconds(500);

		// Assert
		stats.MaxAcquisitionTime.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void AverageConnectionLifetime_CanBeSet()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics();

		// Act
		stats.AverageConnectionLifetime = TimeSpan.FromMinutes(15);

		// Assert
		stats.AverageConnectionLifetime.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region Computed Properties Tests

	[Fact]
	public void HitRatePercentage_WithZeroTotalAcquisitions_ReturnsZero()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 0,
			PoolHits = 0,
		};

		// Act & Assert
		stats.HitRatePercentage.ShouldBe(0.0);
	}

	[Fact]
	public void HitRatePercentage_WithAllHits_ReturnsHundred()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 100,
			PoolHits = 100,
		};

		// Act & Assert
		stats.HitRatePercentage.ShouldBe(100.0);
	}

	[Fact]
	public void HitRatePercentage_WithPartialHits_ReturnsCorrectPercentage()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 100,
			PoolHits = 75,
		};

		// Act & Assert
		stats.HitRatePercentage.ShouldBe(75.0);
	}

	[Fact]
	public void UtilizationPercentage_WithZeroMaxConnections_ReturnsZero()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			MaxConnections = 0,
			ActiveConnections = 0,
		};

		// Act & Assert
		stats.UtilizationPercentage.ShouldBe(0.0);
	}

	[Fact]
	public void UtilizationPercentage_WithFullUtilization_ReturnsHundred()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			MaxConnections = 100,
			ActiveConnections = 100,
		};

		// Act & Assert
		stats.UtilizationPercentage.ShouldBe(100.0);
	}

	[Fact]
	public void UtilizationPercentage_WithPartialUtilization_ReturnsCorrectPercentage()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			MaxConnections = 100,
			ActiveConnections = 50,
		};

		// Act & Assert
		stats.UtilizationPercentage.ShouldBe(50.0);
	}

	[Fact]
	public void FailureRatePercentage_WithZeroTotalAcquisitions_ReturnsZero()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 0,
			AcquisitionFailures = 0,
		};

		// Act & Assert
		stats.FailureRatePercentage.ShouldBe(0.0);
	}

	[Fact]
	public void FailureRatePercentage_WithNoFailures_ReturnsZero()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 100,
			AcquisitionFailures = 0,
		};

		// Act & Assert
		stats.FailureRatePercentage.ShouldBe(0.0);
	}

	[Fact]
	public void FailureRatePercentage_WithPartialFailures_ReturnsCorrectPercentage()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			TotalAcquisitions = 100,
			AcquisitionFailures = 5,
		};

		// Act & Assert
		stats.FailureRatePercentage.ShouldBe(5.0);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var stats = new ConnectionPoolStatistics
		{
			PoolName = "TestPool",
			CurrentConnections = 50,
			MaxConnections = 100,
			ActiveConnections = 30,
			AvailableConnections = 20,
			TotalAcquisitions = 1000,
			PoolHits = 900,
		};

		// Act
		var result = stats.ToString();

		// Assert
		result.ShouldContain("TestPool");
		result.ShouldContain("50/100");
		result.ShouldContain("30 active");
		result.ShouldContain("20 available");
		result.ShouldContain("Hit Rate: 90.0%");
		result.ShouldContain("Utilization: 30.0%");
	}

	#endregion
}
