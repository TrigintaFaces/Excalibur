// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-Firestore locks on the persisted shape of a snapshot document being the same whichever write
/// path stored it.
/// </summary>
/// <remarks>
/// <para>
/// The store writes a snapshot two ways: it creates the document when the aggregate has none, and updates
/// it in place when it does. An update in Firestore MERGES — fields the write does not name keep whatever
/// value they already had — so the two paths agree only while every field is named on both. Consumers hold
/// data written by either, so a divergence is not a stale test, it is a document that reads back wrong.
/// </para>
/// <para>
/// The field names below are written out rather than derived from the store, deliberately. A test that
/// asked the store what shape it writes would agree with any shape the store happened to write, including
/// a changed one; this one fails when the persisted contract moves, which is the point.
/// </para>
/// </remarks>
[Collection(FirestoreSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "STORE")]
public sealed class FirestoreSnapshotStoreDocumentShapeShould
{
	private const string AggregateType = "ShapedAggregate";
	private const string TenantId = "tenant-shape";

	/// <summary>The fields a stored snapshot carries when it has metadata.</summary>
	private static readonly string[] FieldsWithMetadata =
		["snapshotId", "aggregateId", "aggregateType", "version", "createdAt", "data", "metadata"];

	/// <summary>The fields a stored snapshot carries when it has none.</summary>
	private static readonly string[] FieldsWithoutMetadata =
		["snapshotId", "aggregateId", "aggregateType", "version", "createdAt", "data"];

	private readonly FirestoreSnapshotStoreContainerFixture _fixture;
	private readonly string _collectionPrefix = "snapshots_shape_" + Guid.NewGuid().ToString("N");

	public FirestoreSnapshotStoreDocumentShapeShould(FirestoreSnapshotStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// The create path and the update path must leave the same fields behind. An update that named only
	/// some of them would still round-trip the fields it named, so this compares the whole stored map.
	/// </summary>
	[Fact]
	public async Task StoreTheSameFieldsWhetherTheDocumentWasCreatedOrUpdated()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		var collectionName = $"{_collectionPrefix}_paths";
		var store = CreateStore(collectionName);
		const string AggregateId = "agg-both-paths";

		// Create path: no document for this aggregate yet.
		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 1, payload: 0x01, WithMetadata()), CancellationToken.None)
			.ConfigureAwait(false);

		var afterCreate = await ReadRawAsync(collectionName, AggregateId).ConfigureAwait(false);

		// Update path: the document now exists, so the higher version replaces it in place.
		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 2, payload: 0x02, WithMetadata()), CancellationToken.None)
			.ConfigureAwait(false);

		var afterUpdate = await ReadRawAsync(collectionName, AggregateId).ConfigureAwait(false);

		afterCreate.Keys.Order().ShouldBe(
			FieldsWithMetadata.Order(),
			"the created document must carry exactly the documented field set.");

		afterUpdate.Keys.Order().ShouldBe(
			FieldsWithMetadata.Order(),
			"the updated document must carry exactly the same field set as the created one. A merge that "
			+ "named only some fields would leave the rest holding the PREVIOUS snapshot's values while "
			+ "reading back as though they belonged to this one.");

		// Every field must have moved on to the second snapshot's value, not merely be present.
		afterUpdate["version"].ShouldBe(2L);
		((Blob)afterUpdate["data"]).ByteString.ToByteArray().ShouldBe([(byte)0x02]);
		afterUpdate["snapshotId"].ShouldNotBe(afterCreate["snapshotId"], "each save carries its own snapshot id.");
		afterUpdate["aggregateId"].ShouldBe(AggregateId);
		afterUpdate["aggregateType"].ShouldBe(AggregateType);
	}

	/// <summary>
	/// A snapshot with no metadata must leave no metadata behind, even when the snapshot it replaced had
	/// some. This is the one field a merge can silently carry forward.
	/// </summary>
	[Fact]
	public async Task RemoveMetadataWhenTheReplacingSnapshotHasNone()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the Firestore emulator must be available — this is a real-infrastructure lock and is never skipped.");

		var collectionName = $"{_collectionPrefix}_metadata";
		var store = CreateStore(collectionName);
		const string AggregateId = "agg-metadata";

		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 1, payload: 0x01, WithMetadata()), CancellationToken.None)
			.ConfigureAwait(false);

		var seeded = await ReadRawAsync(collectionName, AggregateId).ConfigureAwait(false);
		seeded.Keys.ShouldContain(
			"metadata",
			"the first save must actually store metadata, or the assertion below passes for the wrong reason.");

		await store
			.SaveSnapshotAsync(CreateSnapshot(AggregateId, version: 2, payload: 0x02, metadata: null), CancellationToken.None)
			.ConfigureAwait(false);

		var afterReplace = await ReadRawAsync(collectionName, AggregateId).ConfigureAwait(false);

		afterReplace.Keys.Order().ShouldBe(
			FieldsWithoutMetadata.Order(),
			"the replacing snapshot has no metadata, so the stored document must have none. Leaving the "
			+ "previous snapshot's metadata attached would hand a reader another snapshot's metadata as "
			+ "this one's own.");

		var loaded = await store
			.GetLatestSnapshotAsync(AggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull();
		loaded.Version.ShouldBe(2);
		(loaded.Metadata is null || loaded.Metadata.Count == 0).ShouldBeTrue(
			$"the reloaded snapshot must carry no metadata, but carried {loaded.Metadata?.Count} entries.");
	}

	private async Task<IReadOnlyDictionary<string, object>> ReadRawAsync(string collectionName, string aggregateId)
	{
		var collection = _fixture.Db.Collection(collectionName);
		var documents = new List<DocumentReference>();

		await foreach (var document in collection.ListDocumentsAsync().ConfigureAwait(false))
		{
			documents.Add(document);
		}

		documents.Count.ShouldBe(
			1,
			$"exactly one document must exist for {aggregateId}; the store keeps one snapshot per aggregate.");

		var snapshot = await documents[0].GetSnapshotAsync().ConfigureAwait(false);
		snapshot.Exists.ShouldBeTrue();

		return snapshot.ToDictionary();
	}

	private static Dictionary<string, object> WithMetadata() =>
		new() { ["origin"] = "shape-test", ["sequence"] = 7L };

	private FirestoreSnapshotStore CreateStore(string collectionName) =>
		new(
			_fixture.Db,
			Options.Create(new FirestoreSnapshotStoreOptions
			{
				ProjectId = _fixture.ProjectId,
				CollectionName = collectionName,
				EmulatorHost = _fixture.EmulatorEndpoint,
			}),
			NullLogger<FirestoreSnapshotStore>.Instance,
			new FixedShapeTenantContext(TenantId));

	private static Snapshot CreateSnapshot(
		string aggregateId,
		long version,
		byte payload,
		IDictionary<string, object>? metadata) =>
		new()
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			AggregateType = AggregateType,
			Version = version,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = new ReadOnlyMemory<byte>([payload]),
			Metadata = metadata,
		};

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedShapeTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
