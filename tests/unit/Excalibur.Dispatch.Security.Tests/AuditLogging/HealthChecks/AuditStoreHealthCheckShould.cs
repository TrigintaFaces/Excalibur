// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.HealthChecks;

/// <summary>
/// Unit tests for <see cref="AuditStoreHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HealthCheck")]
public sealed class AuditStoreHealthCheckShould
{
	private readonly IAuditStore _fakeAuditStore = A.Fake<IAuditStore>();
	private readonly ILogger<AuditStoreHealthCheck> _logger = NullLogger<AuditStoreHealthCheck>.Instance;

	[Fact]
	public void ThrowArgumentNullException_WhenAuditStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AuditStoreHealthCheck(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new AuditStoreHealthCheck(_fakeAuditStore, null!));
	}

	[Fact]
	public async Task ReturnHealthy_WhenStoreRespondsQuickly()
	{
		// Arrange
		A.CallTo(() => _fakeAuditStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(42L);

		var healthCheck = new AuditStoreHealthCheck(_fakeAuditStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("audit", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("42");
		result.Data.ShouldContainKey("store_type");
		result.Data.ShouldContainKey("duration_ms");
		result.Data.ShouldContainKey("total_events");
		((long)result.Data["total_events"]).ShouldBe(42L);
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenStoreThrows()
	{
		// Arrange
		A.CallTo(() => _fakeAuditStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection failed"));

		var healthCheck = new AuditStoreHealthCheck(_fakeAuditStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("audit", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("Connection failed");
		result.Exception.ShouldNotBeNull();
		result.Data.ShouldContainKey("duration_ms");
	}

	[Fact]
	public async Task ReturnDegraded_WhenStoreRespondsSlowly()
	{
		// Arrange — use a tiny threshold so the store always seems slow
		A.CallTo(() => _fakeAuditStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(50);
				return 10L;
			});

		var healthCheck = new AuditStoreHealthCheck(
			_fakeAuditStore, _logger, degradedThreshold: TimeSpan.FromMilliseconds(1));
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("audit", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("slowly");
	}

	[Fact]
	public async Task IncludeStoreTypeInData()
	{
		// Arrange
		A.CallTo(() => _fakeAuditStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(0L);

		var healthCheck = new AuditStoreHealthCheck(_fakeAuditStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("audit", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Data["store_type"].ShouldBeOfType<string>();
		((string)result.Data["store_type"]).ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task UseDefaultDegradedThreshold_WhenNotSpecified()
	{
		// Arrange — default threshold is 500ms; a fast store should be healthy
		A.CallTo(() => _fakeAuditStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
			.Returns(5L);

		var healthCheck = new AuditStoreHealthCheck(_fakeAuditStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("audit", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}
}
