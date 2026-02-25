using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class LockInfoShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new LockInfo();

        // Assert
        sut.SessionId.ShouldBe(string.Empty);
        sut.LockToken.ShouldBe(string.Empty);
        sut.Type.ShouldBe(LockType.Read);
        sut.OwnerId.ShouldBe(string.Empty);
        sut.AcquiredAt.ShouldBe(default);
        sut.ExpiresAt.ShouldBe(default);
        sut.ExtensionCount.ShouldBe(0);
        sut.Metadata.ShouldNotBeNull();
        sut.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange
        var acquired = DateTimeOffset.UtcNow;
        var expires = acquired.AddMinutes(5);

        // Act
        var sut = new LockInfo
        {
            SessionId = "sess-1",
            LockToken = "token-abc",
            Type = LockType.Write,
            OwnerId = "owner-1",
            AcquiredAt = acquired,
            ExpiresAt = expires,
            ExtensionCount = 3,
            Metadata = { ["reason"] = "exclusive-processing" }
        };

        // Assert
        sut.SessionId.ShouldBe("sess-1");
        sut.LockToken.ShouldBe("token-abc");
        sut.Type.ShouldBe(LockType.Write);
        sut.OwnerId.ShouldBe("owner-1");
        sut.AcquiredAt.ShouldBe(acquired);
        sut.ExpiresAt.ShouldBe(expires);
        sut.ExtensionCount.ShouldBe(3);
        sut.Metadata["reason"].ShouldBe("exclusive-processing");
    }

    [Theory]
    [InlineData(LockType.Read)]
    [InlineData(LockType.Write)]
    [InlineData(LockType.UpgradeableRead)]
    public void Support_all_lock_types(LockType lockType)
    {
        // Arrange & Act
        var sut = new LockInfo { Type = lockType };

        // Assert
        sut.Type.ShouldBe(lockType);
    }
}
