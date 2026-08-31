// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Data.Tests.Persistence;

[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PersistenceHealthCheckShould
{
	private const string ProviderKey = "test-provider";

	// ----------------------------------------------------------------------------------
	// LIVENESS: the check must actually resolve a REAL provider through REAL keyed DI and
	// report it healthy. Every assertion below this block is a safety assertion, and each
	// one of them is also satisfied by a check that can resolve nothing at all.
	// ----------------------------------------------------------------------------------

	[Fact]
	public async Task ResolveARealProviderThroughKeyedDiAndReportHealthy()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPersistence();
		_ = services.AddExcaliburInMemory();
		using var container = services.BuildServiceProvider();

		// The real InMemory package registers under "inmemory"; nothing here hands the check
		// a provider - it has to find it in the container by key.
		var sut = new PersistenceHealthCheck(container, "inmemory", NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("inmemory");
	}

	[Fact]
	public async Task ResolveTheDefaultKeyRegisteredByAProviderPackage()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPersistence();
		_ = services.AddExcaliburInMemory();
		using var container = services.BuildServiceProvider();

		var sut = new PersistenceHealthCheck(container, "default", NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public void BeConstructibleByAddPersistenceHealthCheckAgainstTheRealContainer()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPersistence();
		_ = services.AddExcaliburInMemory();
		_ = services.AddHealthChecks().AddPersistenceHealthCheck("inmemory", name: "persistence-inmemory");
		using var container = services.BuildServiceProvider();

		var registration = container
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.Single(r => r.Name == "persistence-inmemory");

		var check = registration.Factory(container);

		_ = check.ShouldNotBeNull();
	}

	// ----------------------------------------------------------------------------------
	// SAFETY
	// ----------------------------------------------------------------------------------

	[Fact]
	public void ThrowOnNullServiceProvider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(null!, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		using var container = EmptyContainer();

		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(container, ProviderKey, null!));
	}

	[Fact]
	public void ThrowOnNullProviderKey()
	{
		using var container = EmptyContainer();

		Should.Throw<ArgumentNullException>(() =>
			new PersistenceHealthCheck(container, null!, NullLogger<PersistenceHealthCheck>.Instance));
	}

	[Fact]
	public async Task ReturnUnhealthyWhenNoProviderIsRegisteredForTheKey()
	{
		using var container = EmptyContainer();
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain(ProviderKey);
	}

	[Fact]
	public async Task ReturnDegradedWhenProviderDoesNotSupportHealthChecks()
	{
		var fakeProvider = A.Fake<IPersistenceProvider>();
		A.CallTo(() => fakeProvider.GetService(typeof(IPersistenceProviderHealth))).Returns(null);
		using var container = ContainerKeyedTo(fakeProvider);
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("does not support health checks");
	}

	[Fact]
	public async Task ReturnHealthyWithMetrics()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._)).Returns(true);
		A.CallTo(() => fakeHealth.GetMetricsAsync(A<CancellationToken>._))
			.Returns(new Dictionary<string, object>(StringComparer.Ordinal) { ["connections"] = 5 });
		using var container = ContainerKeyedTo(fakeProvider);
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data["connections"].ShouldBe(5);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenConnectionFails()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._)).Returns(false);
		using var container = ContainerKeyedTo(fakeProvider);
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("connection test failed");
	}

	[Fact]
	public async Task ReturnUnhealthyOnException()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Connection refused"));
		using var container = ContainerKeyedTo(fakeProvider);
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
		_ = result.Exception.ShouldNotBeNull();
		result.Data["error"].ShouldBe("Connection refused");
	}

	[Fact]
	public async Task ReturnUnhealthyOnTimeout()
	{
		var (fakeProvider, fakeHealth) = CreateProviderWithHealth();
		A.CallTo(() => fakeHealth.TestConnectionAsync(A<CancellationToken>._))
			.Throws(new OperationCanceledException());
		using var container = ContainerKeyedTo(fakeProvider);
		var sut = new PersistenceHealthCheck(container, ProviderKey, NullLogger<PersistenceHealthCheck>.Instance);

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		var result = await sut.CheckHealthAsync(null!, cts.Token);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("timed out");
	}

	private static ServiceProvider EmptyContainer() => new ServiceCollection().BuildServiceProvider();

	private static ServiceProvider ContainerKeyedTo(IPersistenceProvider provider)
	{
		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton(ProviderKey, provider);
		return services.BuildServiceProvider();
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
