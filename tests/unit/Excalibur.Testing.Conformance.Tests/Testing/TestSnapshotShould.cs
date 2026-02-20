// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Testing.Conformance;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="TestSnapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestSnapshotShould
{
	[Fact]
	public void Have_Default_SnapshotId_As_NewGuid()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.SnapshotId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(snapshot.SnapshotId, out _).ShouldBeTrue("SnapshotId should be a valid GUID");
	}

	[Fact]
	public void Have_Default_AggregateId_As_Empty()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void Have_Default_Version_As_Zero()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.Version.ShouldBe(0);
	}

	[Fact]
	public void Have_Default_CreatedAt_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var snapshot = new TestSnapshot();

		// Assert
		var after = DateTime.UtcNow;
		snapshot.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Have_Default_Data_As_Empty_Array()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.Data.ShouldBeEmpty();
	}

	[Fact]
	public void Have_Default_AggregateType_As_TestAggregate()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.AggregateType.ShouldBe("TestAggregate");
	}

	[Fact]
	public void Have_Default_Metadata_As_Null()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Allow_Setting_All_Properties_Via_Init()
	{
		// Arrange
		var snapshotId = "custom-snapshot-id";
		var aggregateId = "custom-aggregate-id";
		var version = 99L;
		var createdAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
		var data = Encoding.UTF8.GetBytes("custom-state-data");
		var aggregateType = "CustomAggregate";
		var metadata = new Dictionary<string, object> { ["snapshotKey"] = "snapshotValue" };

		// Act
		var snapshot = new TestSnapshot
		{
			SnapshotId = snapshotId,
			AggregateId = aggregateId,
			Version = version,
			CreatedAt = createdAt,
			Data = data,
			AggregateType = aggregateType,
			Metadata = metadata
		};

		// Assert
		snapshot.SnapshotId.ShouldBe(snapshotId);
		snapshot.AggregateId.ShouldBe(aggregateId);
		snapshot.Version.ShouldBe(version);
		snapshot.CreatedAt.ShouldBe(createdAt);
		snapshot.Data.ShouldBe(data);
		snapshot.AggregateType.ShouldBe(aggregateType);
		snapshot.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void Create_Snapshot_With_Required_Parameters()
	{
		// Arrange
		var aggregateId = "order-123";
		var aggregateType = "OrderAggregate";
		var version = 10L;

		// Act
		var snapshot = TestSnapshot.Create(aggregateId, aggregateType, version);

		// Assert
		snapshot.ShouldNotBeNull();
		snapshot.AggregateId.ShouldBe(aggregateId);
		snapshot.AggregateType.ShouldBe(aggregateType);
		snapshot.Version.ShouldBe(version);
	}

	[Fact]
	public void Create_Snapshot_With_Unique_SnapshotId()
	{
		// Arrange
		var aggregateId = "order-123";
		var aggregateType = "OrderAggregate";
		var version = 10L;

		// Act
		var snapshot1 = TestSnapshot.Create(aggregateId, aggregateType, version);
		var snapshot2 = TestSnapshot.Create(aggregateId, aggregateType, version);

		// Assert
		snapshot1.SnapshotId.ShouldNotBe(snapshot2.SnapshotId,
			"Each Create call should generate a unique SnapshotId");
	}

	[Fact]
	public void Create_Snapshot_With_CreatedAt_Near_UtcNow()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var snapshot = TestSnapshot.Create("agg-1", "TestAggregate", 1);

		// Assert
		var after = DateTime.UtcNow;
		snapshot.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.CreatedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Create_Snapshot_With_Default_State_When_Not_Provided()
	{
		// Arrange
		var version = 5L;

		// Act
		var snapshot = TestSnapshot.Create("agg-1", "TestAggregate", version);

		// Assert
		var stateString = Encoding.UTF8.GetString(snapshot.Data);
		stateString.ShouldBe($"state-v{version}");
	}

	[Fact]
	public void Create_Snapshot_With_Custom_State_When_Provided()
	{
		// Arrange
		var customState = "{\"counter\": 42, \"name\": \"Test\"}";

		// Act
		var snapshot = TestSnapshot.Create("agg-1", "TestAggregate", 1, customState);

		// Assert
		var stateString = Encoding.UTF8.GetString(snapshot.Data);
		stateString.ShouldBe(customState);
	}

	[Fact]
	public void Create_Snapshot_With_Null_State_Uses_Default()
	{
		// Arrange
		var version = 3L;

		// Act
		var snapshot = TestSnapshot.Create("agg-1", "TestAggregate", version, null);

		// Assert
		var stateString = Encoding.UTF8.GetString(snapshot.Data);
		stateString.ShouldBe($"state-v{version}");
	}

	[Fact]
	public void Implement_ISnapshot_Interface()
	{
		// Arrange & Act
		var snapshot = new TestSnapshot();

		// Assert
		snapshot.ShouldBeAssignableTo<Excalibur.Domain.Model.ISnapshot>();
	}

	[Fact]
	public void Preserve_Data_Through_Round_Trip()
	{
		// Arrange
		var originalState = "Complex state with unicode: \u4e2d\u6587 and special chars: @#$%";
		var data = Encoding.UTF8.GetBytes(originalState);

		// Act
		var snapshot = new TestSnapshot { Data = data };
		var recoveredState = Encoding.UTF8.GetString(snapshot.Data);

		// Assert
		recoveredState.ShouldBe(originalState);
	}
}
