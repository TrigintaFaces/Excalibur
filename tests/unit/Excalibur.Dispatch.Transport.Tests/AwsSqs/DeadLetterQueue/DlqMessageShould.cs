// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqMessageShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var message = new DlqMessage { MessageId = "msg-1", Body = "test-body" };

		// Assert
		message.MessageId.ShouldBe("msg-1");
		message.Body.ShouldBe("test-body");
		message.ReceiptHandle.ShouldBeNull();
		message.SourceQueueUrl.ShouldBeNull();
		message.AttemptCount.ShouldBe(0);
		message.FirstSentTimestamp.ShouldBe(default);
		message.MovedToDlqTimestamp.ShouldBeNull();
		message.Attributes.ShouldNotBeNull();
		message.Attributes.ShouldBeEmpty();
		message.Metadata.ShouldNotBeNull();
		message.Metadata.ShouldBeEmpty();
		message.LastError.ShouldBeNull();
		message.DlqReason.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-dlq");

		// Act
		var message = new DlqMessage
		{
			MessageId = "msg-42",
			Body = "{\"event\":\"test\"}",
			ReceiptHandle = "receipt-handle-abc",
			SourceQueueUrl = queueUrl,
			AttemptCount = 5,
			FirstSentTimestamp = now.AddHours(-1),
			MovedToDlqTimestamp = now,
			LastError = "Deserialization failed",
			DlqReason = "Max retries exceeded",
		};
		message.Attributes["SenderId"] = "AIDAEXAMPLE";
		message.Metadata["processor"] = "batch-worker";

		// Assert
		message.MessageId.ShouldBe("msg-42");
		message.Body.ShouldBe("{\"event\":\"test\"}");
		message.ReceiptHandle.ShouldBe("receipt-handle-abc");
		message.SourceQueueUrl.ShouldBe(queueUrl);
		message.AttemptCount.ShouldBe(5);
		message.FirstSentTimestamp.ShouldBe(now.AddHours(-1));
		message.MovedToDlqTimestamp.ShouldBe(now);
		message.LastError.ShouldBe("Deserialization failed");
		message.DlqReason.ShouldBe("Max retries exceeded");
		message.Attributes["SenderId"].ShouldBe("AIDAEXAMPLE");
		message.Metadata["processor"].ShouldBe("batch-worker");
	}
}
