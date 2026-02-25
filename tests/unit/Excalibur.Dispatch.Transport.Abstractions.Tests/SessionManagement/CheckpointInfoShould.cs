using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CheckpointInfoShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new CheckpointInfo();

        // Assert
        sut.CheckpointId.ShouldBe(string.Empty);
        sut.SessionId.ShouldBe(string.Empty);
        sut.CreatedAt.ShouldBe(default);
        sut.SizeInBytes.ShouldBe(0);
        sut.Metadata.ShouldNotBeNull();
        sut.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var sut = new CheckpointInfo
        {
            CheckpointId = "chk-001",
            SessionId = "sess-xyz",
            CreatedAt = now,
            SizeInBytes = 4096,
            Metadata = { ["source"] = "kafka", ["partition"] = "0" }
        };

        // Assert
        sut.CheckpointId.ShouldBe("chk-001");
        sut.SessionId.ShouldBe("sess-xyz");
        sut.CreatedAt.ShouldBe(now);
        sut.SizeInBytes.ShouldBe(4096);
        sut.Metadata.Count.ShouldBe(2);
        sut.Metadata["source"].ShouldBe("kafka");
        sut.Metadata["partition"].ShouldBe("0");
    }

    [Fact]
    public void Allow_large_size_values()
    {
        // Arrange & Act
        var sut = new CheckpointInfo { SizeInBytes = long.MaxValue };

        // Assert
        sut.SizeInBytes.ShouldBe(long.MaxValue);
    }
}
