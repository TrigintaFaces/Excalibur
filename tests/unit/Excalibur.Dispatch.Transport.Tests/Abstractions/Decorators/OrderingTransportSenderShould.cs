// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="OrderingTransportSender"/>.
/// Verifies ordering key is set in message properties before delegation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class OrderingTransportSenderShould
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();

	public OrderingTransportSenderShould()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 1, SuccessCount = 1 });
	}

	[Fact]
	public async Task Set_OrderingKey_On_Single_Send()
	{
		var sut = new OrderingTransportSender(_innerSender, _ => "partition-1");
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.OrderingKey);
		message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("partition-1");
		A.CallTo(() => _innerSender.SendAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Set_OrderingKey_On_All_Messages_In_Batch()
	{
		var sut = new OrderingTransportSender(_innerSender, msg => msg.Subject);
		var messages = new[]
		{
			new TransportMessage { Subject = "key-A", Body = new byte[] { 1 } },
			new TransportMessage { Subject = "key-B", Body = new byte[] { 2 } },
		};

		await sut.SendBatchAsync(messages, CancellationToken.None);

		messages[0].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("key-A");
		messages[1].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("key-B");
	}

	[Fact]
	public async Task Not_Set_OrderingKey_When_Selector_Returns_Null()
	{
		var sut = new OrderingTransportSender(_innerSender, _ => null);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldNotContainKey(TransportTelemetryConstants.PropertyKeys.OrderingKey);
	}

	[Fact]
	public void Throw_When_KeySelector_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new OrderingTransportSender(_innerSender, null!));
	}
}
