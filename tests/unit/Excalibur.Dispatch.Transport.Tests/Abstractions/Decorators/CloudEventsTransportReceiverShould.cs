// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Decorators;

/// <summary>
/// Tests for <see cref="CloudEventsTransportReceiver"/>.
/// Verifies CloudEvent detection, unwrapping, and pass-through behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudEventsTransportReceiverShould
{
	private readonly ITransportReceiver _innerReceiver = A.Fake<ITransportReceiver>();
	private readonly ICloudEventMapper<TransportReceivedMessage> _mapper = A.Fake<ICloudEventMapper<TransportReceivedMessage>>();

	public CloudEventsTransportReceiverShould()
	{
		A.CallTo(() => _innerReceiver.Source).Returns("test-queue");
	}

	[Fact]
	public async Task Unwrap_Message_When_CE_Detected_And_Unwrapper_Present()
	{
		var originalMessage = CreateTestMessage("original");
		var unwrappedMessage = CreateTestMessage("unwrapped");

		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { originalMessage });
		A.CallTo(() => _mapper.TryDetectModeAsync(originalMessage, A<CancellationToken>._))
			.Returns(new ValueTask<CloudEventMode?>(CloudEventMode.Structured));

		var sut = new CloudEventsTransportReceiver(_innerReceiver, _mapper, _ => unwrappedMessage);
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].ShouldBeSameAs(unwrappedMessage);
	}

	[Fact]
	public async Task Pass_Through_When_CE_Not_Detected()
	{
		var originalMessage = CreateTestMessage("original");

		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { originalMessage });
		A.CallTo(() => _mapper.TryDetectModeAsync(originalMessage, A<CancellationToken>._))
			.Returns(new ValueTask<CloudEventMode?>((CloudEventMode?)null));

		var sut = new CloudEventsTransportReceiver(_innerReceiver, _mapper, _ => CreateTestMessage("should-not-appear"));
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].ShouldBeSameAs(originalMessage);
	}

	[Fact]
	public async Task Pass_Through_When_CE_Detected_But_No_Unwrapper()
	{
		var originalMessage = CreateTestMessage("original");

		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage> { originalMessage });
		A.CallTo(() => _mapper.TryDetectModeAsync(originalMessage, A<CancellationToken>._))
			.Returns(new ValueTask<CloudEventMode?>(CloudEventMode.Binary));

		var sut = new CloudEventsTransportReceiver(_innerReceiver, _mapper, unwrapper: null);
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.Count.ShouldBe(1);
		result[0].ShouldBeSameAs(originalMessage);
	}

	[Fact]
	public async Task Return_Empty_List_When_No_Messages_Received()
	{
		A.CallTo(() => _innerReceiver.ReceiveAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<TransportReceivedMessage>());

		var sut = new CloudEventsTransportReceiver(_innerReceiver, _mapper);
		var result = await sut.ReceiveAsync(10, CancellationToken.None);

		result.Count.ShouldBe(0);
		A.CallTo(() => _mapper.TryDetectModeAsync(A<TransportReceivedMessage>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void Throw_When_Mapper_Is_Null()
	{
		Should.Throw<ArgumentNullException>(
			() => new CloudEventsTransportReceiver(_innerReceiver, null!));
	}

	[Fact]
	public async Task Pass_Through_AcknowledgeAsync_And_RejectAsync_Unchanged()
	{
		var testMessage = CreateTestMessage("ack-test");
		var sut = new CloudEventsTransportReceiver(_innerReceiver, _mapper);

		await sut.AcknowledgeAsync(testMessage, CancellationToken.None);

		A.CallTo(() => _innerReceiver.AcknowledgeAsync(testMessage, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		await sut.RejectAsync(testMessage, "reason", false, CancellationToken.None);

		A.CallTo(() => _innerReceiver.RejectAsync(testMessage, "reason", false, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static TransportReceivedMessage CreateTestMessage(string id) =>
		new()
		{
			Id = id,
			Body = "ce-test"u8.ToArray(),
			Source = "test-queue",
		};
}
