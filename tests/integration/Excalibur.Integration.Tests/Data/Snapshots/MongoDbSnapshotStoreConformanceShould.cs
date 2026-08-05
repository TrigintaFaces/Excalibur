// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Snapshots;

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
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

	/// <summary>
	/// A duplicate key raised by an index OTHER than <c>_id</c> must surface, not be reported as a
	/// version conflict.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The save path retries on duplicate key and, on exhaustion, concludes "a newer snapshot is
	/// already stored, skipping this one". That conclusion is only sound for the <c>_id</c> index.
	/// Any other unique constraint produces the same error code and means something completely
	/// different, so the untargeted catch turned an unrelated write violation into a silent,
	/// confident, WRONG verdict -- the caller is told its snapshot was superseded, the real
	/// violation is discarded, and nothing is logged as a failure.
	/// </para>
	/// <para>
	/// This runs against real MongoDB because the assumption under test belongs to the SERVER, not
	/// to us: the store distinguishes the two cases by reading the index name out of the driver's
	/// error message. A mocked exception would assert only that we can parse a string we wrote
	/// ourselves, which is precisely the part that cannot be wrong. The safety arm below therefore
	/// establishes a second unique index on the real collection and provokes it.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Surface_A_Duplicate_Key_From_A_Foreign_Index_Instead_Of_Reporting_A_Version_Conflict()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var store = SnapshotStore!;
		var client = new MongoClient(_fixture.ConnectionString);
		var collection = client.GetDatabase(_fixture.DatabaseName)
			.GetCollection<BsonDocument>(_fixture.CollectionName);

		// LIVENESS, asserted first and against the UNMODIFIED collection: an ordinary superseded
		// snapshot is still skipped quietly. Without this arm the safety assertion below is equally
		// satisfied by a store that has simply started throwing on every duplicate key, which would
		// be a worse regression than the defect being fixed.
		var supersededType = $"TL{Guid.NewGuid():N}";
		var supersededAggregate = Guid.NewGuid().ToString();

		await store.SaveSnapshotAsync(
			CreateTestSnapshot(supersededAggregate, supersededType, 9, [9]),
			CancellationToken.None).ConfigureAwait(false);
		await Should.NotThrowAsync(async () => await store.SaveSnapshotAsync(
			CreateTestSnapshot(supersededAggregate, supersededType, 2, [2]),
			CancellationToken.None).ConfigureAwait(false));

		var kept = await store.GetLatestSnapshotAsync(supersededAggregate, supersededType, CancellationToken.None)
			.ConfigureAwait(false);
		kept.ShouldNotBeNull();
		kept.Version.ShouldBe(
			9,
			"the older snapshot was not merely accepted without throwing -- it overwrote a newer one, "
			+ "which is the lost update this guard exists to prevent.");

		// Every document written so far carries a DISTINCT aggregateType, which is what makes the
		// unique index below creatable at all. Writing two of a kind first makes CreateOneAsync
		// itself fail, and the safety assertion is then never reached -- the first version of this
		// test did exactly that and reported a failure that had nothing to do with the store.
		var foreignIndex = await collection.Indexes.CreateOneAsync(
			new CreateIndexModel<BsonDocument>(
				Builders<BsonDocument>.IndexKeys.Ascending("aggregateType"),
				new CreateIndexOptions { Unique = true }),
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

		try
		{
			var collidingType = $"TS{Guid.NewGuid():N}";

			await store.SaveSnapshotAsync(
				CreateTestSnapshot(Guid.NewGuid().ToString(), collidingType, 5, [1, 2, 3]),
				CancellationToken.None).ConfigureAwait(false);

			// SAFETY. A DIFFERENT aggregate, so the _id differs and the version guard has no quarrel
			// with it. It collides only on the unique index established above, and that collision
			// must not be reported to the caller as "your snapshot was superseded".
			var write = await Should.ThrowAsync<MongoWriteException>(async () => await store.SaveSnapshotAsync(
				CreateTestSnapshot(Guid.NewGuid().ToString(), collidingType, 6, [4, 5, 6]),
				CancellationToken.None).ConfigureAwait(false));

			write.WriteError.Code.ShouldBe(
				11000,
				"the arm did not provoke a duplicate key at all, so it proves nothing about how one "
				+ "is classified.");
			write.WriteError.Message.Contains("aggregateType", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
				"the duplicate key came from a different index than the one this test established, so "
				+ $"the safety property was never exercised. Message: {write.WriteError.Message}");
		}
		finally
		{
			await collection.Indexes.DropOneAsync(foreignIndex, CancellationToken.None).ConfigureAwait(false);
		}
	}
}
