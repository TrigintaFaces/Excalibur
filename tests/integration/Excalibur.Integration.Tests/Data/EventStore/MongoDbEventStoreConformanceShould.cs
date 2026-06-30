// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MongoDbEventStore"/> using the EventStore
/// Conformance Test Kit against a live MongoDB container.
/// </summary>
/// <remarks>
/// These tests verify that the MongoDB implementation correctly implements the
/// <see cref="IEventStore"/> contract — including optimistic-concurrency version conflicts — using
/// TestContainers. They are never skipped: when Docker is unavailable the fixture fails fast, so a
/// missing container surfaces as a failure rather than a silent pass. The store is constructed via its
/// options-only constructor, which builds the provider's DEFAULT <c>MongoClient</c> (and therefore the
/// default serializer) from the connection string — the surface a normal consumer uses.
/// </remarks>
[Collection(MongoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbEventStoreConformanceShould : EventStoreConformanceTestBase, IClassFixture<MongoDbEventStoreContainerFixture>
{
	private readonly MongoDbEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbEventStoreConformanceShould(MongoDbEventStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IEventStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MongoDbEventStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});

		// Options-only constructor: the store builds the provider's DEFAULT MongoClient (default
		// serializer) from the connection string — the surface most consumers use. The store
		// self-initializes its collections and the UNIQUE concurrency index on first use.
		return Task.FromResult<IEventStore>(
			new MongoDbEventStore(options, NullLogger<MongoDbEventStore>.Instance));
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}
}
