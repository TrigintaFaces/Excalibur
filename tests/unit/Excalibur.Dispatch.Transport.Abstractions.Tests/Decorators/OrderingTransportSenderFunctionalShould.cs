// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

/// <summary>
/// Functional tests for <see cref="OrderingTransportSender"/> verifying
/// ordering key application on single and batch messages.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrderingTransportSenderFunctionalShould
{
	[Fact]
	public async Task Apply_ordering_key_to_single_message()
	{
		var inner = A.Fake<ITransportSender>();
		TransportMessage? captured = null;
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage msg, CancellationToken _) => captured = msg)
			.Returns(new SendResult { IsSuccess = true });

		var sut = new OrderingTransportSender(inner, msg => $"key-{msg.Id}");

		var message = new TransportMessage
		{
			Id = "msg-1",
			Subject ="queue",
			Body = new byte[] { 1 },
		};

		await sut.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.OrderingKey);
		captured.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("key-msg-1");
	}

	[Fact]
	public async Task Apply_ordering_key_to_all_batch_messages()
	{
		var inner = A.Fake<ITransportSender>();
		IReadOnlyList<TransportMessage>? capturedMessages = null;
		A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Invokes((IReadOnlyList<TransportMessage> msgs, CancellationToken _) => capturedMessages = msgs)
			.Returns(new BatchSendResult { SuccessCount = 3, TotalMessages = 3 });

		var sut = new OrderingTransportSender(inner, msg => msg.Subject);

		var messages = new List<TransportMessage>
		{
			new() { Id = "a", Subject ="orders", Body = new byte[] { 1 } },
			new() { Id = "b", Subject ="orders", Body = new byte[] { 2 } },
			new() { Id = "c", Subject ="payments", Body = new byte[] { 3 } },
		};

		await sut.SendBatchAsync(messages, CancellationToken.None);

		capturedMessages.ShouldNotBeNull();
		capturedMessages.Count.ShouldBe(3);
		foreach (var msg in capturedMessages)
		{
			msg.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.OrderingKey);
		}

		capturedMessages[0].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("orders");
		capturedMessages[2].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("payments");
	}

	[Fact]
	public async Task Skip_ordering_key_when_selector_returns_null()
	{
		var inner = A.Fake<ITransportSender>();
		TransportMessage? captured = null;
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage msg, CancellationToken _) => captured = msg)
			.Returns(new SendResult { IsSuccess = true });

		var sut = new OrderingTransportSender(inner, _ => null);

		var message = new TransportMessage
		{
			Id = "msg-1",
			Subject ="queue",
			Body = new byte[] { 1 },
		};

		await sut.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured.Properties.ShouldNotContainKey(TransportTelemetryConstants.PropertyKeys.OrderingKey);
	}

	[Fact]
	public void Throw_for_null_key_selector()
	{
		var inner = A.Fake<ITransportSender>();

		Should.Throw<ArgumentNullException>(
			() => new OrderingTransportSender(inner, null!));
	}

	[Fact]
	public async Task Delegate_to_inner_sender()
	{
		var inner = A.Fake<ITransportSender>();
		var expectedResult = new SendResult { IsSuccess = true, MessageId = "result-1" };
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(expectedResult);

		var sut = new OrderingTransportSender(inner, _ => "key");

		var result = await sut.SendAsync(
			new TransportMessage { Id = "x", Subject ="q", Body = new byte[] { 0 } },
			CancellationToken.None);

		result.ShouldBeSameAs(expectedResult);
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Use_message_id_as_ordering_key()
	{
		var inner = A.Fake<ITransportSender>();
		TransportMessage? captured = null;
		A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Invokes((TransportMessage msg, CancellationToken _) => captured = msg)
			.Returns(new SendResult { IsSuccess = true });

		// Common pattern: use MessageId as ordering key for FIFO
		var sut = new OrderingTransportSender(inner, msg => msg.Id);

		var message = new TransportMessage
		{
			Id = "order-123",
			Subject ="q",
			Body = new byte[] { 1 },
		};

		await sut.SendAsync(message, CancellationToken.None);

		captured.ShouldNotBeNull();
		captured.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("order-123");
	}
}
