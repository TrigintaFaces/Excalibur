// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportHealthCheckResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportHealthCheckResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsStatus()
	{
		// Arrange & Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"All systems operational",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
	}

	[Fact]
	public void Constructor_SetsDescription()
	{
		// Arrange & Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"All systems operational",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Description.ShouldBe("All systems operational");
	}

	[Fact]
	public void Constructor_WithNullDescription_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			null!,
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100)));
	}

	[Fact]
	public void Constructor_SetsCategories()
	{
		// Arrange & Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.Connectivity,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Categories.ShouldBe(TransportHealthCheckCategory.Connectivity);
	}

	[Fact]
	public void Constructor_SetsDuration()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(250);

		// Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.All,
			duration);

		// Assert
		result.Duration.ShouldBe(duration);
	}

	[Fact]
	public void Constructor_WithNullData_SetsEmptyData()
	{
		// Arrange & Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100),
			data: null);

		// Assert
		_ = result.Data.ShouldNotBeNull();
		result.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100),
			data);

		// Assert
		result.Data.ShouldContainKeyAndValue("key", "value");
	}

	[Fact]
	public void Constructor_WithNullTimestamp_SetsCurrentTime()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100),
			timestamp: null);

		var after = DateTimeOffset.UtcNow;

		// Assert
		result.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		result.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Constructor_WithTimestamp_SetsTimestamp()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100),
			timestamp: timestamp);

		// Assert
		result.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region IsHealthy Tests

	[Fact]
	public void IsHealthy_WhenStatusIsHealthy_ReturnsTrue()
	{
		// Arrange
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Healthy,
			"All good",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	[Fact]
	public void IsHealthy_WhenStatusIsDegraded_ReturnsFalse()
	{
		// Arrange
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Degraded,
			"Some issues",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public void IsHealthy_WhenStatusIsUnhealthy_ReturnsFalse()
	{
		// Arrange
		var result = new TransportHealthCheckResult(
			TransportHealthStatus.Unhealthy,
			"Critical failure",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	#endregion

	#region Healthy Factory Tests

	[Fact]
	public void Healthy_SetsStatusToHealthy()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy(
			"All systems operational",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Healthy);
	}

	[Fact]
	public void Healthy_SetsDescription()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy(
			"All systems operational",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Description.ShouldBe("All systems operational");
	}

	[Fact]
	public void Healthy_SetsCategories()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test",
			TransportHealthCheckCategory.Connectivity,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Categories.ShouldBe(TransportHealthCheckCategory.Connectivity);
	}

	[Fact]
	public void Healthy_SetsDuration()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(500);

		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test",
			TransportHealthCheckCategory.All,
			duration);

		// Assert
		result.Duration.ShouldBe(duration);
	}

	[Fact]
	public void Healthy_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["connections"] = 10 };

		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100),
			data);

		// Assert
		result.Data.ShouldContainKeyAndValue("connections", 10);
	}

	[Fact]
	public void Healthy_ReturnsHealthyResult()
	{
		// Act
		var result = TransportHealthCheckResult.Healthy(
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeTrue();
	}

	#endregion

	#region Degraded Factory Tests

	[Fact]
	public void Degraded_SetsStatusToDegraded()
	{
		// Act
		var result = TransportHealthCheckResult.Degraded(
			"High latency detected",
			TransportHealthCheckCategory.Performance,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Degraded);
	}

	[Fact]
	public void Degraded_SetsDescription()
	{
		// Act
		var result = TransportHealthCheckResult.Degraded(
			"High latency detected",
			TransportHealthCheckCategory.Performance,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Description.ShouldBe("High latency detected");
	}

	[Fact]
	public void Degraded_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["latency_ms"] = 500 };

		// Act
		var result = TransportHealthCheckResult.Degraded(
			"High latency",
			TransportHealthCheckCategory.Performance,
			TimeSpan.FromMilliseconds(100),
			data);

		// Assert
		result.Data.ShouldContainKeyAndValue("latency_ms", 500);
	}

	[Fact]
	public void Degraded_ReturnsNotHealthyResult()
	{
		// Act
		var result = TransportHealthCheckResult.Degraded(
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	#endregion

	#region Unhealthy Factory Tests

	[Fact]
	public void Unhealthy_SetsStatusToUnhealthy()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy(
			"Connection failed",
			TransportHealthCheckCategory.Connectivity,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Status.ShouldBe(TransportHealthStatus.Unhealthy);
	}

	[Fact]
	public void Unhealthy_SetsDescription()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy(
			"Connection failed",
			TransportHealthCheckCategory.Connectivity,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.Description.ShouldBe("Connection failed");
	}

	[Fact]
	public void Unhealthy_WithData_SetsData()
	{
		// Arrange
		var data = new Dictionary<string, object> { ["error"] = "Connection refused" };

		// Act
		var result = TransportHealthCheckResult.Unhealthy(
			"Connection failed",
			TransportHealthCheckCategory.Connectivity,
			TimeSpan.FromMilliseconds(100),
			data);

		// Assert
		result.Data.ShouldContainKeyAndValue("error", "Connection refused");
	}

	[Fact]
	public void Unhealthy_ReturnsNotHealthyResult()
	{
		// Act
		var result = TransportHealthCheckResult.Unhealthy(
			"Test",
			TransportHealthCheckCategory.All,
			TimeSpan.FromMilliseconds(100));

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	#endregion
}
