// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Data.CosmosDb;
using Excalibur.Data.CosmosDb.Authorization;
using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Integration.Tests.Data.Authorization;

/// <summary>
/// Holds <see cref="CosmosDbGrantStore"/> to the shared <see cref="IGrantQueryStore"/> contract on the live
/// Cosmos DB emulator.
/// </summary>
/// <remarks>
/// The store's initialization only reads its container, so this suite provisions a per-instance container
/// partitioned on <c>/tenant_id</c> (the property the store's documents are partitioned by) and drops it on
/// the way out. The store is built through its options-only constructor, so it builds its own client with
/// its own serializer; that client is routed through the fixture's emulator transport, which re-aims each
/// request at the mapped host port.
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "CosmosDb")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Pattern", "STORE")]
public sealed class CosmosDbGrantQueryStoreConformanceShould : GrantQueryStoreConformanceTestBase
{
	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private readonly string _containerName = $"grants_conf_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbGrantQueryStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB emulator fixture, shared by the collection.</param>
	public CosmosDbGrantQueryStoreConformanceShould(CosmosDbEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync().ConfigureAwait(false);
		await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task<IGrantStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB emulator must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		_ = await _fixture.Client.GetDatabase(_fixture.DatabaseName)
			.CreateContainerIfNotExistsAsync(new ContainerProperties(_containerName, "/tenant_id"))
			.ConfigureAwait(false);

		var store = new CosmosDbGrantStore(
			Options.Create(new CosmosDbAuthorizationOptions
			{
				DatabaseName = _fixture.DatabaseName,
				GrantsContainerName = _containerName,
				Client = new CosmosDbClientOptions
				{
					ConnectionString = _fixture.ConnectionString,
					UseDirectMode = false,
					HttpClientFactory = () => new HttpClient(_fixture.EmulatorHttpMessageHandler, disposeHandler: false),
				},
			}),
			NullLogger<CosmosDbGrantStore>.Instance);

		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
		return store;
	}
}
