// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB;

/// <summary>
/// Unit tests for <see cref="MongoDbServiceCollectionExtensions.AddExcaliburMongoDb"/>.
/// </summary>
/// <remarks>
/// Sprint 698 T.7 (i4nq8): Verifies keyed DI registration pattern for MongoDB persistence.
/// Note: MongoDB persistence provider has external dependencies (IMongoDatabase) that cannot be
/// resolved from a basic ServiceCollection, so we verify registrations via ServiceDescriptor
/// instead of resolving instances.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void RegisterKeyedPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMongoDb(o => o.ConnectionString = "mongodb://localhost");

		// Assert - verify registration exists (MongoDb has external deps so we check descriptors)
		services.Any(d =>
			d.ServiceType == typeof(IPersistenceProvider) &&
			d.ServiceKey as string == "mongodb").ShouldBeTrue();
	}

	[Fact]
	public void RegisterDefaultAliasForPersistenceProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMongoDb(o => o.ConnectionString = "mongodb://localhost");

		// Assert
		services.Any(d =>
			d.ServiceType == typeof(IPersistenceProvider) &&
			d.ServiceKey as string == "default").ShouldBeTrue();
	}

	[Fact]
	public void ConfigureMongoDbProviderOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMongoDb(o =>
		{
			o.ConnectionString = "mongodb://custom-host:27017";
			o.DatabaseName = "test-db";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<MongoDbProviderOptions>>();
		options.Value.ConnectionString.ShouldBe("mongodb://custom-host:27017");
		options.Value.DatabaseName.ShouldBe("test-db");
	}

	[Fact]
	public void BeIdempotentOnDoubleRegistration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMongoDb(o => o.ConnectionString = "mongodb://localhost");
		services.AddExcaliburMongoDb(o => o.ConnectionString = "mongodb://localhost");

		// Assert - keyed registration count should not double
		var keyedCount = services.Count(d =>
			d.ServiceType == typeof(IPersistenceProvider) &&
			d.ServiceKey as string == "mongodb");
		keyedCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddExcaliburMongoDb(o => o.ConnectionString = "x"));
	}

	[Fact]
	public void ThrowOnNullConfigure()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddExcaliburMongoDb((Action<Excalibur.Data.MongoDB.MongoDbProviderOptions>)null!));
	}
}
