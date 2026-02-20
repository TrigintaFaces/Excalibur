// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="DeduplicationTransportSender"/>.
/// Verifies deduplication ID is set in message properties before delegation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class DeduplicationTransportSenderShould
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();

	public DeduplicationTransportSenderShould()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 1, SuccessCount = 1 });
	}

	[Fact]
	public async Task Set_DeduplicationId_On_Single_Send()
	{
		var sut = new DeduplicationTransportSender(_innerSender, msg => msg.Id);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
		message.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe(message.Id);
	}

	[Fact]
	public async Task Set_DeduplicationId_On_All_Messages_In_Batch()
	{
		var sut = new DeduplicationTransportSender(_innerSender, msg => $"dedup-{msg.Id}");
		var messages = new[]
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
		};

		await sut.SendBatchAsync(messages, CancellationToken.None);

		messages[0].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe($"dedup-{messages[0].Id}");
		messages[1].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe($"dedup-{messages[1].Id}");
	}

	[Fact]
	public async Task Not_Set_DeduplicationId_When_Selector_Returns_Null()
	{
		var sut = new DeduplicationTransportSender(_innerSender, _ => null);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldNotContainKey(TransportTelemetryConstants.PropertyKeys.DeduplicationId);
	}

	[Fact]
	public void Throw_When_IdSelector_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new DeduplicationTransportSender(_innerSender, null!));
	}
}
