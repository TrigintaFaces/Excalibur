// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Dispatch;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Conformance.Cdc;
using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDB;

/// <summary>
/// Per-provider real-store durability conformance for the MongoDB <see cref="ICdcStateStore"/>
/// implementation, driven by the shared <see cref="CdcProviderConformanceTestBase"/> kit against a real
/// MongoDB container. Verifies the checkpoint round-trip actually survives the database (real-infra bar).
/// </summary>
/// <remarks>
/// Each test instance uses a unique collection so the run is isolated and
/// <c>GetAllPositions_EmptyStore_ReturnsEmpty</c> holds; Docker is a hard requirement (never skipped).
/// </remarks>
[Collection(ContainerCollections.MongoDB)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbCdcStateStoreConformanceShould : CdcProviderConformanceTestBase
{
	private readonly MongoDbContainerFixture _fixture;
	private readonly string _collectionName = $"cdc_state_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcStateStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared MongoDB container fixture.</param>
	public MongoDbCdcStateStoreConformanceShould(MongoDbContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/MongoDB must be available - the CDC state-store durability conformance is a real-infra lock and must never be skipped.");

		var client = new MongoClient(_fixture.ConnectionString);
		ICdcStateStore store = new MongoDbCdcStateStore(
			client,
			MsOptions.Create(new MongoDbCdcStateStoreOptions
			{
				DatabaseName = "excalibur_cdc_conformance",
				CollectionName = _collectionName,
			}));
		return Task.FromResult(store);
	}

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index) =>
		// A distinct, non-null Mongo change-stream resume token per index (index 0 is still valid: a
		// non-null ResumeToken is valid). Round-trip through MongoDbCdcPosition so the token format matches
		// what the store persists.
		new MongoDbCdcPosition(new BsonDocument("_data", $"resume-{index:D6}")).ToChangePosition();

	/// <inheritdoc/>
	protected override Task CleanupAsync() =>
		// Each instance uses a unique collection, so state is naturally isolated; the container teardown
		// reclaims the collections. No per-test cleanup required.
		Task.CompletedTask;
}
