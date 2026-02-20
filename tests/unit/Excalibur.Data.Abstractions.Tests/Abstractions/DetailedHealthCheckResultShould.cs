// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DetailedHealthCheckResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "HealthChecks")]
public sealed class DetailedHealthCheckResultShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithStatusOnly_StoresStatus()
	{
		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Theory]
	[InlineData(HealthStatus.Healthy)]
	[InlineData(HealthStatus.Degraded)]
	[InlineData(HealthStatus.Unhealthy)]
	public void Create_WithVariousStatuses_StoresCorrectly(HealthStatus status)
	{
		// Act
		var result = new DetailedHealthCheckResult(status);

		// Assert
		result.Status.ShouldBe(status);
	}

	[Fact]
	public void Create_WithDescription_StoresDescription()
	{
		// Arrange
		var description = "Database connection healthy";

		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy, description);

		// Assert
		result.Description.ShouldBe(description);
	}

	[Fact]
	public void Create_WithNullDescription_StoresNull()
	{
		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy, description: null);

		// Assert
		result.Description.ShouldBeNull();
	}

	[Fact]
	public void Create_WithException_StoresException()
	{
		// Arrange
		var exception = new InvalidOperationException("Connection failed");

		// Act
		var result = new DetailedHealthCheckResult(
			HealthStatus.Unhealthy,
			"Database unavailable",
			exception);

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void Create_WithData_StoresData()
	{
		// Arrange
		var data = new Dictionary<string, object>
		{
			["server"] = "db-server-01",
			["version"] = "16.0"
		};

		// Act
		var result = new DetailedHealthCheckResult(
			HealthStatus.Healthy,
			data: data);

		// Assert
		result.Data.Count.ShouldBe(2);
		result.Data["server"].ShouldBe("db-server-01");
		result.Data["version"].ShouldBe("16.0");
	}

	[Fact]
	public void Create_WithNullData_InitializesEmptyDictionary()
	{
		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy, data: null);

		// Assert
		result.Data.ShouldNotBeNull();
		result.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Create_WithResponseTime_StoresResponseTime()
	{
		// Arrange
		var responseTime = TimeSpan.FromMilliseconds(150);

		// Act
		var result = new DetailedHealthCheckResult(
			HealthStatus.Healthy,
			responseTime: responseTime);

		// Assert
		result.ResponseTime.ShouldBe(responseTime);
	}

	[Fact]
	public void Create_WithNullResponseTime_StoresNull()
	{
		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy, responseTime: null);

		// Assert
		result.ResponseTime.ShouldBeNull();
	}

	[Fact]
	public void Create_WithMetrics_StoresMetrics()
	{
		// Arrange
		var metrics = new Dictionary<string, double>
		{
			["latency_ms"] = 25.5,
			["connections"] = 10.0
		};

		// Act
		var result = new DetailedHealthCheckResult(
			HealthStatus.Healthy,
			metrics: metrics);

		// Assert
		result.Metrics.Count.ShouldBe(2);
		result.Metrics["latency_ms"].ShouldBe(25.5);
		result.Metrics["connections"].ShouldBe(10.0);
	}

	[Fact]
	public void Create_WithNullMetrics_InitializesEmptyDictionary()
	{
		// Act
		var result = new DetailedHealthCheckResult(HealthStatus.Healthy, metrics: null);

		// Assert
		result.Metrics.ShouldNotBeNull();
		result.Metrics.ShouldBeEmpty();
	}

	[Fact]
	public void Create_WithAllParameters_StoresAllValues()
	{
		// Arrange
		var status = HealthStatus.Degraded;
		var description = "High latency detected";
		var exception = new TimeoutException("Query timeout");
		var data = new Dictionary<string, object> { ["server"] = "db-01" };
		var responseTime = TimeSpan.FromSeconds(5);
		var metrics = new Dictionary<string, double> { ["latency"] = 5000 };

		// Act
		var result = new DetailedHealthCheckResult(
			status,
			description,
			exception,
			data,
			responseTime,
			metrics);

		// Assert
		result.Status.ShouldBe(status);
		result.Description.ShouldBe(description);
		result.Exception.ShouldBe(exception);
		result.Data["server"].ShouldBe("db-01");
		result.ResponseTime.ShouldBe(responseTime);
		result.Metrics["latency"].ShouldBe(5000);
	}

	#endregion

	#region ToHealthCheckResult Tests

	[Fact]
	public void ToHealthCheckResult_ConvertsStatus()
	{
		// Arrange
		var detailed = new DetailedHealthCheckResult(HealthStatus.Healthy);

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public void ToHealthCheckResult_ConvertsDescription()
	{
		// Arrange
		var description = "All systems operational";
		var detailed = new DetailedHealthCheckResult(HealthStatus.Healthy, description);

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Description.ShouldBe(description);
	}

	[Fact]
	public void ToHealthCheckResult_ConvertsException()
	{
		// Arrange
		var exception = new InvalidOperationException("Failed");
		var detailed = new DetailedHealthCheckResult(HealthStatus.Unhealthy, exception: exception);

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Exception.ShouldBe(exception);
	}

	[Fact]
	public void ToHealthCheckResult_ConvertsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["key"] = "value" };
		var detailed = new DetailedHealthCheckResult(HealthStatus.Healthy, data: data);

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Data["key"].ShouldBe("value");
	}

	[Fact]
	public void ToHealthCheckResult_DoesNotIncludeResponseTime()
	{
		// Arrange
		var detailed = new DetailedHealthCheckResult(
			HealthStatus.Healthy,
			responseTime: TimeSpan.FromMilliseconds(100));

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Data.ContainsKey("ResponseTime").ShouldBeFalse();
	}

	[Fact]
	public void ToHealthCheckResult_DoesNotIncludeMetrics()
	{
		// Arrange
		var metrics = new Dictionary<string, double> { ["latency"] = 50 };
		var detailed = new DetailedHealthCheckResult(
			HealthStatus.Healthy,
			metrics: metrics);

		// Act
		var standard = detailed.ToHealthCheckResult();

		// Assert
		standard.Data.ContainsKey("latency").ShouldBeFalse();
	}

	#endregion
}
