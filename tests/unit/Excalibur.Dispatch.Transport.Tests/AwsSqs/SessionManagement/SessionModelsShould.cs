// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using AwsSessionInfo = Excalibur.Dispatch.Transport.Aws.SessionInfo;
using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.PollingStatus;
using AwsSessionContext = Excalibur.Dispatch.Transport.Aws.SessionContext;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SessionModelsShould
{
	[Fact]
	public void SessionInfoHaveCorrectDefaults()
	{
		// Arrange & Act
		var info = new AwsSessionInfo { SessionId = "test-session" };

		// Assert
		info.SessionId.ShouldBe("test-session");
		info.Status.ShouldBe(SessionStatus.Idle);
		info.ConsumerId.ShouldBeNull();
		info.MessageCount.ShouldBe(0);
		info.PendingMessageCount.ShouldBe(0);
		info.Metrics.ShouldNotBeNull();
	}

	[Fact]
	public void SessionInfoAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var info = new AwsSessionInfo
		{
			SessionId = "sess-123",
			Status = SessionStatus.Active,
			ConsumerId = "consumer-1",
			CreatedAt = now,
			LastActivityAt = now,
			MessageCount = 50,
			PendingMessageCount = 5,
		};

		// Assert
		info.SessionId.ShouldBe("sess-123");
		info.Status.ShouldBe(SessionStatus.Active);
		info.ConsumerId.ShouldBe("consumer-1");
		info.CreatedAt.ShouldBe(now);
		info.LastActivityAt.ShouldBe(now);
		info.MessageCount.ShouldBe(50);
		info.PendingMessageCount.ShouldBe(5);
	}

	[Fact]
	public void SessionDataHaveCorrectDefaults()
	{
		// Arrange & Act
		var data = new SessionData();

		// Assert
		data.Id.ShouldBe(string.Empty);
		data.State.ShouldBe(AwsSessionState.Idle);
		data.ExpiresAt.ShouldBeNull();
		data.Metadata.ShouldBeEmpty();
		data.MessageCount.ShouldBe(0);
	}

	[Fact]
	public void SessionDataAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var data = new SessionData
		{
			Id = "sess-data-1",
			State = AwsSessionState.Active,
			CreatedAt = now,
			LastAccessedAt = now,
			ExpiresAt = now.AddHours(1),
			MessageCount = 100,
		};
		data.Metadata["key"] = "value";

		// Assert
		data.Id.ShouldBe("sess-data-1");
		data.State.ShouldBe(AwsSessionState.Active);
		data.CreatedAt.ShouldBe(now);
		data.LastAccessedAt.ShouldBe(now);
		data.ExpiresAt.ShouldBe(now.AddHours(1));
		data.MessageCount.ShouldBe(100);
		data.Metadata.Count.ShouldBe(1);
	}

	[Fact]
	public void SessionContextHaveCorrectDefaults()
	{
		// Arrange & Act
		var context = new AwsSessionContext { SessionId = "ctx-session" };

		// Assert
		context.SessionId.ShouldBe("ctx-session");
		context.Lock.ShouldBeNull();
		context.State.ShouldBeNull();
		context.ConsumerId.ShouldBeNull();
		context.Data.ShouldBeEmpty();
	}

	[Fact]
	public void SessionContextAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var context = new AwsSessionContext
		{
			SessionId = "ctx-123",
			State = AwsSessionState.Locked,
			ConsumerId = "worker-5",
			ProcessingStartedAt = now,
		};
		context.Data["operation"] = "batch-processing";

		// Assert
		context.SessionId.ShouldBe("ctx-123");
		context.State.ShouldBe(AwsSessionState.Locked);
		context.ConsumerId.ShouldBe("worker-5");
		context.ProcessingStartedAt.ShouldBe(now);
		context.Data.Count.ShouldBe(1);
	}

	[Fact]
	public void RedisSessionStoreOptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RedisSessionStoreOptions();

		// Assert
		options.KeyPrefix.ShouldBe("dispatch");
	}

	[Fact]
	public void RedisSessionStoreOptionsAllowSettingKeyPrefix()
	{
		// Arrange & Act
		var options = new RedisSessionStoreOptions { KeyPrefix = "custom:prefix" };

		// Assert
		options.KeyPrefix.ShouldBe("custom:prefix");
	}

	[Fact]
	public void SessionStatusEnumHaveCorrectValues()
	{
		// Assert
		((int)SessionStatus.Idle).ShouldBe(0);
		((int)SessionStatus.Active).ShouldBe(1);
		((int)SessionStatus.Locked).ShouldBe(2);
		((int)SessionStatus.Closed).ShouldBe(3);
		((int)SessionStatus.Expired).ShouldBe(4);
		((int)SessionStatus.Suspended).ShouldBe(5);
		((int)SessionStatus.Closing).ShouldBe(6);
	}

	[Fact]
	public void AwsSessionStateEnumMatchSessionStatus()
	{
		// Assert — AwsSessionState values should map to SessionStatus values
		((int)AwsSessionState.Idle).ShouldBe((int)SessionStatus.Idle);
		((int)AwsSessionState.Active).ShouldBe((int)SessionStatus.Active);
		((int)AwsSessionState.Locked).ShouldBe((int)SessionStatus.Locked);
		((int)AwsSessionState.Closed).ShouldBe((int)SessionStatus.Closed);
		((int)AwsSessionState.Expired).ShouldBe((int)SessionStatus.Expired);
		((int)AwsSessionState.Suspended).ShouldBe((int)SessionStatus.Suspended);
		((int)AwsSessionState.Closing).ShouldBe((int)SessionStatus.Closing);
	}

	[Fact]
	public void AwsPollingStatusEnumHaveCorrectValues()
	{
		// Assert
		((int)AwsPollingStatus.Inactive).ShouldBe(0);
		((int)AwsPollingStatus.Active).ShouldBe(1);
		((int)AwsPollingStatus.Stopping).ShouldBe(2);
		((int)AwsPollingStatus.Error).ShouldBe(3);
	}
}
