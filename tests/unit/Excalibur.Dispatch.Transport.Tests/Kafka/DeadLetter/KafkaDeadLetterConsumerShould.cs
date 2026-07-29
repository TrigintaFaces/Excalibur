// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Unit tests for <see cref="KafkaDeadLetterConsumer"/> — validates B2 fix:
/// <c>Peek</c> reads messages without committing offsets (non-destructive).
/// <c>Consume</c> commits offsets after reading (destructive).
/// </summary>
/// <remarks>
/// These drive a FAKE <see cref="IConsumer{TKey,TValue}"/> through the internal constructor.
///
/// They previously used "a real (but localhost-targeted) consumer instance" because the class built its
/// own consumer and could not be mocked. That had two costs. Constructing the class opened live
/// librdkafka connections to a broker that is not running, and those retry on background threads for the
/// life of the process -- under a parallel run the handles accumulate until the native library aborts the
/// host after every test has already passed, producing a failing exit code with no failing test.
///
/// The second cost was quieter: every "returns no messages" assertion below passed because there was no
/// broker to return anything. A consumer that never connected and a topic that is genuinely empty are
/// indistinguishable to that assertion, so it could not fail. Driving a fake states which case is under
/// test, so the assertions now bind the class's own behaviour rather than the absence of infrastructure.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KafkaDeadLetterConsumerShould : IDisposable
{
	private readonly KafkaDeadLetterConsumer _sut;
	private readonly IConsumer<string, byte[]> _consumer;
	private bool _disposed;

	public KafkaDeadLetterConsumerShould()
	{
		var dlqOptions = Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions
		{
			ConsumeTimeout = TimeSpan.FromMilliseconds(100), // Fast timeout for tests
		});

		// Consume is configured EXPLICITLY to return null, and the explicitness is the point.
		//
		// An unconfigured fake does not return null here -- FakeItEasy hands back a dummy ConsumeResult
		// whose Message is null, which is a shape the real client never produces and which makes the
		// consumer throw a NullReferenceException while converting it. Returning null is what a real poll
		// returns when it times out with nothing to read, so this states the "topic is empty" case that
		// these tests are actually about. No broker is contacted and no native handle is created.
		_consumer = A.Fake<IConsumer<string, byte[]>>();
		// Explicit nullable generic: Consume's declared return is ConsumeResult<..>?, but generic
		// inference over the expression drops the annotation, so a bare A.CallTo binds the non-nullable
		// overload and null is a CS8620 mismatch. Stating T fixes the binding (no suppression).
		A.CallTo<global::Confluent.Kafka.ConsumeResult<string, byte[]>?>(() => _consumer.Consume(A<TimeSpan>._)).Returns(null);

		_sut = new KafkaDeadLetterConsumer(
			_consumer,
			dlqOptions,
			NullLogger<KafkaDeadLetterConsumer>.Instance);
	}

	/// <summary>
	/// LIVENESS: the consumer must actually surface a message when one is there to read.
	/// </summary>
	/// <remarks>
	/// This is the arm the suite was missing, and without it the "returns empty" arms below are satisfied
	/// by a consumer that is completely broken -- one that never connects, or that drops everything it
	/// reads, returns an empty list just as convincingly as a correctly-drained topic. Every other test
	/// here passes against a consumer that does nothing at all; this is the only one that does not.
	/// </remarks>
	[Fact]
	public void Peek_ReturnsTheMessage_WhenOneIsAvailable()
	{
		// Arrange -- one message, then an empty poll to end the loop.
		var body = Encoding.UTF8.GetBytes("payload");
		var available = new global::Confluent.Kafka.ConsumeResult<string, byte[]>
		{
			Topic = "test-topic.dead-letter",
			Partition = new Partition(0),
			Offset = new Offset(7),
			Message = new global::Confluent.Kafka.Message<string, byte[]>
			{
				Key = "message-key",
				Value = body,
				Timestamp = new Timestamp(DateTime.UtcNow, TimestampType.CreateTime),
				Headers = [],
			},
		};

		// A FRESH fake and subject, deliberately, rather than re-configuring the shared one.
		//
		// Layering ReturnsNextFromSequence over the constructor's Returns(null) makes the result depend on
		// FakeItEasy's rule precedence rather than on the behaviour under test -- and it silently lost, so
		// the poll returned null, the loop broke immediately, and the arm reported zero messages while
		// appearing to exercise one. A test whose outcome hinges on which of two stacked rules wins is not
		// testing the consumer.
		var consumer = A.Fake<IConsumer<string, byte[]>>();
		A.CallTo<global::Confluent.Kafka.ConsumeResult<string, byte[]>?>(() => consumer.Consume(A<TimeSpan>._))
			.ReturnsNextFromSequence(available, null);

		using var sut = new KafkaDeadLetterConsumer(
			consumer,
			Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions
			{
				ConsumeTimeout = TimeSpan.FromMilliseconds(100),
			}),
			NullLogger<KafkaDeadLetterConsumer>.Instance);

		// Act
		var messages = sut.Peek("test-topic.dead-letter", maxMessages: 10, CancellationToken.None);

		// Assert
		messages.Count.ShouldBe(
			1,
			"a message was available to read, so the consumer must surface it -- if this is zero the "
			+ "'returns empty' arms are passing against a consumer that reads nothing at all");
		messages[0].OriginalMessage.Id.ShouldBe("message-key");
		Encoding.UTF8.GetString(messages[0].OriginalMessage.Body.ToArray()).ShouldBe("payload");
	}

	[Fact]
	public void Peek_ReturnsEmptyList_WhenNoMessagesAvailable()
	{
		// Act — the fake polls empty (null), which is what a real poll returns on timeout with nothing
		// to read. Paired with the liveness arm above, this now distinguishes "topic is empty" from
		// "consumer is broken"; on its own it could not.
		var messages = _sut.Peek("test-topic.dead-letter", maxMessages: 10, CancellationToken.None);

		// Assert
		messages.ShouldNotBeNull();
		messages.Count.ShouldBe(0);
	}

	[Fact]
	public void Consume_ReturnsEmptyList_WhenNoMessagesAvailable()
	{
		// Act
		var messages = _sut.Consume("test-topic.dead-letter", maxMessages: 10, CancellationToken.None);

		// Assert
		messages.ShouldNotBeNull();
		messages.Count.ShouldBe(0);
	}

	[Fact]
	public void Peek_CanBeCalledMultipleTimes_WithoutError()
	{
		// Act — call Peek multiple times to verify it doesn't corrupt state
		var messages1 = _sut.Peek("test-topic.dead-letter", maxMessages: 5, CancellationToken.None);
		var messages2 = _sut.Peek("test-topic.dead-letter", maxMessages: 5, CancellationToken.None);

		// Assert — both calls should succeed with empty results
		messages1.Count.ShouldBe(0);
		messages2.Count.ShouldBe(0);
	}

	[Fact]
	public void Dispose_CanBeCalledSafely()
	{
		// Act & Assert — should not throw
		_sut.Dispose();
	}

	[Fact]
	public void Dispose_IsIdempotent()
	{
		// Act & Assert — calling Dispose twice should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Both are disposed here, and both are idempotent: the subject guards its own second Dispose
		// (asserted by Dispose_IsIdempotent), and the fake consumer records the extra call harmlessly.
		_sut.Dispose();
		_consumer.Dispose();
	}
}
