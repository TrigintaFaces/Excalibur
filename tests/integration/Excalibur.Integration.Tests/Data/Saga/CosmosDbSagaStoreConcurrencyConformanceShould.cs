// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

using Excalibur.Saga.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Optimistic-concurrency conformance for the Cosmos DB saga store (e1tsq2, S853) — one of the five
/// distributed providers. Author≠impl (TestsDeveloper); runs the shared
/// <see cref="SagaStoreConformanceTestBase"/> contract with <see cref="SupportsOptimisticConcurrency"/>
/// enabled against the CosmosDB Linux emulator.
/// </summary>
/// <remarks>
/// RED on the pre-fix upsert with no <c>IfMatchEtag</c>; GREEN on the e1tsq2 <c>IfMatchEtag</c> optimistic
/// concurrency (<c>412 PreconditionFailed</c> → <see cref="ConcurrencyException"/>) + no-resurrect guard. A
/// fresh container (partition <c>/sagaType</c>) per test gives isolation on the shared emulator. Heavy
/// emulator — verifies at CRUCIBLE / full-CI (the fixture degrades gracefully when it can't start).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbSagaStoreConcurrencyConformanceShould : SagaStoreConformanceTestBase, IClassFixture<CosmosDbSagaStoreContainerFixture>
{
	private readonly CosmosDbSagaStoreContainerFixture _fixture;
	private readonly string _containerName = $"sagas_{Guid.NewGuid():N}";

	public CosmosDbSagaStoreConcurrencyConformanceShould(CosmosDbSagaStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override bool SupportsOptimisticConcurrency => true;

	/// <inheritdoc/>
	protected override Task<ISagaStore> CreateStoreAsync()
	{
		var options = Options.Create(new CosmosDbSagaOptions
		{
			Client = new CosmosDbClientOptions
			{
				// Satisfies CosmosDbClientOptions.Validate(); the injected client is what's actually used.
				ConnectionString = _fixture.ConnectionString,
				HttpClientFactory = () => _fixture.EmulatorHttpClient,
			},
			DatabaseName = _fixture.DatabaseName,
			ContainerName = _containerName,
			PartitionKeyPath = "/sagaType",
			CreateContainerIfNotExists = true,
			ContainerThroughput = 400,
		});

		return Task.FromResult<ISagaStore>(
			new CosmosDbSagaStore(_fixture.Client, options, NullLogger<CosmosDbSagaStore>.Instance, new DispatchJsonSerializer()));
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask; // throwaway per-test container; emulator disposed at end
}
