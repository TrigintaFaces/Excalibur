// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Tests.Shared.Conformance.Saga;
using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the MongoDB saga store (e1tsq2, S853) — one of the five
/// distributed providers. Author≠impl (TestsDeveloper); runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled, so the version-gated <c>no-overwrite</c> and <c>no-resurrect</c> facts are enforced against a
/// real MongoDB container.
/// </summary>
/// <remarks>
/// RED on the pre-fix blind upsert keyed on <c>SagaId</c> only (no <c>Version</c>); GREEN on the
/// e1tsq2 <c>{SagaId, Version}</c>-gated upsert that maps the duplicate-key / no-match to
/// <see cref="ConcurrencyException"/>. A fresh database per test gives isolation on the shared container.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<MongoDbContainerFixture>
{
	private readonly MongoDbContainerFixture _fixture;
	private readonly string _databaseName = $"saga_conf_{Guid.NewGuid():N}";

	public MongoDbSagaStoreConcurrencyConformanceShould(MongoDbContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override Task<ISagaStore> CreateStoreAsync()
	{
		var options = Options.Create(new MongoDbSagaOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _databaseName,
			CollectionName = "sagas",
		});

		// The store self-initializes its client, collection, and indexes lazily from the connection string.
		return Task.FromResult<ISagaStore>(
			new MongoDbSagaStore(options, NullLogger<MongoDbSagaStore>.Instance, new DispatchJsonSerializer()));
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Drop the per-test database to keep the shared container clean.
		var client = new MongoClient(_fixture.ConnectionString);
		await client.DropDatabaseAsync(_databaseName).ConfigureAwait(false);
	}
}
