// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using PubSubBatchProcessingResult = Excalibur.Dispatch.Transport.Google.BatchProcessingResult;
using PubSubMessageBatch = Excalibur.Dispatch.Transport.Google.MessageBatch;
using PubSubProcessedMessage = Excalibur.Dispatch.Transport.Google.ProcessedMessage;
using PubSubFailedMessage = Excalibur.Dispatch.Transport.Google.FailedMessage;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class BatchProcessingResultShould
{
	private static PubSubMessageBatch CreateBatch(int count)
	{
		var messages = Enumerable.Range(0, count)
			.Select(i => new ReceivedMessage
			{
				AckId = $"ack-{i}",
				Message = new PubsubMessage
				{
					MessageId = $"msg-{i}",
					Data = ByteString.CopyFromUtf8($"data-{i}"),
				},
			})
			.ToList();

		return new PubSubMessageBatch(messages, "projects/p/subscriptions/s", count * 100L);
	}

	[Fact]
	public void CreateWithAllProperties()
	{
		// Arrange
		var batch = CreateBatch(3);
		var successes = new List<PubSubProcessedMessage>
		{
			new("msg-0", "ack-0", new object(), TimeSpan.FromMilliseconds(10)),
			new("msg-1", "ack-1", new object(), TimeSpan.FromMilliseconds(20)),
		};
		var failures = new List<PubSubFailedMessage>
		{
			new("msg-2", "ack-2", new InvalidOperationException("err")),
		};
		var duration = TimeSpan.FromMilliseconds(100);

		// Act
		var result = new PubSubBatchProcessingResult(batch, successes, failures, duration);

		// Assert
		result.Batch.ShouldBeSameAs(batch);
		result.SuccessfulMessages.Count.ShouldBe(2);
		result.FailedMessages.Count.ShouldBe(1);
		result.ProcessingDuration.ShouldBe(duration);
	}

	[Fact]
	public void BeFullySuccessfulWhenNoFailures()
	{
		// Arrange
		var batch = CreateBatch(2);
		var successes = new List<PubSubProcessedMessage>
		{
			new("msg-0", "ack-0", new object(), TimeSpan.FromMilliseconds(10)),
			new("msg-1", "ack-1", new object(), TimeSpan.FromMilliseconds(20)),
		};

		// Act
		var result = new PubSubBatchProcessingResult(batch, successes, new List<PubSubFailedMessage>(), TimeSpan.FromMilliseconds(50));

		// Assert
		result.IsFullySuccessful.ShouldBeTrue();
	}

	[Fact]
	public void NotBeFullySuccessfulWhenHasFailures()
	{
		// Arrange
		var batch = CreateBatch(2);
		var failures = new List<PubSubFailedMessage>
		{
			new("msg-1", "ack-1", new InvalidOperationException("err")),
		};

		// Act
		var result = new PubSubBatchProcessingResult(batch, new List<PubSubProcessedMessage>(), failures, TimeSpan.FromMilliseconds(50));

		// Assert
		result.IsFullySuccessful.ShouldBeFalse();
	}

	[Fact]
	public void CalculateSuccessRateCorrectly()
	{
		// Arrange
		var batch = CreateBatch(4);
		var successes = new List<PubSubProcessedMessage>
		{
			new("msg-0", "ack-0", new object(), TimeSpan.FromMilliseconds(10)),
			new("msg-1", "ack-1", new object(), TimeSpan.FromMilliseconds(10)),
			new("msg-2", "ack-2", new object(), TimeSpan.FromMilliseconds(10)),
		};
		var failures = new List<PubSubFailedMessage>
		{
			new("msg-3", "ack-3", new InvalidOperationException("err")),
		};

		// Act
		var result = new PubSubBatchProcessingResult(batch, successes, failures, TimeSpan.FromMilliseconds(100));

		// Assert
		result.SuccessRate.ShouldBe(0.75);
	}

	[Fact]
	public void ThrowWhenBatchIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubBatchProcessingResult(null!, new List<PubSubProcessedMessage>(), new List<PubSubFailedMessage>(), TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenSuccessfulMessagesIsNull()
	{
		var batch = CreateBatch(1);
		Should.Throw<ArgumentNullException>(() =>
			new PubSubBatchProcessingResult(batch, null!, new List<PubSubFailedMessage>(), TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenFailedMessagesIsNull()
	{
		var batch = CreateBatch(1);
		Should.Throw<ArgumentNullException>(() =>
			new PubSubBatchProcessingResult(batch, new List<PubSubProcessedMessage>(), null!, TimeSpan.Zero));
	}
}
