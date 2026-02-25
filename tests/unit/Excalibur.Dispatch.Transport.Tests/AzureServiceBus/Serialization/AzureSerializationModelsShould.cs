// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AzureSerializationModelsShould
{
	[Fact]
	public void CreateCheckpointData()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var data = new CheckpointData("partition-0", 42, 1024, now);

		// Assert
		data.PartitionId.ShouldBe("partition-0");
		data.SequenceNumber.ShouldBe(42);
		data.Offset.ShouldBe(1024);
		data.CheckpointTime.ShouldBe(now);
	}

	[Fact]
	public void SupportCheckpointDataRecordEquality()
	{
		var now = DateTimeOffset.UtcNow;
		var c1 = new CheckpointData("p0", 1, 100, now);
		var c2 = new CheckpointData("p0", 1, 100, now);
		c1.ShouldBe(c2);
	}

	[Fact]
	public void CreateDeadLetterInfo()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new DeadLetterInfo("msg-123", "MaxRetries", "Too many retries", now, 5);

		// Assert
		info.MessageId.ShouldBe("msg-123");
		info.DeadLetterReason.ShouldBe("MaxRetries");
		info.DeadLetterErrorDescription.ShouldBe("Too many retries");
		info.DeadLetterTime.ShouldBe(now);
		info.DeliveryCount.ShouldBe(5);
	}

	[Fact]
	public void CreatePartitionContext()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var ctx = new PartitionContext("partition-0", "$Default", "my-hub", 999, now);

		// Assert
		ctx.PartitionId.ShouldBe("partition-0");
		ctx.ConsumerGroup.ShouldBe("$Default");
		ctx.EventHubName.ShouldBe("my-hub");
		ctx.LastEnqueuedSequenceNumber.ShouldBe(999);
		ctx.LastEnqueuedTime.ShouldBe(now);
	}

	[Fact]
	public void CreateRetryInfo()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new RetryInfo(3, now, "Connection refused", TimeSpan.FromSeconds(30));

		// Assert
		info.AttemptNumber.ShouldBe(3);
		info.NextRetryTime.ShouldBe(now);
		info.LastError.ShouldBe("Connection refused");
		info.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void CreateRetryInfoWithNullError()
	{
		// Arrange & Act
		var info = new RetryInfo(1, DateTimeOffset.UtcNow, null, TimeSpan.FromSeconds(5));

		// Assert
		info.LastError.ShouldBeNull();
	}

	[Fact]
	public void CreateServiceBusSessionState()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var state = new byte[] { 1, 2, 3 };

		// Act
		var session = new ServiceBusSessionState("session-1", state, now);

		// Assert
		session.SessionId.ShouldBe("session-1");
		session.State.ShouldBe(state);
		session.LastAccessTime.ShouldBe(now);
		session.CustomProperties.ShouldBeNull();
	}

	[Fact]
	public void CreateServiceBusSessionStateWithCustomProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var props = new Dictionary<string, object> { ["key"] = "value" };

		// Act
		var session = new ServiceBusSessionState("session-2", null, now, props);

		// Assert
		session.State.ShouldBeNull();
		session.CustomProperties.ShouldNotBeNull();
		session.CustomProperties!["key"].ShouldBe("value");
	}
}
