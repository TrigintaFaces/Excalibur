// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="SessionLockToken"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class SessionLockTokenShould
{
	[Fact]
	public void HaveEmptySessionId_ByDefault()
	{
		// Arrange & Act
		var token = new SessionLockToken();

		// Assert
		token.SessionId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyToken_ByDefault()
	{
		// Arrange & Act
		var token = new SessionLockToken();

		// Assert
		token.Token.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultAcquiredAt_ByDefault()
	{
		// Arrange & Act
		var token = new SessionLockToken();

		// Assert
		token.AcquiredAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultExpiresAt_ByDefault()
	{
		// Arrange & Act
		var token = new SessionLockToken();

		// Assert
		token.ExpiresAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveEmptyOwnerId_ByDefault()
	{
		// Arrange & Act
		var token = new SessionLockToken();

		// Assert
		token.OwnerId.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingSessionId()
	{
		// Arrange & Act
		var token = new SessionLockToken { SessionId = "session-123" };

		// Assert
		token.SessionId.ShouldBe("session-123");
	}

	[Fact]
	public void AllowSettingToken()
	{
		// Arrange & Act
		var token = new SessionLockToken { Token = "lock-token-abc" };

		// Assert
		token.Token.ShouldBe("lock-token-abc");
	}

	[Fact]
	public void AllowSettingAcquiredAt()
	{
		// Arrange
		var acquiredAt = DateTimeOffset.UtcNow;

		// Act
		var token = new SessionLockToken { AcquiredAt = acquiredAt };

		// Assert
		token.AcquiredAt.ShouldBe(acquiredAt);
	}

	[Fact]
	public void AllowSettingExpiresAt()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

		// Act
		var token = new SessionLockToken { ExpiresAt = expiresAt };

		// Assert
		token.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void AllowSettingOwnerId()
	{
		// Arrange & Act
		var token = new SessionLockToken { OwnerId = "owner-xyz" };

		// Assert
		token.OwnerId.ShouldBe("owner-xyz");
	}

	[Fact]
	public void IsValid_ReturnsTrueWhenNotExpired()
	{
		// Arrange
		var token = new SessionLockToken { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) };

		// Assert
		token.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void IsValid_ReturnsFalseWhenExpired()
	{
		// Arrange
		var token = new SessionLockToken { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) };

		// Assert
		token.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void IsValid_ReturnsFalseWhenExpiresAtIsDefault()
	{
		// Arrange
		var token = new SessionLockToken();

		// Assert - default DateTimeOffset is MinValue which is in the past
		token.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void IsValid_ReturnsFalseWhenExpiresAtEqualsNow()
	{
		// Arrange - set expires to slightly in the past to ensure it's expired
		var token = new SessionLockToken { ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(-1) };

		// Assert
		token.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void AllowCreatingFullyPopulatedToken()
	{
		// Arrange
		var acquiredAt = DateTimeOffset.UtcNow;
		var expiresAt = acquiredAt.AddMinutes(5);

		// Act
		var token = new SessionLockToken
		{
			SessionId = "session-123",
			Token = "token-abc",
			AcquiredAt = acquiredAt,
			ExpiresAt = expiresAt,
			OwnerId = "owner-xyz",
		};

		// Assert
		token.SessionId.ShouldBe("session-123");
		token.Token.ShouldBe("token-abc");
		token.AcquiredAt.ShouldBe(acquiredAt);
		token.ExpiresAt.ShouldBe(expiresAt);
		token.OwnerId.ShouldBe("owner-xyz");
		token.IsValid.ShouldBeTrue();
	}
}
