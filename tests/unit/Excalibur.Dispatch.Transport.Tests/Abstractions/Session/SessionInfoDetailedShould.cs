// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Session;

/// <summary>
/// Detailed unit tests for <see cref="SessionInfo"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class SessionInfoDetailedShould
{
	[Fact]
	public void HaveEmptySessionId_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.SessionId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultState_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.State.ShouldBe(default(DispatchSessionState));
	}

	[Fact]
	public void HaveDefaultCreatedAt_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.CreatedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultLastAccessedAt_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.LastAccessedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveNullExpiresAt_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroMessageCount_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.MessageCount.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroPendingMessageCount_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.PendingMessageCount.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.Metadata.ShouldNotBeNull();
		info.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void HaveNullLockToken_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.LockToken.ShouldBeNull();
	}

	[Fact]
	public void HaveNullOwnerId_ByDefault()
	{
		// Arrange & Act
		var info = new SessionInfo();

		// Assert
		info.OwnerId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingSessionId()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.SessionId = "session-abc-123";

		// Assert
		info.SessionId.ShouldBe("session-abc-123");
	}

	[Fact]
	public void AllowSettingState()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.State = DispatchSessionState.Active;

		// Assert
		info.State.ShouldBe(DispatchSessionState.Active);
	}

	[Fact]
	public void AllowSettingCreatedAt()
	{
		// Arrange
		var info = new SessionInfo();
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		info.CreatedAt = createdAt;

		// Assert
		info.CreatedAt.ShouldBe(createdAt);
	}

	[Fact]
	public void AllowSettingLastAccessedAt()
	{
		// Arrange
		var info = new SessionInfo();
		var lastAccessedAt = DateTimeOffset.UtcNow;

		// Act
		info.LastAccessedAt = lastAccessedAt;

		// Assert
		info.LastAccessedAt.ShouldBe(lastAccessedAt);
	}

	[Fact]
	public void AllowSettingExpiresAt()
	{
		// Arrange
		var info = new SessionInfo();
		var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		info.ExpiresAt = expiresAt;

		// Assert
		info.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void AllowSettingMessageCount()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.MessageCount = 5000;

		// Assert
		info.MessageCount.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingPendingMessageCount()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.PendingMessageCount = 100;

		// Assert
		info.PendingMessageCount.ShouldBe(100);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.Metadata["key1"] = "value1";
		info.Metadata["key2"] = "value2";

		// Assert
		info.Metadata.Count.ShouldBe(2);
		info.Metadata["key1"].ShouldBe("value1");
		info.Metadata["key2"].ShouldBe("value2");
	}

	[Fact]
	public void AllowSettingLockToken()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.LockToken = "lock-token-xyz";

		// Assert
		info.LockToken.ShouldBe("lock-token-xyz");
	}

	[Fact]
	public void AllowSettingOwnerId()
	{
		// Arrange
		var info = new SessionInfo();

		// Act
		info.OwnerId = "owner-123";

		// Assert
		info.OwnerId.ShouldBe("owner-123");
	}

	[Fact]
	public void AllowCreatingFullyPopulatedSessionInfo()
	{
		// Arrange
		var createdAt = DateTimeOffset.UtcNow.AddMinutes(-30);
		var lastAccessedAt = DateTimeOffset.UtcNow;
		var expiresAt = DateTimeOffset.UtcNow.AddHours(1);

		// Act
		var info = new SessionInfo
		{
			SessionId = "session-full-123",
			State = DispatchSessionState.Active,
			CreatedAt = createdAt,
			LastAccessedAt = lastAccessedAt,
			ExpiresAt = expiresAt,
			MessageCount = 5000,
			PendingMessageCount = 50,
			LockToken = "lock-token-abc",
			OwnerId = "owner-xyz",
		};
		info.Metadata["purpose"] = "testing";

		// Assert
		info.SessionId.ShouldBe("session-full-123");
		info.State.ShouldBe(DispatchSessionState.Active);
		info.CreatedAt.ShouldBe(createdAt);
		info.LastAccessedAt.ShouldBe(lastAccessedAt);
		info.ExpiresAt.ShouldBe(expiresAt);
		info.MessageCount.ShouldBe(5000);
		info.PendingMessageCount.ShouldBe(50);
		info.LockToken.ShouldBe("lock-token-abc");
		info.OwnerId.ShouldBe("owner-xyz");
		info.Metadata["purpose"].ShouldBe("testing");
	}
}
