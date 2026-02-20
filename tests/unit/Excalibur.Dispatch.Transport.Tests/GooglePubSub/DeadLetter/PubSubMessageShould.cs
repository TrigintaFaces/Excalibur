// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubMessageShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var message = new PubSubMessage();

		// Assert
		message.MessageId.ShouldBe(string.Empty);
		message.Data.ShouldBeEmpty();
		message.Attributes.ShouldBeEmpty();
		message.PublishTime.ShouldBe(default);
		message.AckId.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var data = new byte[] { 1, 2, 3 };

		// Act
		var message = new PubSubMessage
		{
			MessageId = "msg-001",
			Data = data,
			Attributes = new Dictionary<string, string> { ["type"] = "OrderCreated" },
			PublishTime = now,
			AckId = "ack-001",
		};

		// Assert
		message.MessageId.ShouldBe("msg-001");
		message.Data.ShouldBe(data);
		message.Attributes["type"].ShouldBe("OrderCreated");
		message.PublishTime.ShouldBe(now);
		message.AckId.ShouldBe("ack-001");
	}
}
