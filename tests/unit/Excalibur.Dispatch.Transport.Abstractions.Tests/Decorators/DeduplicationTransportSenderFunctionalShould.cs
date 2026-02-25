// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

/// <summary>
/// Functional tests for <see cref="DeduplicationTransportSender"/> verifying
/// deduplication ID assignment on single and batch messages.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeduplicationTransportSenderFunctionalShould
{
	[Fact]
	public async Task Set_deduplication_id_on_single_message()
	{
		var inner = A.Fake<ITransportSender>();
		TransportMessage? captured = null;
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage msg, CancellationToken _) => captured = msg)
			.Returns(new SendResult { IsSuccess = true });

		var sut = new DeduplicationTransportSender(inner, msg => $"dedup-{msg.Id}");

		var message = new TransportMessage
		{
			Id = "msg-123",
			Subject = "test-queue",
			Body = new byte[] { 1, 2, 3 },
		};

		await sut.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
		captured.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("dedup-msg-123");
	}

	[Fact]
	public async Task Set_deduplication_id_on_batch_messages()
	{
		var inner = A.Fake<ITransportSender>();
		IReadOnlyList<TransportMessage>? capturedBatch = null;
		A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Invokes((IReadOnlyList<TransportMessage> msgs, CancellationToken _) => capturedBatch = msgs)
			.Returns(new BatchSendResult { SuccessCount = 2, TotalMessages = 2 });

		var sut = new DeduplicationTransportSender(inner, msg => $"batch-{msg.Id}");

		var messages = new List<TransportMessage>
		{
			new() { Id = "a", Subject = "q", Body = new byte[] { 1 } },
			new() { Id = "b", Subject = "q", Body = new byte[] { 2 } },
		};

		await sut.SendBatchAsync(messages, CancellationToken.None);

		capturedBatch.ShouldNotBeNull();
		foreach (var msg in capturedBatch!)
		{
			msg.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
		}

		capturedBatch[0].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("batch-a");
		capturedBatch[1].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("batch-b");
	}

	[Fact]
	public async Task Not_set_deduplication_id_when_selector_returns_null()
	{
		var inner = A.Fake<ITransportSender>();
		TransportMessage? captured = null;
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage msg, CancellationToken _) => captured = msg)
			.Returns(new SendResult { IsSuccess = true });

		var sut = new DeduplicationTransportSender(inner, _ => null);

		var message = new TransportMessage
		{
			Id = "msg-456",
			Subject = "test-queue",
			Body = new byte[] { 1, 2, 3 },
		};

		await sut.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured!.Properties.ShouldNotContainKey(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
	}

	[Fact]
	public void Throw_for_null_id_selector()
	{
		var inner = A.Fake<ITransportSender>();

		Should.Throw<ArgumentNullException>(() =>
			new DeduplicationTransportSender(inner, null!));
	}

	[Fact]
	public async Task Delegate_to_inner_sender()
	{
		var inner = A.Fake<ITransportSender>();
		var expectedResult = new SendResult { IsSuccess = true, MessageId = "sent-1" };
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(expectedResult);

		var sut = new DeduplicationTransportSender(inner, msg => msg.Id);

		var result = await sut.SendAsync(
			new TransportMessage { Id = "x", Subject = "q", Body = new byte[] { 0 } },
			CancellationToken.None);

		result.ShouldBeSameAs(expectedResult);
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
