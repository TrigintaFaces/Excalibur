// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;
using Excalibur.Data.Postgres.Persistence;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Verifies that the Postgres provider the documented registration actually wires answers capability
/// queries. <see cref="IPersistenceProvider.GetService"/> has a default implementation returning
/// <see langword="null"/>, so a provider that implements a capability but does not override it declines
/// that capability silently -- no compiler error, no analyzer warning, and no runtime failure until a
/// caller receives <see langword="null"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.Postgres")]
public sealed class PostgresPersistenceCapabilityShould
{
	private const string TestConnectionString =
		"Host=localhost;Database=excalibur_capability_test;Username=postgres;Password=postgres";

	[Fact]
	public void ExposeHealthThroughGetServiceOnTheDiResolvedProvider()
	{
		using var provider = BuildProvider();

		var resolved = provider.GetRequiredService<ISqlPersistenceProvider>();

		var health = resolved.GetService(typeof(IPersistenceProviderHealth));

		_ = health.ShouldNotBeNull().ShouldBeAssignableTo<IPersistenceProviderHealth>();
	}

	[Fact]
	public void ExposeTransactionThroughGetServiceOnTheDiResolvedProvider()
	{
		using var provider = BuildProvider();

		var resolved = provider.GetRequiredService<ISqlPersistenceProvider>();

		var transaction = resolved.GetService(typeof(IPersistenceProviderTransaction));

		_ = transaction.ShouldNotBeNull().ShouldBeAssignableTo<IPersistenceProviderTransaction>();
	}

	[Fact]
	public void ExposeCapabilitiesWhenViewedThroughTheBasePersistenceProviderContract()
	{
		using var provider = BuildProvider();

		// ISqlPersistenceProvider re-declares GetService with `new`, so a consumer holding the base
		// contract binds a different method. Both must answer.
		IPersistenceProvider resolved = provider.GetRequiredKeyedService<IPersistenceProvider>("postgres");

		resolved.GetService(typeof(IPersistenceProviderHealth)).ShouldNotBeNull();
		resolved.GetService(typeof(IPersistenceProviderTransaction)).ShouldNotBeNull();
	}

	[Fact]
	public void ReturnNullForAnUnsupportedCapability()
	{
		using var provider = BuildProvider();

		var resolved = provider.GetRequiredService<ISqlPersistenceProvider>();

		resolved.GetService(typeof(IDisposable)).ShouldBeNull();
	}

	[Fact]
	public void RejectANullServiceType()
	{
		using var provider = BuildProvider();

		var resolved = provider.GetRequiredService<ISqlPersistenceProvider>();

		_ = Should.Throw<ArgumentNullException>(() => resolved.GetService(null!));
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPostgresPersistence(TestConnectionString);

		return services.BuildServiceProvider(new ServiceProviderOptions
		{
			ValidateOnBuild = false,
			ValidateScopes = true,
		});
	}
}
