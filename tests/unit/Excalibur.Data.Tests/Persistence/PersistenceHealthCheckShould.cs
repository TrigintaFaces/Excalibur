// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Data.Tests.Persistence;

public sealed class PersistenceHealthCheckShould
{
	private readonly IPersistenceProviderFactory _fakeFactory;
	private readonly PersistenceHealthCheck _sut;

	public PersistenceHealthCheckShould()
	{
		_fakeFactory = A.Fake<IPersistenceProviderFactory>();
		_sut = new PersistenceHealthCheck(
			_fakeFactory,
			NullLogger<PersistenceHealthCheck>.Instance,
			"test-provider");
	}

	[Fact]
	public void ThrowOnNullFactory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(null!, NullLogger<PersistenceHealthCheck>.Instance, "test"));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(_fakeFactory, null!, "test"));
	}

	[Fact]
	public void ThrowOnNullProviderName()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(_fakeFactory, NullLogger<PersistenceHealthCheck>.Instance, null!));
	}

	[Fact]
	public async Task ReturnUnhealthyWhenProviderNotFound()
	{
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(null);

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not found");
	}

	[Fact]
	public async Task ReturnHealthyWhenConnectionSucceeds()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(fakeProvider);
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Returns(true);
		A.CallTo(() => fakeHealth.GetMetricsAsync(A<CancellationToken>._))
			.Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("healthy");
	}

	[Fact]
	public async Task ReturnHealthyWithMetrics()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(fakeProvider);
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Returns(true);
		A.CallTo(() => fakeHealth.GetMetricsAsync(A<CancellationToken>._))
			.Returns(new Dictionary<string, object>(StringComparer.Ordinal) { ["connections"] = 5 });

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data["connections"].ShouldBe(5);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenConnectionFails()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(fakeProvider);
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Returns(false);

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("connection test failed");
	}

	[Fact]
	public async Task ReturnUnhealthyOnException()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(fakeProvider);
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection refused"));

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
		result.Exception.ShouldNotBeNull();
		result.Data["error"].ShouldBe("Connection refused");
	}

	[Fact]
	public async Task ReturnUnhealthyOnTimeout()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => _fakeFactory.GetProvider("test-provider"))
			.Returns(fakeProvider);
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Throws(new OperationCanceledException());

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("timed out");
	}

	private static (IPersistenceProvider Provider, IPersistenceProviderHealth Health) CreateProviderWithHealth()
	{
		var fakeProvider = A.Fake<IPersistenceProvider>();
		var fakeHealth = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => fakeProvider.GetService(typeof(IPersistenceProviderHealth)))
			.Returns(fakeHealth);
		return (fakeProvider, fakeHealth);
	}
}
