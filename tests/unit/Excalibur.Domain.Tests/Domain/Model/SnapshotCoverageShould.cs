using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SnapshotCoverageShould
{
    [Fact]
    public void Create_GenerateSnapshotWithAllProperties()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var snapshot = Snapshot.Create("agg-1", 5, data, "OrderAggregate");

        // Assert
        snapshot.SnapshotId.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParse(snapshot.SnapshotId, out _).ShouldBeTrue();
        snapshot.AggregateId.ShouldBe("agg-1");
        snapshot.Version.ShouldBe(5);
        snapshot.Data.ShouldBe(data);
        snapshot.AggregateType.ShouldBe("OrderAggregate");
        snapshot.CreatedAt.ShouldBeInRange(
            DateTimeOffset.UtcNow.AddSeconds(-5),
            DateTimeOffset.UtcNow.AddSeconds(1));
        snapshot.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Create_WithMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["source"] = "test" };

        // Act
        var snapshot = Snapshot.Create("agg-2", 10, [99], "TestAggregate", metadata);

        // Assert
        snapshot.Metadata.ShouldNotBeNull();
        snapshot.Metadata["source"].ShouldBe("test");
    }

    [Fact]
    public void RequiredProperties_MustBeSet()
    {
        // Act
        var snapshot = new Snapshot
        {
            SnapshotId = "snap-1",
            AggregateId = "agg-1",
            Version = 3,
            CreatedAt = DateTimeOffset.UtcNow,
            Data = [1, 2],
            AggregateType = "TestType",
        };

        // Assert
        snapshot.SnapshotId.ShouldBe("snap-1");
        snapshot.AggregateId.ShouldBe("agg-1");
        snapshot.Version.ShouldBe(3);
        snapshot.Data.Length.ShouldBe(2);
        snapshot.AggregateType.ShouldBe("TestType");
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        // Arrange — share the Data array since record equality uses reference equality for arrays
        var ts = DateTimeOffset.UtcNow;
        var data = new byte[] { 1 };
        var snap1 = new Snapshot
        {
            SnapshotId = "s1",
            AggregateId = "a1",
            Version = 1,
            CreatedAt = ts,
            Data = data,
            AggregateType = "T1",
        };
        var snap2 = new Snapshot
        {
            SnapshotId = "s1",
            AggregateId = "a1",
            Version = 1,
            CreatedAt = ts,
            Data = data,
            AggregateType = "T1",
        };

        // Act & Assert
        snap1.ShouldBe(snap2);
    }

    [Fact]
    public void Create_GenerateUniqueSnapshotIds()
    {
        // Act
        var snap1 = Snapshot.Create("agg-1", 1, [1], "T");
        var snap2 = Snapshot.Create("agg-1", 1, [1], "T");

        // Assert
        snap1.SnapshotId.ShouldNotBe(snap2.SnapshotId);
    }

    [Fact]
    public void Metadata_CanBeNull()
    {
        // Arrange & Act
        var snapshot = new Snapshot
        {
            SnapshotId = "s",
            AggregateId = "a",
            Version = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            Data = [],
            AggregateType = "T",
            Metadata = null,
        };

        // Assert
        snapshot.Metadata.ShouldBeNull();
    }
}
