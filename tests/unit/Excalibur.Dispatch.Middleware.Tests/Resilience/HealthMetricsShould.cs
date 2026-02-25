// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="HealthMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class HealthMetricsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var metrics = new HealthMetrics();

		// Assert
		metrics.CpuUsagePercent.ShouldBe(0);
		metrics.MemoryUsagePercent.ShouldBe(0);
		metrics.ErrorRate.ShouldBe(0);
		metrics.ResponseTimeMs.ShouldBe(0);
		metrics.ActiveConnections.ShouldBe(0);
		metrics.Timestamp.ShouldBe(default);
	}

	[Fact]
	public void CpuUsagePercent_CanBeSet()
	{
		// Arrange & Act
		var metrics = new HealthMetrics { CpuUsagePercent = 75.5 };

		// Assert
		metrics.CpuUsagePercent.ShouldBe(75.5);
	}

	[Fact]
	public void MemoryUsagePercent_CanBeSet()
	{
		// Arrange & Act
		var metrics = new HealthMetrics { MemoryUsagePercent = 80.0 };

		// Assert
		metrics.MemoryUsagePercent.ShouldBe(80.0);
	}

	[Fact]
	public void ErrorRate_CanBeSet()
	{
		// Arrange & Act
		var metrics = new HealthMetrics { ErrorRate = 0.05 };

		// Assert
		metrics.ErrorRate.ShouldBe(0.05);
	}

	[Fact]
	public void ResponseTimeMs_CanBeSet()
	{
		// Arrange & Act
		var metrics = new HealthMetrics { ResponseTimeMs = 150.5 };

		// Assert
		metrics.ResponseTimeMs.ShouldBe(150.5);
	}

	[Fact]
	public void ActiveConnections_CanBeSet()
	{
		// Arrange & Act
		var metrics = new HealthMetrics { ActiveConnections = 100 };

		// Assert
		metrics.ActiveConnections.ShouldBe(100);
	}

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var metrics = new HealthMetrics { Timestamp = timestamp };

		// Assert
		metrics.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllProperties_CanBeSetTogether()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var metrics = new HealthMetrics
		{
			CpuUsagePercent = 65.0,
			MemoryUsagePercent = 70.0,
			ErrorRate = 0.02,
			ResponseTimeMs = 100.0,
			ActiveConnections = 50,
			Timestamp = timestamp
		};

		// Assert
		metrics.CpuUsagePercent.ShouldBe(65.0);
		metrics.MemoryUsagePercent.ShouldBe(70.0);
		metrics.ErrorRate.ShouldBe(0.02);
		metrics.ResponseTimeMs.ShouldBe(100.0);
		metrics.ActiveConnections.ShouldBe(50);
		metrics.Timestamp.ShouldBe(timestamp);
	}
}
