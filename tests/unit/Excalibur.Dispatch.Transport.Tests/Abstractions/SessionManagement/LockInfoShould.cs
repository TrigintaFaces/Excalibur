// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="LockInfo"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class LockInfoShould
{
	[Fact]
	public void HaveEmptySessionId_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.SessionId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyLockToken_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.LockToken.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultLockType_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.Type.ShouldBe(default(LockType));
	}

	[Fact]
	public void HaveEmptyOwnerId_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.OwnerId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultAcquiredAt_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.AcquiredAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultExpiresAt_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.ExpiresAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveZeroExtensionCount_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.ExtensionCount.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyMetadata_ByDefault()
	{
		// Arrange & Act
		var info = new LockInfo();

		// Assert
		info.Metadata.ShouldNotBeNull();
		info.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingSessionId()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.SessionId = "session-123";

		// Assert
		info.SessionId.ShouldBe("session-123");
	}

	[Fact]
	public void AllowSettingLockToken()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.LockToken = "token-abc";

		// Assert
		info.LockToken.ShouldBe("token-abc");
	}

	[Fact]
	public void AllowSettingType()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.Type = LockType.Write;

		// Assert
		info.Type.ShouldBe(LockType.Write);
	}

	[Fact]
	public void AllowSettingOwnerId()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.OwnerId = "owner-xyz";

		// Assert
		info.OwnerId.ShouldBe("owner-xyz");
	}

	[Fact]
	public void AllowSettingAcquiredAt()
	{
		// Arrange
		var info = new LockInfo();
		var acquiredAt = DateTimeOffset.UtcNow;

		// Act
		info.AcquiredAt = acquiredAt;

		// Assert
		info.AcquiredAt.ShouldBe(acquiredAt);
	}

	[Fact]
	public void AllowSettingExpiresAt()
	{
		// Arrange
		var info = new LockInfo();
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

		// Act
		info.ExpiresAt = expiresAt;

		// Assert
		info.ExpiresAt.ShouldBe(expiresAt);
	}

	[Fact]
	public void AllowSettingExtensionCount()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.ExtensionCount = 3;

		// Assert
		info.ExtensionCount.ShouldBe(3);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var info = new LockInfo();

		// Act
		info.Metadata["key1"] = "value1";
		info.Metadata["key2"] = "value2";

		// Assert
		info.Metadata.Count.ShouldBe(2);
		info.Metadata["key1"].ShouldBe("value1");
		info.Metadata["key2"].ShouldBe("value2");
	}

	[Fact]
	public void AllowCreatingFullyPopulatedLockInfo()
	{
		// Arrange
		var acquiredAt = DateTimeOffset.UtcNow;
		var expiresAt = acquiredAt.AddMinutes(5);

		// Act
		var info = new LockInfo
		{
			SessionId = "session-123",
			LockToken = "token-abc",
			Type = LockType.Write,
			OwnerId = "owner-xyz",
			AcquiredAt = acquiredAt,
			ExpiresAt = expiresAt,
			ExtensionCount = 2,
		};
		info.Metadata["purpose"] = "testing";

		// Assert
		info.SessionId.ShouldBe("session-123");
		info.LockToken.ShouldBe("token-abc");
		info.Type.ShouldBe(LockType.Write);
		info.OwnerId.ShouldBe("owner-xyz");
		info.AcquiredAt.ShouldBe(acquiredAt);
		info.ExpiresAt.ShouldBe(expiresAt);
		info.ExtensionCount.ShouldBe(2);
		info.Metadata["purpose"].ShouldBe("testing");
	}
}
