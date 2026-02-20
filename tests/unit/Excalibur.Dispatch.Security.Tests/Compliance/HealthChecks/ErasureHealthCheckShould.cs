// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Dispatch.Security.Tests.Compliance.HealthChecks;

/// <summary>
/// Unit tests for <see cref="ErasureHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HealthCheck")]
public sealed class ErasureHealthCheckShould
{
	private readonly IErasureStore _fakeErasureStore = A.Fake<IErasureStore>();
	private readonly ILogger<ErasureHealthCheck> _logger = NullLogger<ErasureHealthCheck>.Instance;

	[Fact]
	public void ThrowArgumentNullException_WhenErasureStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureHealthCheck(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureHealthCheck(_fakeErasureStore, null!));
	}

	[Fact]
	public async Task ReturnHealthy_WhenStoreRespondsQuickly()
	{
		// Arrange — query for Guid.Empty returns null (no record), which is expected
		A.CallTo(() => _fakeErasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		var healthCheck = new ErasureHealthCheck(_fakeErasureStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("erasure", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("healthy");
		result.Data.ShouldContainKey("store_type");
		result.Data.ShouldContainKey("duration_ms");
		result.Data.ShouldContainKey("probe_result");
		((string)result.Data["probe_result"]).ShouldBe("no_record");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenStoreThrows()
	{
		// Arrange
		A.CallTo(() => _fakeErasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store unavailable"));

		var healthCheck = new ErasureHealthCheck(_fakeErasureStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("erasure", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("Store unavailable");
		result.Exception.ShouldNotBeNull();
		result.Data.ShouldContainKey("duration_ms");
	}

	[Fact]
	public async Task ReturnDegraded_WhenStoreRespondsSlowly()
	{
		// Arrange — use a tiny threshold
		A.CallTo(() => _fakeErasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(50);
				return (ErasureStatus?)null;
			});

		var healthCheck = new ErasureHealthCheck(
			_fakeErasureStore, _logger, degradedThreshold: TimeSpan.FromMilliseconds(1));
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("erasure", healthCheck, null, null),
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
		A.CallTo(() => _fakeErasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Returns((ErasureStatus?)null);

		var healthCheck = new ErasureHealthCheck(_fakeErasureStore, _logger);
		var context = new HealthCheckContext
		{
			Registration = new HealthCheckRegistration("erasure", healthCheck, null, null),
		};

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Data["store_type"].ShouldBeOfType<string>();
		((string)result.Data["store_type"]).ShouldNotBeNullOrWhiteSpace();
	}
}
