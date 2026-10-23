// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Data.MongoDB.Projections;
using Excalibur.EventSourcing;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Tests.MongoDB;

/// <summary>
/// The projection store is SCOPED and the inline projection path creates a DI scope per projection per
/// save. MongoClient owns a connection pool and is documented as thread-safe and intended to be shared,
/// so a store that builds its own leaks a pool on every save until the process exhausts connections --
/// surfacing as a timeout against a healthy cluster. These arms bind the client to the PROCESS.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "MongoDB")]
public sealed class MongoDbProjectionClientLifetimeShould
{
	private const string ConnectionString = "mongodb://localhost:27017";

	private sealed class LifetimeProjection
	{
		public string Id { get; set; } = string.Empty;
	}

	// Reads the field the store will actually issue requests through, not a registration descriptor: a
	// singleton can be registered and the store still build its own, which is exactly the bug.
	private static IMongoClient? ClientOf(IProjectionStore<LifetimeProjection> store) =>
		(IMongoClient?)typeof(MongoDbProjectionStore<LifetimeProjection>)
			.GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!
			.GetValue(store);

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMongoDbProjectionStore<LifetimeProjection>(o => o.ConnectionString = ConnectionString);
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task GiveEveryScopedStoreTheSameClient()
	{
		await using var provider = BuildProvider();
		await using var first = provider.CreateAsyncScope();
		await using var second = provider.CreateAsyncScope();

		var firstStore = first.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();
		var secondStore = second.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();

		// Scoped: the STORES must differ, or this arm would pass for the wrong reason.
		ReferenceEquals(firstStore, secondStore).ShouldBeFalse();

		var firstClient = ClientOf(firstStore);
		var secondClient = ClientOf(secondStore);

		firstClient.ShouldNotBeNull();
		secondClient.ShouldNotBeNull();
		ReferenceEquals(firstClient, secondClient).ShouldBeTrue();
	}

	[Fact]
	public async Task SupplyTheClientBeforeTheStoreIssuesAnyRequest()
	{
		// A client created lazily on first use is created PER STORE, so "it works" is indistinguishable
		// from the leak. The store must already hold its client the moment it is constructed.
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();

		ClientOf(store).ShouldNotBeNull();
	}
}
