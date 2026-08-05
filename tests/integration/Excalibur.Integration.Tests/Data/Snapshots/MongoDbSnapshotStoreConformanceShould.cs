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
	/// A concurrent save must never leave the store reporting a version it has already accepted a
	/// higher one than, no matter which writer wins the race to create the document.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The inherited conformance fact asserts this property once, and once is not enough to hold it:
	/// the defect it guards reproduced at roughly one run in thirty-five, so that fact passes over a
	/// broken store about ninety-seven percent of the time. Sixty consecutive green runs were
	/// measured against the FIXED store, and the same loop had earlier gone twenty green against the
	/// BROKEN one -- a detector that weak cannot distinguish a fix from luck.
	/// </para>
	/// <para>
	/// The window is narrow because it only opens before the document exists. Every concurrent writer
	/// then evaluates the version guard against nothing, matches nothing, and the upsert becomes an
	/// insert of the same id; one wins and the rest collide. Whether that loses data depends on
	/// whether the winner happened to be the highest version, which is why it is rare and why it is
	/// arbitrary when it happens.
	/// </para>
	/// <para>
	/// So this repeats the race on a FRESH aggregate id each round, which re-opens that window every
	/// time. Each round is an independent chance to catch the defect, making one invocation of this
	/// fact a far stronger detector than one invocation of the inherited one.
	/// </para>
	/// </remarks>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Never_Report_A_Version_Lower_Than_One_It_Accepted_Under_Concurrency()
	{
		// Each round re-opens the create-the-document window, which is the only window the defect
		// lives in. Rounds are cheap -- the container is already up and shared by the class.
		const int rounds = 40;
		const int writersPerRound = 10;

		var store = await CreateSnapshotStoreAsync().ConfigureAwait(false);
		var losses = new List<string>();

		for (var round = 1; round <= rounds; round++)
		{
			var aggregateId = Guid.NewGuid().ToString();
			var versions = Enumerable.Range(1, writersPerRound).Select(i => (long)(i * 10)).ToArray();

			await Task.WhenAll(versions.Select(v =>
				store.SaveSnapshotAsync(
					CreateTestSnapshot(aggregateId, "ConcurrencyLock", v, new byte[] { (byte)(v / 10) }),
					CancellationToken.None).AsTask())).ConfigureAwait(false);

			var retrieved = await store.GetLatestSnapshotAsync(
				aggregateId, "ConcurrencyLock", CancellationToken.None).ConfigureAwait(false);

			var highest = versions.Max();
			if (retrieved is null)
			{
				losses.Add($"round {round}: no snapshot at all, though {writersPerRound} saves completed");
			}
			else if (retrieved.Version != highest)
			{
				// Report the value, not just the mismatch: an arbitrary surviving version is the
				// signature of an insert race, whereas a consistently low one would point elsewhere.
				losses.Add($"round {round}: reported {retrieved.Version}, accepted {highest}");
			}
		}

		losses.ShouldBeEmpty(
			$"the store reported a version lower than one it had already accepted in {losses.Count} of "
			+ $"{rounds} rounds. A save that is acknowledged and then silently discarded is data loss, "
			+ "not a race that resolves itself: "
			+ string.Join("; ", losses.Take(5)));
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
