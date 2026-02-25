// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheHealthCheckShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenMonitorIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new CacheHealthCheck(null!));
	}

	[Fact]
	public async Task ReturnHealthy_WhenCacheIsHealthy()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(new CacheHealthStatus
			{
				IsHealthy = true,
				ConnectionStatus = "Connected",
				ResponseTimeMs = 2.5,
				LastChecked = DateTimeOffset.UtcNow,
			});

		var check = new CacheHealthCheck(monitor);

		// Act
		var result = await check.CheckHealthAsync(
			new HealthCheckContext { Registration = new HealthCheckRegistration("test", check, null, null) },
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("reachable");
	}

	[Fact]
	public async Task ReturnDegraded_WhenCacheIsUnhealthy()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(new CacheHealthStatus
			{
				IsHealthy = false,
				ConnectionStatus = "Disconnected",
				LastChecked = DateTimeOffset.UtcNow,
			});

		var check = new CacheHealthCheck(monitor);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("test", check, HealthStatus.Degraded, null),
		};

		// Act
		var result = await check.CheckHealthAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("unhealthy");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenMonitorThrows()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Connection failed"));

		var check = new CacheHealthCheck(monitor);

		// Act
		var result = await check.CheckHealthAsync(null!, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
		result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public async Task UseFailureStatus_FromRegistration()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(new CacheHealthStatus
			{
				IsHealthy = false,
				ConnectionStatus = "Error",
				LastChecked = DateTimeOffset.UtcNow,
			});

		var check = new CacheHealthCheck(monitor);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("test", check, HealthStatus.Unhealthy, null),
		};

		// Act
		var result = await check.CheckHealthAsync(context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task HandleNullContext_WithDefaultFailureStatus()
	{
		// Arrange
		var monitor = A.Fake<ICacheHealthMonitor>();
		A.CallTo(() => monitor.GetHealthStatusAsync(A<CancellationToken>._))
			.Returns(new CacheHealthStatus
			{
				IsHealthy = false,
				ConnectionStatus = "Error",
				LastChecked = DateTimeOffset.UtcNow,
			});

		var check = new CacheHealthCheck(monitor);

		// Act
		var result = await check.CheckHealthAsync(null!, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
	}
}
