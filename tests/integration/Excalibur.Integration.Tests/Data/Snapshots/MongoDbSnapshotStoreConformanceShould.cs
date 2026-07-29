// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Snapshots;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MongoDbSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against a live MongoDB container.
/// </summary>
/// <remarks>
/// These tests verify that the MongoDB implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract using TestContainers. They are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The store is constructed via its options-only constructor,
/// which builds the provider's default <c>MongoClient</c> (and therefore the default serializer)
/// from the connection string — the surface a normal consumer uses.
/// </remarks>
[Collection(MongoDbSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<MongoDbSnapshotStoreContainerFixture>
{
	private readonly MongoDbSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbSnapshotStoreConformanceShould(MongoDbSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MongoDbSnapshotStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			CollectionName = _fixture.CollectionName,
		});

		// Options-only constructor: the store builds the provider's DEFAULT MongoClient (default
		// serializer) from the connection string — the surface most consumers use.
		return Task.FromResult<ISnapshotStore>(
			new MongoDbSnapshotStore(
				options,
				NullLogger<MongoDbSnapshotStore>.Instance,
				// Ambient context, not the default null: the tenant-isolation arms establish tenants with
				// TenantContextHolder.BeginScope, and FromContext(null) collapses all of them onto the
				// untenanted sentinel so they overwrite one another's snapshots.
				new AmbientTenantContext()));
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Reads the tenant established by <see cref="TenantContextHolder.BeginScope"/>. The production
	/// equivalent is internal to Excalibur.Dispatch, so a directly-constructed store needs this here.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
