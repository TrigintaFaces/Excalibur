// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class InMemoryTransportReceiverShould : IAsyncDisposable
{
	private readonly InMemoryTransportReceiver _receiver = new("test-source");

	public ValueTask DisposeAsync() => _receiver.DisposeAsync();

	[Fact]
	public async Task ReceiveAsync_ReturnsEnqueuedMessages()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };
		_receiver.Enqueue(msg);

		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);

		received.Count.ShouldBe(1);
		received[0].ShouldBeSameAs(msg);
	}

	[Fact]
	public async Task ReceiveAsync_RespectsMaxMessages()
	{
		_receiver.Enqueue(
			new TransportReceivedMessage { Id = "msg-1" },
			new TransportReceivedMessage { Id = "msg-2" },
			new TransportReceivedMessage { Id = "msg-3" });

		var received = await _receiver.ReceiveAsync(2, CancellationToken.None);

		received.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReceiveAsync_ReturnsEmptyWhenNoMessages()
	{
		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);

		received.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReceiveAsync_ThrowsOnCancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await Should.ThrowAsync<OperationCanceledException>(
			() => _receiver.ReceiveAsync(10, cts.Token));
	}

	[Fact]
	public async Task AcknowledgeAsync_RecordsMessage()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		await _receiver.AcknowledgeAsync(msg, CancellationToken.None);

		_receiver.AcknowledgedMessages.Count.ShouldBe(1);
		_receiver.AcknowledgedMessages[0].ShouldBeSameAs(msg);
	}

	[Fact]
	public async Task AcknowledgeAsync_ThrowsOnCancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		await Should.ThrowAsync<OperationCanceledException>(
			() => _receiver.AcknowledgeAsync(msg, cts.Token));
	}

	[Fact]
	public async Task RejectAsync_RecordsMessageWithReasonAndRequeue()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		await _receiver.RejectAsync(msg, "bad format", true, CancellationToken.None);

		_receiver.RejectedMessages.Count.ShouldBe(1);
		_receiver.RejectedMessages[0].Message.ShouldBeSameAs(msg);
		_receiver.RejectedMessages[0].Reason.ShouldBe("bad format");
		_receiver.RejectedMessages[0].Requeue.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectedMessages_ContainsReasonAndRequeueFlag()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		await _receiver.RejectAsync(msg, null, false, CancellationToken.None);

		_receiver.RejectedMessages.Count.ShouldBe(1);
		_receiver.RejectedMessages[0].Reason.ShouldBeNull();
		_receiver.RejectedMessages[0].Requeue.ShouldBeFalse();
	}

	[Fact]
	public async Task Enqueue_AddsSingleMessage()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		_receiver.Enqueue(msg);

		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);
		received.Count.ShouldBe(1);
	}

	[Fact]
	public async Task EnqueueParams_AddsMultipleMessages()
	{
		_receiver.Enqueue(
			new TransportReceivedMessage { Id = "msg-1" },
			new TransportReceivedMessage { Id = "msg-2" });

		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);

		received.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Clear_RemovesAllState()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };
		_receiver.Enqueue(msg);
		await _receiver.AcknowledgeAsync(msg, CancellationToken.None);
		await _receiver.RejectAsync(new TransportReceivedMessage { Id = "msg-2" }, "err", false, CancellationToken.None);

		_receiver.Clear();

		var received = await _receiver.ReceiveAsync(10, CancellationToken.None);
		received.ShouldBeEmpty();
		_receiver.AcknowledgedMessages.ShouldBeEmpty();
		_receiver.RejectedMessages.ShouldBeEmpty();
	}

	[Fact]
	public async Task DisposeAsync_CompletesSuccessfully()
	{
		var receiver = new InMemoryTransportReceiver("disposable-source");
		await receiver.DisposeAsync();

		// No exception = success
	}
}
