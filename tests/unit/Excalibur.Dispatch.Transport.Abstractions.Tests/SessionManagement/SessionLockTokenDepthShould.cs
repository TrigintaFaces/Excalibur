using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionLockTokenDepthShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new SessionLockToken();

        // Assert
        sut.SessionId.ShouldBe(string.Empty);
        sut.Token.ShouldBe(string.Empty);
        sut.AcquiredAt.ShouldBe(default);
        sut.ExpiresAt.ShouldBe(default);
        sut.OwnerId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Report_valid_when_expires_in_future()
    {
        // Arrange
        var sut = new SessionLockToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act & Assert
        sut.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Report_invalid_when_expired()
    {
        // Arrange
        var sut = new SessionLockToken
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        // Act & Assert
        sut.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Report_invalid_when_default_expiry()
    {
        // Arrange
        var sut = new SessionLockToken();

        // Act & Assert
        sut.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Allow_setting_all_properties_via_init()
    {
        // Arrange
        var acquired = DateTimeOffset.UtcNow;
        var expires = acquired.AddMinutes(30);

        // Act
        var sut = new SessionLockToken
        {
            SessionId = "sess-abc",
            Token = "lock-token-xyz",
            AcquiredAt = acquired,
            ExpiresAt = expires,
            OwnerId = "consumer-1"
        };

        // Assert
        sut.SessionId.ShouldBe("sess-abc");
        sut.Token.ShouldBe("lock-token-xyz");
        sut.AcquiredAt.ShouldBe(acquired);
        sut.ExpiresAt.ShouldBe(expires);
        sut.OwnerId.ShouldBe("consumer-1");
        sut.IsValid.ShouldBeTrue();
    }
}
