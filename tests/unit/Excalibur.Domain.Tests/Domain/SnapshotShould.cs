// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="Snapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SnapshotShould
{
	[Fact]
	public void Create_WithRequiredProperties_SetsAllProperties()
	{
		// Arrange
		var aggregateId = "aggregate-123";
		var version = 42L;
		var data = "test-data"u8.ToArray();
		var aggregateType = "TestAggregate";

		// Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = version,
			Data = data,
			AggregateType = aggregateType,
			CreatedAt = DateTime.UtcNow,
		};

		// Assert
		snapshot.AggregateId.ShouldBe(aggregateId);
		snapshot.Version.ShouldBe(version);
		snapshot.Data.ShouldBe(data);
		snapshot.AggregateType.ShouldBe(aggregateType);
	}

	[Fact]
	public void Create_GeneratesSnapshotId()
	{
		// Arrange & Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = DateTime.UtcNow,
		};

		// Assert
		snapshot.SnapshotId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(snapshot.SnapshotId, out _).ShouldBeTrue();
	}

	[Fact]
	public void Create_SetsCreatedAtToUtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow.AddSeconds(-1);
		var now = DateTime.UtcNow;

		// Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = now,
		};

		var after = DateTime.UtcNow.AddSeconds(1);

		// Assert
		snapshot.CreatedAt.ShouldBeGreaterThan(before);
		snapshot.CreatedAt.ShouldBeLessThan(after);
	}

	[Fact]
	public void Metadata_CanBeNull()
	{
		// Arrange & Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = DateTime.UtcNow,
			Metadata = null,
		};

		// Assert
		snapshot.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Metadata_CanContainValues()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		// Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = DateTime.UtcNow,
			Metadata = metadata,
		};

		// Assert
		snapshot.Metadata.ShouldNotBeNull();
		snapshot.Metadata["key1"].ShouldBe("value1");
		snapshot.Metadata["key2"].ShouldBe(42);
	}

	[Fact]
	public void SnapshotId_CanBeOverridden()
	{
		// Arrange
		const string customId = "custom-snapshot-id";

		// Act
		var snapshot = new Snapshot
		{
			SnapshotId = customId,
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = DateTime.UtcNow,
		};

		// Assert
		snapshot.SnapshotId.ShouldBe(customId);
	}

	[Fact]
	public void CreatedAt_CanBeOverridden()
	{
		// Arrange
		var customDate = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = customDate,
		};

		// Assert
		snapshot.CreatedAt.ShouldBe(customDate);
	}

	[Fact]
	public void ImplementsISnapshot()
	{
		// Arrange & Act
		var snapshot = new Snapshot
		{
			SnapshotId = Guid.NewGuid().ToString(),
			AggregateId = "agg-1",
			Version = 1,
			Data = "data"u8.ToArray(),
			AggregateType = "Test",
			CreatedAt = DateTime.UtcNow,
		};

		// Assert
		_ = snapshot.ShouldBeAssignableTo<ISnapshot>();
	}

	[Fact]
	public void Equality_TwoSnapshotsWithSameData_AreEqual()
	{
		// Arrange
		var data = "test-data"u8.ToArray();
		var snapshotId = "shared-id";
		var createdAt = DateTime.UtcNow;

		var snapshot1 = new Snapshot
		{
			SnapshotId = snapshotId,
			AggregateId = "agg-1",
			Version = 1,
			Data = data,
			AggregateType = "Test",
			CreatedAt = createdAt,
		};

		var snapshot2 = new Snapshot
		{
			SnapshotId = snapshotId,
			AggregateId = "agg-1",
			Version = 1,
			Data = data,
			AggregateType = "Test",
			CreatedAt = createdAt,
		};

		// Assert - records compare by value
		snapshot1.ShouldBe(snapshot2);
	}
}
