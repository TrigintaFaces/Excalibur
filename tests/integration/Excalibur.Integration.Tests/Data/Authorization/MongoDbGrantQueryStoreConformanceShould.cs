// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Data.MongoDB.Authorization;
using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Inbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Conformance.Grants;

namespace Excalibur.Integration.Tests.Data.Authorization;

/// <summary>
/// Holds <see cref="MongoDbGrantStore"/> to the shared <see cref="IGrantQueryStore"/> contract on a live
/// MongoDB container.
/// </summary>
/// <remarks>
/// The store is built through its options-only constructor, so it builds the provider's default client and
/// self-initializes a per-instance collection, which is dropped on the way out.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
[Trait("Pattern", "STORE")]
public sealed class MongoDbGrantQueryStoreConformanceShould : GrantQueryStoreConformanceTestBase
{
	private readonly MongoDbInboxStoreContainerFixture _fixture;
	private readonly string _collectionName = $"grants_conf_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbGrantQueryStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture, shared by the collection.</param>
	public MongoDbGrantQueryStoreConformanceShould(MongoDbInboxStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync().ConfigureAwait(false);
		if (_fixture.DockerAvailable)
		{
			await new MongoClient(_fixture.ConnectionString).GetDatabase(_fixture.DatabaseName)
				.DropCollectionAsync(_collectionName).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override Task<IGrantStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		return Task.FromResult<IGrantStore>(new MongoDbGrantStore(
			Options.Create(new MongoDbAuthorizationOptions
			{
				ConnectionString = _fixture.ConnectionString,
				DatabaseName = _fixture.DatabaseName,
				GrantsCollectionName = _collectionName,
			}),
			NullLogger<MongoDbGrantStore>.Instance));
	}
}
