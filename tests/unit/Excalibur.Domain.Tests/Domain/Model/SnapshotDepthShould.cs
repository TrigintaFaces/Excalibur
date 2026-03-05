// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

/// <summary>
/// Depth coverage tests for <see cref="Snapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SnapshotDepthShould
{
	[Fact]
	public void Create_SetsAllProperties()
	{
		// Arrange
		var data = new byte[] { 1, 2, 3 };
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var snapshot = Snapshot.Create("agg-1", 5, data, "OrderAggregate");
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		snapshot.SnapshotId.ShouldNotBeNullOrEmpty();
		snapshot.AggregateId.ShouldBe("agg-1");
		snapshot.Version.ShouldBe(5);
		snapshot.Data.ShouldBe(data);
		snapshot.AggregateType.ShouldBe("OrderAggregate");
		snapshot.CreatedAt.ShouldBeGreaterThanOrEqualTo(lowerBound);
		snapshot.CreatedAt.ShouldBeLessThanOrEqualTo(upperBound);
		snapshot.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Create_WithMetadata_SetsMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		var snapshot = Snapshot.Create("agg-2", 10, [4, 5], "TestAggregate", metadata);

		// Assert
		snapshot.Metadata.ShouldNotBeNull();
		snapshot.Metadata!["key"].ShouldBe("value");
	}

	[Fact]
	public void Create_GeneratesUniqueIds()
	{
		// Arrange & Act
		var snap1 = Snapshot.Create("agg-1", 1, [1], "Type1");
		var snap2 = Snapshot.Create("agg-1", 1, [1], "Type1");

		// Assert
		snap1.SnapshotId.ShouldNotBe(snap2.SnapshotId);
	}

	[Fact]
	public void RecordEquality_WorksCorrectly()
	{
		// Arrange
		var snap = Snapshot.Create("agg-1", 1, [1, 2], "Type1");

		// Act
		var copy = snap with { AggregateId = "agg-2" };

		// Assert
		copy.AggregateId.ShouldBe("agg-2");
		copy.Version.ShouldBe(1);
		copy.SnapshotId.ShouldBe(snap.SnapshotId);
	}

	[Fact]
	public void RequiredProperties_MustBeSet()
	{
		// Arrange & Act
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-1",
			AggregateId = "agg-1",
			Version = 3,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = [0xFF],
			AggregateType = "MyAggregate",
		};

		// Assert
		snapshot.SnapshotId.ShouldBe("snap-1");
		snapshot.AggregateId.ShouldBe("agg-1");
		snapshot.Version.ShouldBe(3);
		snapshot.Data.ShouldBe(new byte[] { 0xFF });
		snapshot.AggregateType.ShouldBe("MyAggregate");
	}
}
