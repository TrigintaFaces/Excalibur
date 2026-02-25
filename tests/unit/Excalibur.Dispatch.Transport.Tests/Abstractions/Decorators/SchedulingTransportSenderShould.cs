// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="SchedulingTransportSender"/>.
/// Verifies scheduled time is set as ISO 8601 string in message properties before delegation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public class SchedulingTransportSenderShould
{
	private readonly ITransportSender _innerSender = A.Fake<ITransportSender>();

	public SchedulingTransportSenderShould()
	{
		A.CallTo(() => _innerSender.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
			.Returns(SendResult.Success("msg-1"));
		A.CallTo(() => _innerSender.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
			.Returns(new BatchSendResult { TotalMessages = 1, SuccessCount = 1 });
	}

	[Fact]
	public async Task Set_ScheduledTime_On_Single_Send()
	{
		var scheduledTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
		var sut = new SchedulingTransportSender(_innerSender, _ => scheduledTime);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.ScheduledTime);
		var storedValue = message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime].ToString();
		DateTimeOffset.Parse(storedValue).ShouldBe(scheduledTime);
	}

	[Fact]
	public async Task Set_ScheduledTime_On_All_Messages_In_Batch()
	{
		var scheduledTime = new DateTimeOffset(2026, 6, 15, 8, 30, 0, TimeSpan.Zero);
		var sut = new SchedulingTransportSender(_innerSender, _ => scheduledTime);
		var messages = new[]
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
		};

		await sut.SendBatchAsync(messages, CancellationToken.None);

		foreach (var msg in messages)
		{
			msg.Properties.ShouldContainKey(TransportTelemetryConstants.PropertyKeys.ScheduledTime);
		}
	}

	[Fact]
	public async Task Not_Set_ScheduledTime_When_Selector_Returns_Null()
	{
		var sut = new SchedulingTransportSender(_innerSender, _ => null);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		message.Properties.ShouldNotContainKey(TransportTelemetryConstants.PropertyKeys.ScheduledTime);
	}

	[Fact]
	public void Throw_When_TimeSelector_Is_Null()
	{
		Should.Throw<ArgumentNullException>(() => new SchedulingTransportSender(_innerSender, null!));
	}

	[Fact]
	public async Task Store_ScheduledTime_As_ISO8601_RoundTrip_Format()
	{
		var scheduledTime = new DateTimeOffset(2026, 12, 25, 18, 0, 0, TimeSpan.FromHours(5));
		var sut = new SchedulingTransportSender(_innerSender, _ => scheduledTime);
		var message = TransportMessage.FromString("hello");

		await sut.SendAsync(message, CancellationToken.None);

		var storedValue = message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime].ToString();
		var parsed = DateTimeOffset.Parse(storedValue);
		parsed.ShouldBe(scheduledTime);
	}
}
