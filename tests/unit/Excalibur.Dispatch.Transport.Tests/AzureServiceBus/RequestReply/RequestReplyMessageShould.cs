// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.RequestReply;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RequestReplyMessageShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var message = new RequestReplyMessage();

		// Assert
		message.MessageId.ShouldNotBeNullOrWhiteSpace();
		message.Body.ShouldBeEmpty();
		message.CorrelationId.ShouldBeNull();
		message.SessionId.ShouldBeNull();
		message.ContentType.ShouldBeNull();
		message.Subject.ShouldBeNull();
		message.Properties.ShouldNotBeNull();
		message.Properties.ShouldBeEmpty();
		message.ReplyTo.ShouldBeNull();
		message.ReplyToSessionId.ShouldBeNull();
		message.TimeToLive.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var message = new RequestReplyMessage
		{
			MessageId = "custom-id",
			Body = [1, 2, 3],
			CorrelationId = "corr-123",
			SessionId = "session-1",
			ContentType = "application/json",
			Subject = "OrderRequest",
			ReplyTo = "reply-queue",
			ReplyToSessionId = "reply-session",
			TimeToLive = TimeSpan.FromMinutes(5),
		};

		// Assert
		message.MessageId.ShouldBe("custom-id");
		message.Body.ShouldBe([1, 2, 3]);
		message.CorrelationId.ShouldBe("corr-123");
		message.SessionId.ShouldBe("session-1");
		message.ContentType.ShouldBe("application/json");
		message.Subject.ShouldBe("OrderRequest");
		message.ReplyTo.ShouldBe("reply-queue");
		message.ReplyToSessionId.ShouldBe("reply-session");
		message.TimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void SupportApplicationProperties()
	{
		// Arrange
		var message = new RequestReplyMessage();

		// Act
		message.Properties["key1"] = "value1";
		message.Properties["key2"] = 42;

		// Assert
		message.Properties.Count.ShouldBe(2);
		message.Properties["key1"].ShouldBe("value1");
		message.Properties["key2"].ShouldBe(42);
	}

	[Fact]
	public void GenerateUniqueMessageIds()
	{
		// Arrange & Act
		var msg1 = new RequestReplyMessage();
		var msg2 = new RequestReplyMessage();

		// Assert
		msg1.MessageId.ShouldNotBe(msg2.MessageId);
	}
}
