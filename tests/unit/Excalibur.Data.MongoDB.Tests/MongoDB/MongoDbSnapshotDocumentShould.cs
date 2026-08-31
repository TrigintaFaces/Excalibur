// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

using Excalibur.Data.MongoDB;
using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.MongoDB.Snapshots;

/// <summary>
/// Unit tests for the <see cref="Data.MongoDB.Snapshots.MongoDbSnapshotDocument"/> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class MongoDbSnapshotDocumentShould : UnitTestBase
{
	[Fact]
	public void CreateId_WithValidInputs_ReturnsCompositeId()
	{
		// Arrange
		var aggregateId = "order-123";
		var aggregateType = "Order";

		// Act
		var id = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, "tenant-a");

		// Assert
		id.ShouldBe("t:tenant-a:order-123:Order");
	}

	[Fact]
	public void CreateId_WithGuidAggregateId_ReturnsCompositeId()
	{
		// Arrange
		var aggregateId = "550e8400-e29b-41d4-a716-446655440000";
		var aggregateType = "Customer";

		// Act
		var id = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId(aggregateId, aggregateType, "tenant-a");

		// Assert
		id.ShouldBe("t:tenant-a:550e8400-e29b-41d4-a716-446655440000:Customer");
	}

	[Fact]
	public void CreateId_CarriesTheTenantSegment_ForEveryPartition()
	{
		// LIVENESS: a real tenant and the untenanted partition both yield a well-formed id. Without this
		// arm a builder returning the empty string for everything satisfies the safety arm below.
		Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId("order-123", "Order", "tenant-a")
			.ShouldBe("t:tenant-a:order-123:Order");
		Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId("order-123", "Order", TenantScope.UntenantedSentinel)
			.ShouldBe($"t:{TenantScope.UntenantedSentinel}:order-123:Order");
	}

	[Fact]
	public void CreateId_SeparatesTenants_AndSeparatesUntenantedFromTenanted()
	{
		// SAFETY: no two partitions share an id for the same aggregate. The untenanted partition is a
		// value like any other, so it is separated too - it is not an absence.
		var tenantA = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId("order-123", "Order", "tenant-a");
		var tenantB = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId("order-123", "Order", "tenant-b");
		var untenanted = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.CreateId("order-123", "Order", TenantScope.UntenantedSentinel);

		tenantA.ShouldNotBe(tenantB);
		tenantA.ShouldNotBe(untenanted);
		tenantB.ShouldNotBe(untenanted);
	}

	[Fact]
	public void FromSnapshot_WithValidSnapshot_CreatesDocument()
	{
		// Arrange
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-123",
			AggregateId = "order-456",
			AggregateType = "Order",
			Version = 10,
			Data = new byte[] { 1, 2, 3, 4 },
			CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
		};

		// Act
		var document = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.FromSnapshot(snapshot, "tenant-a");

		// Assert
		document.Id.ShouldBe("t:tenant-a:order-456:Order");
		document.SnapshotId.ShouldBe("snap-123");
		document.AggregateId.ShouldBe("order-456");
		document.AggregateType.ShouldBe("Order");
		document.Version.ShouldBe(10);
		document.Data.ShouldBe(new byte[] { 1, 2, 3, 4 });
		document.CreatedAt.ShouldBe(new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void FromSnapshot_WithMetadata_SerializesMetadata()
	{
		// Arrange
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "order-456",
			AggregateType = "Order",
			Version = 10,
			Data = new byte[] { 1, 2, 3 },
			CreatedAt = DateTime.UtcNow,
			Metadata = new Dictionary<string, object>
			{
				["key1"] = "value1",
				["key2"] = 42
			}
		};

		// Act
		var document = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.FromSnapshot(snapshot, "tenant-a");

		// Assert
		_ = document.Metadata.ShouldNotBeNull();
		document.Metadata.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void FromSnapshot_WithNullMetadata_HasNullMetadata()
	{
		// Arrange
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "order-456",
			AggregateType = "Order",
			Version = 10,
			Data = new byte[] { 1, 2, 3 },
			CreatedAt = DateTime.UtcNow,
			Metadata = null
		};

		// Act
		var document = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.FromSnapshot(snapshot, "tenant-a");

		// Assert
		document.Metadata.ShouldBeNull();
	}

	[Fact]
	public void ToSnapshot_WithValidDocument_CreatesSnapshot()
	{
		// Arrange
		var document = new Data.MongoDB.Snapshots.MongoDbSnapshotDocument
		{
			Id = "order-789:Order",
			SnapshotId = "snap-456",
			AggregateId = "order-789",
			AggregateType = "Order",
			Version = 25,
			Data = new byte[] { 5, 6, 7, 8 },
			CreatedAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc)
		};

		// Act
		var snapshot = document.ToSnapshot();

		// Assert
		snapshot.SnapshotId.ShouldBe("snap-456");
		snapshot.AggregateId.ShouldBe("order-789");
		snapshot.AggregateType.ShouldBe("Order");
		snapshot.Version.ShouldBe(25);
		snapshot.Data.ToArray().ShouldBe(new byte[] { 5, 6, 7, 8 });
		snapshot.CreatedAt.ShouldBe(new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc));
	}

	[Fact]
	public void RoundTrip_PreservesAllData()
	{
		// Arrange
		var original = new Snapshot
		{
			SnapshotId = "snap-round",
			AggregateId = "agg-round",
			AggregateType = "RoundTrip",
			Version = 100,
			Data = new byte[] { 10, 20, 30, 40, 50 },
			CreatedAt = new DateTime(2025, 3, 20, 15, 45, 30, DateTimeKind.Utc)
		};

		// Act
		var document = Excalibur.Data.MongoDB.Snapshots.MongoDbSnapshotDocument.FromSnapshot(original, "tenant-a");
		var result = document.ToSnapshot();

		// Assert
		result.SnapshotId.ShouldBe(original.SnapshotId);
		result.AggregateId.ShouldBe(original.AggregateId);
		result.AggregateType.ShouldBe(original.AggregateType);
		result.Version.ShouldBe(original.Version);
		result.Data.ToArray().ShouldBe(original.Data.ToArray());
		result.CreatedAt.ShouldBe(original.CreatedAt);
	}
}
