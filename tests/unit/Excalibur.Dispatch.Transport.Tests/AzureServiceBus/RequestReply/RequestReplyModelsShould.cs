// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.RequestReply;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class RequestReplyModelsShould
{
	[Fact]
	public void RequestReplyOptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RequestReplyOptions();

		// Assert
		options.ReplyQueueName.ShouldBe(string.Empty);
		options.ReplyTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.RequestTimeToLive.ShouldBe(TimeSpan.FromSeconds(60));
		options.MaxConcurrentRequests.ShouldBe(100);
	}

	[Fact]
	public void RequestReplyOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RequestReplyOptions
		{
			ReplyQueueName = "reply-queue",
			ReplyTimeout = TimeSpan.FromMinutes(2),
			RequestTimeToLive = TimeSpan.FromMinutes(5),
			MaxConcurrentRequests = 50,
		};

		// Assert
		options.ReplyQueueName.ShouldBe("reply-queue");
		options.ReplyTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.RequestTimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxConcurrentRequests.ShouldBe(50);
	}

	[Fact]
	public void RequestReplyMessageHaveCorrectDefaults()
	{
		// Arrange & Act
		var msg = new RequestReplyMessage();

		// Assert
		msg.MessageId.ShouldNotBeNullOrEmpty();
		msg.Body.ShouldBeEmpty();
		msg.CorrelationId.ShouldBeNull();
		msg.SessionId.ShouldBeNull();
		msg.ContentType.ShouldBeNull();
		msg.Subject.ShouldBeNull();
		msg.Properties.ShouldBeEmpty();
		msg.ReplyTo.ShouldBeNull();
		msg.ReplyToSessionId.ShouldBeNull();
		msg.TimeToLive.ShouldBeNull();
	}

	[Fact]
	public void RequestReplyMessageAllowSettingAllProperties()
	{
		// Arrange & Act
		var msg = new RequestReplyMessage
		{
			MessageId = "msg-1",
			Body = new byte[] { 1, 2, 3 },
			CorrelationId = "corr-1",
			SessionId = "sess-1",
			ContentType = "application/json",
			Subject = "OrderCreated",
			ReplyTo = "reply-queue",
			ReplyToSessionId = "reply-sess",
			TimeToLive = TimeSpan.FromMinutes(5),
		};
		msg.Properties["key"] = "value";

		// Assert
		msg.MessageId.ShouldBe("msg-1");
		msg.Body.Length.ShouldBe(3);
		msg.CorrelationId.ShouldBe("corr-1");
		msg.SessionId.ShouldBe("sess-1");
		msg.ContentType.ShouldBe("application/json");
		msg.Subject.ShouldBe("OrderCreated");
		msg.ReplyTo.ShouldBe("reply-queue");
		msg.ReplyToSessionId.ShouldBe("reply-sess");
		msg.TimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
		msg.Properties.Count.ShouldBe(1);
	}

	[Fact]
	public void RequestReplyMessageGenerateUniqueIds()
	{
		// Arrange & Act
		var msg1 = new RequestReplyMessage();
		var msg2 = new RequestReplyMessage();

		// Assert
		msg1.MessageId.ShouldNotBe(msg2.MessageId);
	}
}
