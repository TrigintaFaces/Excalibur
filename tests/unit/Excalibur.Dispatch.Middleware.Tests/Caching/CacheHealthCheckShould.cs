// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheHealthCheckShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenMonitorIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new CacheHealthCheck(null!));
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthy_WhenCacheIsReachable()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		var healthStatus = new CacheHealthStatus
		{
			IsHealthy = true,
			ConnectionStatus = "Connected",
			ResponseTimeMs = 1.5,
		};
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(healthStatus);

		var healthCheck = new CacheHealthCheck(monitor);

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(),
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("Connected");
		result.Description.ShouldContain("1.5");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsDegraded_WhenCacheIsUnhealthy()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		var healthStatus = new CacheHealthStatus
		{
			IsHealthy = false,
			ConnectionStatus = "Disconnected",
			ResponseTimeMs = 0,
		};
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(healthStatus);

		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration(
				"test-cache",
				A.Fake<IHealthCheck>(),
				HealthStatus.Degraded,
				[]),
		};

		var healthCheck = new CacheHealthCheck(monitor);

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("Disconnected");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthy_WhenMonitorThrows()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		var expectedException = new InvalidOperationException("Connection refused");
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Throws(expectedException);

		var healthCheck = new CacheHealthCheck(monitor);

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(),
			CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
		result.Exception.ShouldBe(expectedException);
	}

	[Fact]
	public async Task CheckHealthAsync_UsesDegradedFromContext_WhenUnhealthy()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		var healthStatus = new CacheHealthStatus
		{
			IsHealthy = false,
			ConnectionStatus = "Timeout",
		};
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(healthStatus);

		// Set failure status to Unhealthy instead of default Degraded
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration(
				"test-cache",
				A.Fake<IHealthCheck>(),
				HealthStatus.Unhealthy,
				[]),
		};

		var healthCheck = new CacheHealthCheck(monitor);

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}
}
