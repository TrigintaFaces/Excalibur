using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SnapshotExtendedShould
{
	[Fact]
	public void CreateSnapshot_WithAllRequiredProperties()
	{
		// Arrange & Act
		var snapshot = Snapshot.Create(
			"aggregate-123",
			5,
			[0x01, 0x02, 0x03],
			"OrderAggregate");

		// Assert
		snapshot.SnapshotId.ShouldNotBeNullOrEmpty();
		snapshot.AggregateId.ShouldBe("aggregate-123");
		snapshot.Version.ShouldBe(5);
		snapshot.Data.ShouldBe(new byte[] { 0x01, 0x02, 0x03 });
		snapshot.AggregateType.ShouldBe("OrderAggregate");
		snapshot.CreatedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		snapshot.Metadata.ShouldBeNull();
	}

	[Fact]
	public void CreateSnapshot_WithMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["version"] = "v2" };

		// Act
		var snapshot = Snapshot.Create(
			"aggregate-456",
			10,
			[0xFF],
			"UserAggregate",
			metadata);

		// Assert
		snapshot.Metadata.ShouldNotBeNull();
		snapshot.Metadata!["version"].ShouldBe("v2");
	}

	[Fact]
	public void CreateSnapshot_GeneratesUniqueIds()
	{
		// Arrange & Act
		var snapshot1 = Snapshot.Create("agg-1", 1, [0x01], "Type1");
		var snapshot2 = Snapshot.Create("agg-2", 2, [0x02], "Type2");

		// Assert
		snapshot1.SnapshotId.ShouldNotBe(snapshot2.SnapshotId);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var data = new byte[] { 0x01, 0x02 };
		var snapshot1 = new Snapshot
		{
			SnapshotId = "snap-1",
			AggregateId = "agg-1",
			Version = 5,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = data,
			AggregateType = "TestAggregate",
		};
		var snapshot2 = snapshot1 with { };

		// Assert
		snapshot1.ShouldBe(snapshot2);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var snapshot1 = new Snapshot
		{
			SnapshotId = "snap-1",
			AggregateId = "agg-1",
			Version = 5,
			CreatedAt = DateTimeOffset.UtcNow,
			Data = [0x01],
			AggregateType = "TestAggregate",
		};
		var snapshot2 = snapshot1 with { Version = 6 };

		// Assert
		snapshot1.ShouldNotBe(snapshot2);
	}

	[Fact]
	public void InitializeWithRequiredProperties()
	{
		// Arrange & Act
		var now = DateTimeOffset.UtcNow;
		var snapshot = new Snapshot
		{
			SnapshotId = "snap-id",
			AggregateId = "agg-id",
			Version = 3,
			CreatedAt = now,
			Data = [0xAA, 0xBB],
			AggregateType = "MyAggregate",
			Metadata = new Dictionary<string, object> { ["key"] = "value" },
		};

		// Assert
		snapshot.SnapshotId.ShouldBe("snap-id");
		snapshot.AggregateId.ShouldBe("agg-id");
		snapshot.Version.ShouldBe(3);
		snapshot.CreatedAt.ShouldBe(now);
		snapshot.Data.ShouldBe(new byte[] { 0xAA, 0xBB });
		snapshot.AggregateType.ShouldBe("MyAggregate");
		snapshot.Metadata!["key"].ShouldBe("value");
	}
}
