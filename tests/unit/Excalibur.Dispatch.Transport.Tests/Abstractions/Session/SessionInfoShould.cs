// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Session;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class SessionInfoShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var info = new SessionInfo();

        info.SessionId.ShouldBe(string.Empty);
        info.State.ShouldBe(default(DispatchSessionState));
        info.CreatedAt.ShouldBe(default);
        info.LastAccessedAt.ShouldBe(default);
        info.ExpiresAt.ShouldBeNull();
        info.MessageCount.ShouldBe(0);
        info.PendingMessageCount.ShouldBe(0);
        info.Metadata.ShouldNotBeNull();
        info.Metadata.ShouldBeEmpty();
        info.LockToken.ShouldBeNull();
        info.OwnerId.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var lastAccessed = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var info = new SessionInfo
        {
            SessionId = "session-123",
            State = DispatchSessionState.Active,
            CreatedAt = createdAt,
            LastAccessedAt = lastAccessed,
            ExpiresAt = expiresAt,
            MessageCount = 5000,
            PendingMessageCount = 100,
            LockToken = "lock-token-abc",
            OwnerId = "consumer-1"
        };

        info.SessionId.ShouldBe("session-123");
        info.State.ShouldBe(DispatchSessionState.Active);
        info.CreatedAt.ShouldBe(createdAt);
        info.LastAccessedAt.ShouldBe(lastAccessed);
        info.ExpiresAt.ShouldBe(expiresAt);
        info.MessageCount.ShouldBe(5000);
        info.PendingMessageCount.ShouldBe(100);
        info.LockToken.ShouldBe("lock-token-abc");
        info.OwnerId.ShouldBe("consumer-1");
    }

    [Theory]
    [InlineData(DispatchSessionState.Active)]
    [InlineData(DispatchSessionState.Idle)]
    [InlineData(DispatchSessionState.Locked)]
    [InlineData(DispatchSessionState.Expired)]
    [InlineData(DispatchSessionState.Closing)]
    [InlineData(DispatchSessionState.Closed)]
    public void AllowSettingAllSessionStates(DispatchSessionState state)
    {
        var info = new SessionInfo { State = state };

        info.State.ShouldBe(state);
    }

    [Fact]
    public void AllowAddingMetadata()
    {
        var info = new SessionInfo
        {
            SessionId = "session-with-metadata",
            Metadata =
            {
                ["customer-id"] = "cust-123",
                ["region"] = "us-east-1"
            }
        };

        info.Metadata.Count.ShouldBe(2);
        info.Metadata["customer-id"].ShouldBe("cust-123");
    }

    [Fact]
    public void AllowNullExpiresAt()
    {
        var info = new SessionInfo
        {
            SessionId = "no-expiry-session",
            ExpiresAt = null
        };

        info.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void AllowNullLockToken()
    {
        var info = new SessionInfo
        {
            SessionId = "unlocked-session",
            State = DispatchSessionState.Idle,
            LockToken = null
        };

        info.LockToken.ShouldBeNull();
    }

    [Fact]
    public void AllowNullOwnerId()
    {
        var info = new SessionInfo
        {
            SessionId = "unowned-session",
            OwnerId = null
        };

        info.OwnerId.ShouldBeNull();
    }

    [Fact]
    public void AllowHighMessageCounts()
    {
        var info = new SessionInfo
        {
            SessionId = "high-volume-session",
            MessageCount = 10_000_000,
            PendingMessageCount = 100_000
        };

        info.MessageCount.ShouldBe(10_000_000);
        info.PendingMessageCount.ShouldBe(100_000);
    }

    [Fact]
    public void AllowActiveSessionConfiguration()
    {
        var info = new SessionInfo
        {
            SessionId = "active-session",
            State = DispatchSessionState.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            MessageCount = 100,
            PendingMessageCount = 10,
            LockToken = "lock-123",
            OwnerId = "processor-1"
        };

        info.State.ShouldBe(DispatchSessionState.Active);
        info.LockToken.ShouldNotBeNull();
        info.OwnerId.ShouldNotBeNull();
    }

    [Fact]
    public void AllowLockedSessionConfiguration()
    {
        var info = new SessionInfo
        {
            SessionId = "locked-session",
            State = DispatchSessionState.Locked,
            LockToken = "exclusive-lock-token",
            OwnerId = "exclusive-owner"
        };

        info.State.ShouldBe(DispatchSessionState.Locked);
        info.LockToken.ShouldBe("exclusive-lock-token");
    }

    [Fact]
    public void AllowExpiredSessionConfiguration()
    {
        var now = DateTimeOffset.UtcNow;
        var info = new SessionInfo
        {
            SessionId = "expired-session",
            State = DispatchSessionState.Expired,
            CreatedAt = now.AddHours(-2),
            ExpiresAt = now.AddHours(-1),
            LockToken = null,
            OwnerId = null
        };

        info.State.ShouldBe(DispatchSessionState.Expired);
        info.ExpiresAt.ShouldNotBeNull();
        info.ExpiresAt.Value.ShouldBeLessThan(now);
    }

    [Fact]
    public void AllowClosedSessionConfiguration()
    {
        var info = new SessionInfo
        {
            SessionId = "closed-session",
            State = DispatchSessionState.Closed,
            MessageCount = 5000,
            PendingMessageCount = 0,
            LockToken = null
        };

        info.State.ShouldBe(DispatchSessionState.Closed);
        info.PendingMessageCount.ShouldBe(0);
    }

    [Fact]
    public void TrackSessionTimestamps()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var lastAccessed = DateTimeOffset.UtcNow.AddMinutes(-5);

        var info = new SessionInfo
        {
            SessionId = "tracked-session",
            CreatedAt = createdAt,
            LastAccessedAt = lastAccessed
        };

        info.CreatedAt.ShouldBeLessThan(info.LastAccessedAt);
    }

    [Fact]
    public void VerifyDispatchSessionStateEnumValues()
    {
        ((int)DispatchSessionState.Active).ShouldBe(0);
        ((int)DispatchSessionState.Idle).ShouldBe(1);
        ((int)DispatchSessionState.Locked).ShouldBe(2);
        ((int)DispatchSessionState.Expired).ShouldBe(3);
        ((int)DispatchSessionState.Closing).ShouldBe(4);
        ((int)DispatchSessionState.Closed).ShouldBe(5);
    }
}
