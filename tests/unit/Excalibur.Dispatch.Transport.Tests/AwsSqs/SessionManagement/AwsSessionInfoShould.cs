// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSessionInfoShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var info = new AwsSessionInfo { SessionId = "session-1" };

		// Assert
		info.SessionId.ShouldBe("session-1");
		info.Status.ShouldBe(SessionStatus.Idle);
		info.CreatedAt.ShouldBe(default);
		info.LastAccessedAt.ShouldBe(default);
		info.MessageCount.ShouldBe(0);
		info.Data.ShouldNotBeNull();
		info.Data.ShouldBeEmpty();
		info.CurrentLock.ShouldBeNull();
		info.Metadata.ShouldNotBeNull();
		info.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var lockInfo = new SessionLockInfo
		{
			LockId = "lock-1",
			OwnerId = "worker-1",
		};

		// Act
		var info = new AwsSessionInfo
		{
			SessionId = "session-42",
			Status = SessionStatus.Active,
			CreatedAt = now.AddMinutes(-30),
			LastAccessedAt = now,
			MessageCount = 500,
			CurrentLock = lockInfo,
		};
		info.Data["key1"] = "value1";
		info.Metadata["env"] = "production";

		// Assert
		info.SessionId.ShouldBe("session-42");
		info.Status.ShouldBe(SessionStatus.Active);
		info.CreatedAt.ShouldBe(now.AddMinutes(-30));
		info.LastAccessedAt.ShouldBe(now);
		info.MessageCount.ShouldBe(500);
		info.CurrentLock.ShouldBeSameAs(lockInfo);
		info.CurrentLock.LockId.ShouldBe("lock-1");
		info.CurrentLock.OwnerId.ShouldBe("worker-1");
		info.Data["key1"].ShouldBe("value1");
		info.Metadata["env"].ShouldBe("production");
	}
}
