using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionInfoShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new SessionInfo();

        // Assert
        sut.SessionId.ShouldBe(string.Empty);
        sut.State.ShouldBe(DispatchSessionState.Active);
        sut.CreatedAt.ShouldBe(default);
        sut.LastAccessedAt.ShouldBe(default);
        sut.ExpiresAt.ShouldBeNull();
        sut.MessageCount.ShouldBe(0);
        sut.PendingMessageCount.ShouldBe(0);
        sut.Metadata.ShouldNotBeNull();
        sut.Metadata.ShouldBeEmpty();
        sut.LockToken.ShouldBeNull();
        sut.OwnerId.ShouldBeNull();
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange
        var created = DateTimeOffset.UtcNow.AddHours(-1);
        var accessed = DateTimeOffset.UtcNow;
        var expires = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        var sut = new SessionInfo
        {
            SessionId = "session-42",
            State = DispatchSessionState.Locked,
            CreatedAt = created,
            LastAccessedAt = accessed,
            ExpiresAt = expires,
            MessageCount = 100,
            PendingMessageCount = 5,
            LockToken = "lock-token-abc",
            OwnerId = "consumer-1",
            Metadata = { ["topic"] = "orders" }
        };

        // Assert
        sut.SessionId.ShouldBe("session-42");
        sut.State.ShouldBe(DispatchSessionState.Locked);
        sut.CreatedAt.ShouldBe(created);
        sut.LastAccessedAt.ShouldBe(accessed);
        sut.ExpiresAt.ShouldBe(expires);
        sut.MessageCount.ShouldBe(100);
        sut.PendingMessageCount.ShouldBe(5);
        sut.LockToken.ShouldBe("lock-token-abc");
        sut.OwnerId.ShouldBe("consumer-1");
        sut.Metadata["topic"].ShouldBe("orders");
    }

    [Theory]
    [InlineData(DispatchSessionState.Active)]
    [InlineData(DispatchSessionState.Idle)]
    [InlineData(DispatchSessionState.Locked)]
    [InlineData(DispatchSessionState.Expired)]
    [InlineData(DispatchSessionState.Closing)]
    [InlineData(DispatchSessionState.Closed)]
    public void Support_all_session_states(DispatchSessionState state)
    {
        // Arrange & Act
        var sut = new SessionInfo { State = state };

        // Assert
        sut.State.ShouldBe(state);
    }
}
