// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SessionExtendedModelsShould
{
	[Fact]
	public void SessionLockInfoHaveCorrectDefaults()
	{
		// Arrange & Act
		var info = new SessionLockInfo { LockId = "lock-1", OwnerId = "owner-1" };

		// Assert
		info.LockId.ShouldBe("lock-1");
		info.OwnerId.ShouldBe("owner-1");
		info.AcquiredAt.ShouldBe(default);
		info.ExpiresAt.ShouldBe(default);
		info.RenewCount.ShouldBe(0);
		info.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void SessionLockInfoAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var info = new SessionLockInfo
		{
			LockId = "lock-2",
			OwnerId = "worker-5",
			AcquiredAt = now,
			ExpiresAt = now.AddMinutes(5),
			RenewCount = 3,
		};
		info.Metadata["priority"] = "high";

		// Assert
		info.LockId.ShouldBe("lock-2");
		info.OwnerId.ShouldBe("worker-5");
		info.AcquiredAt.ShouldBe(now);
		info.ExpiresAt.ShouldBe(now.AddMinutes(5));
		info.RenewCount.ShouldBe(3);
		info.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void AdvancedSessionStateHaveCorrectDefaults()
	{
		// Arrange & Act
		var state = new AdvancedSessionState();

		// Assert
		state.SessionId.ShouldBe(string.Empty);
		state.Status.ShouldBe(SessionStatus.Idle);
		state.MessageCount.ShouldBe(0);
		state.LockToken.ShouldBeNull();
		state.LockOwner.ShouldBeNull();
		state.ExpiresUtc.ShouldBeNull();
		state.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AdvancedSessionStateAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var state = new AdvancedSessionState
		{
			SessionId = "sess-adv-1",
			Status = SessionStatus.Active,
			LastActivityUtc = now,
			CreatedUtc = now.AddHours(-1),
			ExpiresUtc = now.AddHours(1),
			MessageCount = 500,
			LockToken = "token-abc",
			LockOwner = "worker-1",
		};
		state.Metadata["region"] = "us-east-1";

		// Assert
		state.SessionId.ShouldBe("sess-adv-1");
		state.Status.ShouldBe(SessionStatus.Active);
		state.LastActivityUtc.ShouldBe(now);
		state.CreatedUtc.ShouldBe(now.AddHours(-1));
		state.ExpiresUtc.ShouldBe(now.AddHours(1));
		state.MessageCount.ShouldBe(500);
		state.LockToken.ShouldBe("token-abc");
		state.LockOwner.ShouldBe("worker-1");
		state.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void SessionMetricsHaveCorrectDefaults()
	{
		// Arrange & Act
		var metrics = new SessionMetrics();

		// Assert
		metrics.TotalProcessingTime.ShouldBe(TimeSpan.Zero);
		metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.Zero);
		metrics.SuccessfulMessages.ShouldBe(0);
		metrics.FailedMessages.ShouldBe(0);
		metrics.RetriedMessages.ShouldBe(0);
		metrics.LockRenewals.ShouldBe(0);
	}

	[Fact]
	public void SessionMetricsAllowSettingAllProperties()
	{
		// Arrange & Act
		var metrics = new SessionMetrics
		{
			TotalProcessingTime = TimeSpan.FromMinutes(5),
			AverageMessageProcessingTime = TimeSpan.FromMilliseconds(50),
			SuccessfulMessages = 100,
			FailedMessages = 5,
			RetriedMessages = 10,
			LockRenewals = 3,
		};

		// Assert
		metrics.TotalProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
		metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		metrics.SuccessfulMessages.ShouldBe(100);
		metrics.FailedMessages.ShouldBe(5);
		metrics.RetriedMessages.ShouldBe(10);
		metrics.LockRenewals.ShouldBe(3);
	}
}
