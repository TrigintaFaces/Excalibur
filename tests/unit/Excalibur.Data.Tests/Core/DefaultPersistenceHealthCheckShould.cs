// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Persistence;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultPersistenceHealthCheckShould
{
	private readonly IPersistenceProviderFactory _factory;
	private readonly DefaultPersistenceHealthCheck _healthCheck;

	public DefaultPersistenceHealthCheckShould()
	{
		_factory = A.Fake<IPersistenceProviderFactory>();
		var logger = NullLogger<DefaultPersistenceHealthCheck>.Instance;
		_healthCheck = new DefaultPersistenceHealthCheck(_factory, logger);
	}

	[Fact]
	public void HaveCorrectHealthCheckName()
	{
		_healthCheck.HealthCheckName.ShouldBe("PersistenceHealthCheck");
	}

	[Fact]
	public void HaveDefaultTags()
	{
		_healthCheck.Tags.ShouldContain("persistence");
		_healthCheck.Tags.ShouldContain("database");
		_healthCheck.Tags.ShouldContain("ready");
	}

	[Fact]
	public void HaveDefaultTimeout()
	{
		_healthCheck.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingTimeout()
	{
		_healthCheck.Timeout = TimeSpan.FromMinutes(2);
		_healthCheck.Timeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ThrowForZeroTimeout()
	{
		Should.Throw<ArgumentException>(() => _healthCheck.Timeout = TimeSpan.Zero);
	}

	[Fact]
	public void ThrowForNegativeTimeout()
	{
		Should.Throw<ArgumentException>(() => _healthCheck.Timeout = TimeSpan.FromSeconds(-1));
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthyWhenProviderNotFound()
	{
		A.CallTo(() => _factory.GetProvider("test"))
			.Returns(null!);

		var result = await _healthCheck.CheckHealthAsync("test", CancellationToken.None);

		result.IsHealthy.ShouldBeFalse();
		result.ProviderName.ShouldBe("test");
		result.Message.ShouldContain("not found");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthyWhenNoHealthSupport()
	{
		var provider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth)))
			.Returns(null);
		A.CallTo(() => _factory.GetProvider("test")).Returns(provider);

		var result = await _healthCheck.CheckHealthAsync("test", CancellationToken.None);

		result.IsHealthy.ShouldBeFalse();
		result.Message.ShouldContain("does not support health checks");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsHealthyWhenConnected()
	{
		var provider = A.Fake<IPersistenceProvider>();
		var health = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => health.TestConnectionAsync(A<CancellationToken>._))
			.Returns(true);
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth)))
			.Returns(health);
		A.CallTo(() => _factory.GetProvider("test")).Returns(provider);

		var result = await _healthCheck.CheckHealthAsync("test", CancellationToken.None);

		result.IsHealthy.ShouldBeTrue();
		result.ProviderName.ShouldBe("test");
	}

	[Fact]
	public async Task CheckHealthAsync_ReturnsUnhealthyWhenConnectionFails()
	{
		var provider = A.Fake<IPersistenceProvider>();
		var health = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => health.TestConnectionAsync(A<CancellationToken>._))
			.Returns(false);
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth)))
			.Returns(health);
		A.CallTo(() => _factory.GetProvider("test")).Returns(provider);

		var result = await _healthCheck.CheckHealthAsync("test", CancellationToken.None);

		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task CheckHealthAsync_HandlesExceptions()
	{
		A.CallTo(() => _factory.GetProvider("test"))
			.Throws(new InvalidOperationException("Connection refused"));

		var result = await _healthCheck.CheckHealthAsync("test", CancellationToken.None);

		result.IsHealthy.ShouldBeFalse();
		result.Message.ShouldContain("Connection refused");
	}

	[Fact]
	public async Task CheckHealthAsync_ThrowsForEmptyProviderName()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _healthCheck.CheckHealthAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task CheckDetailedHealthAsync_ThrowsForNullProvider()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _healthCheck.CheckDetailedHealthAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CheckDetailedHealthAsync_ReturnsHealthyResult()
	{
		var provider = A.Fake<IPersistenceProvider>();
		var health = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => health.TestConnectionAsync(A<CancellationToken>._)).Returns(true);
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth))).Returns(health);
		A.CallTo(() => provider.Name).Returns("TestProvider");

		var result = await _healthCheck.CheckDetailedHealthAsync(provider, CancellationToken.None);

		result.ShouldNotBeNull();
		result.Status.ShouldBe(HealthStatus.Healthy);
		(result.ResponseTime >= TimeSpan.Zero).ShouldBeTrue();
	}

	[Fact]
	public async Task CheckDetailedHealthAsync_ReturnsDegradedWhenNoHealthSupport()
	{
		var provider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth))).Returns(null);
		A.CallTo(() => provider.Name).Returns("TestProvider");

		var result = await _healthCheck.CheckDetailedHealthAsync(provider, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public async Task CheckDetailedHealthAsync_ReturnsUnhealthyOnException()
	{
		var provider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth)))
			.Throws(new InvalidOperationException("test error"));
		A.CallTo(() => provider.Name).Returns("TestProvider");

		var result = await _healthCheck.CheckDetailedHealthAsync(provider, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public async Task CheckAllProvidersAsync_ChecksEachProvider()
	{
		var provider1 = A.Fake<IPersistenceProvider>();
		var provider2 = A.Fake<IPersistenceProvider>();
		var health1 = A.Fake<IPersistenceProviderHealth>();
		var health2 = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => health1.TestConnectionAsync(A<CancellationToken>._)).Returns(true);
		A.CallTo(() => health2.TestConnectionAsync(A<CancellationToken>._)).Returns(true);
		A.CallTo(() => provider1.GetService(typeof(IPersistenceProviderHealth))).Returns(health1);
		A.CallTo(() => provider2.GetService(typeof(IPersistenceProviderHealth))).Returns(health2);
		A.CallTo(() => _factory.GetProvider("p1")).Returns(provider1);
		A.CallTo(() => _factory.GetProvider("p2")).Returns(provider2);
		A.CallTo(() => _factory.GetProviderNames()).Returns(["p1", "p2"]);

		var results = (await _healthCheck.CheckAllProvidersAsync(CancellationToken.None)).ToList();

		results.Count.ShouldBe(2);
		results.ShouldAllBe(r => r.IsHealthy);
	}

	[Fact]
	public async Task CheckHealthAsync_IHealthCheck_ReturnsHealthyWhenAllProvidersHealthy()
	{
		var provider = A.Fake<IPersistenceProvider>();
		var health = A.Fake<IPersistenceProviderHealth>();
		A.CallTo(() => health.TestConnectionAsync(A<CancellationToken>._)).Returns(true);
		A.CallTo(() => provider.GetService(typeof(IPersistenceProviderHealth))).Returns(health);
		A.CallTo(() => _factory.GetProvider("p1")).Returns(provider);
		A.CallTo(() => _factory.GetProviderNames()).Returns(["p1"]);

		var context = new HealthCheckContext();
		var result = await ((IHealthCheck)_healthCheck).CheckHealthAsync(context, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public void ThrowWhenProviderFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new DefaultPersistenceHealthCheck(null!, NullLogger<DefaultPersistenceHealthCheck>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var factory = A.Fake<IPersistenceProviderFactory>();
		Should.Throw<ArgumentNullException>(
			() => new DefaultPersistenceHealthCheck(factory, null!));
	}
}
