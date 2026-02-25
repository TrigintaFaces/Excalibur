// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Serialization;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SerializationRecordsShould
{
	[Fact]
	public void CheckpointDataStoreAllFields()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var checkpoint = new CheckpointData("partition-0", 42L, 1024L, now);

		// Assert
		checkpoint.PartitionId.ShouldBe("partition-0");
		checkpoint.SequenceNumber.ShouldBe(42L);
		checkpoint.Offset.ShouldBe(1024L);
		checkpoint.CheckpointTime.ShouldBe(now);
	}

	[Fact]
	public void CheckpointDataSupportRecordEquality()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var c1 = new CheckpointData("p0", 1, 0, now);
		var c2 = new CheckpointData("p0", 1, 0, now);

		// Assert
		c1.ShouldBe(c2);
	}

	[Fact]
	public void DeadLetterInfoStoreAllFields()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new DeadLetterInfo("msg-1", "MaxDeliveryExceeded", "Max delivery count reached", now, 10);

		// Assert
		info.MessageId.ShouldBe("msg-1");
		info.DeadLetterReason.ShouldBe("MaxDeliveryExceeded");
		info.DeadLetterErrorDescription.ShouldBe("Max delivery count reached");
		info.DeadLetterTime.ShouldBe(now);
		info.DeliveryCount.ShouldBe(10);
	}

	[Fact]
	public void PartitionContextStoreAllFields()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var ctx = new PartitionContext("partition-1", "$Default", "my-hub", 500L, now);

		// Assert
		ctx.PartitionId.ShouldBe("partition-1");
		ctx.ConsumerGroup.ShouldBe("$Default");
		ctx.EventHubName.ShouldBe("my-hub");
		ctx.LastEnqueuedSequenceNumber.ShouldBe(500L);
		ctx.LastEnqueuedTime.ShouldBe(now);
	}

	[Fact]
	public void RetryInfoStoreAllFields()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var retry = new RetryInfo(3, now, "Timeout", TimeSpan.FromSeconds(5));

		// Assert
		retry.AttemptNumber.ShouldBe(3);
		retry.NextRetryTime.ShouldBe(now);
		retry.LastError.ShouldBe("Timeout");
		retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void RetryInfoAllowNullLastError()
	{
		// Act
		var retry = new RetryInfo(1, DateTimeOffset.UtcNow, null, TimeSpan.FromSeconds(1));

		// Assert
		retry.LastError.ShouldBeNull();
	}

	[Fact]
	public void ServiceBusSessionStateStoreAllFields()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var state = new byte[] { 1, 2, 3 };
		var props = new Dictionary<string, object> { ["key"] = "val" };

		// Act
		var session = new ServiceBusSessionState("sess-1", state, now, props);

		// Assert
		session.SessionId.ShouldBe("sess-1");
		session.State.ShouldBe(state);
		session.LastAccessTime.ShouldBe(now);
		session.CustomProperties.ShouldNotBeNull();
		session.CustomProperties!.Count.ShouldBe(1);
	}

	[Fact]
	public void ServiceBusSessionStateAllowNullStateAndProperties()
	{
		// Act
		var session = new ServiceBusSessionState("sess-2", null, DateTimeOffset.UtcNow);

		// Assert
		session.State.ShouldBeNull();
		session.CustomProperties.ShouldBeNull();
	}
}
