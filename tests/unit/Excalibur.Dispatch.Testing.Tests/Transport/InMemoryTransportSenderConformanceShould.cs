// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

/// <summary>
/// Concrete xUnit conformance tests for <see cref="InMemoryTransportSender"/>.
/// Demonstrates the conformance test kit pattern using the InMemory transport.
/// </summary>
[UnitTest]
public sealed class InMemoryTransportSenderConformanceShould : TransportSenderConformanceTests, IDisposable
{
	private const string Destination = "conformance-test-dest";

	private InMemoryTransportReceiver? _receiver;

	public void Dispose()
	{
		// InMemoryTransportReceiver.DisposeAsync is synchronous (returns ValueTask.CompletedTask)
		_receiver?.DisposeAsync().AsTask().GetAwaiter().GetResult();
		_receiver = null;
	}

	protected override Task<ITransportSender> CreateSenderAsync()
	{
		_receiver = new InMemoryTransportReceiver(Destination);
		var sender = new InMemoryTransportSender(Destination);

		// Wire the sender to enqueue into the receiver for round-trip tests
		sender.OnSend(msg =>
		{
			_receiver.Enqueue(new TransportReceivedMessage
			{
				Id = msg.Id,
				Body = msg.Body,
				ContentType = msg.ContentType,
				MessageType = msg.MessageType,
				CorrelationId = msg.CorrelationId,
				Subject = msg.Subject,
				EnqueuedAt = DateTimeOffset.UtcNow,
			});
			return SendResult.Success(msg.Id);
		});

		return Task.FromResult<ITransportSender>(sender);
	}

	protected override Task<ITransportReceiver?> CreatePairedReceiverAsync() =>
		Task.FromResult<ITransportReceiver?>(_receiver);

	[Fact]
	public Task Destination_IsNotEmpty() => VerifyDestinationIsNotEmpty();

	[Fact]
	public Task SendAsync_DeliversMessage() => VerifySendDeliversMessage();

	[Fact]
	public Task SendAsync_PreservesMessageId() => VerifySendPreservesMessageId();

	[Fact]
	public Task SendAsync_ThrowsOnNullMessage() => VerifySendThrowsOnNullMessage();

	[Fact]
	public Task SendAsync_RespectsCancellation() => VerifySendRespectsCancellation();

	[Fact]
	public Task SendBatchAsync_DeliversAllMessages() => VerifySendBatchDeliversAllMessages();

	[Fact]
	public Task SendBatchAsync_ReturnsPerMessageResults() => VerifySendBatchReturnsPerMessageResults();

	[Fact]
	public Task SendBatchAsync_HandlesEmptyBatch() => VerifySendBatchHandlesEmptyBatch();

	[Fact]
	public Task FlushAsync_CompletesSuccessfully() => VerifyFlushCompletesSuccessfully();

	[Fact]
	public Task GetService_ReturnsNullForUnknownType() => VerifyGetServiceReturnsNullForUnknownType();

	[Fact]
	public Task DisposeAsync_IsIdempotent() => VerifyDisposeAsyncIsIdempotent();

	[Fact]
	public Task SendReceive_RoundTrip() => VerifySendReceiveRoundTrip();
}
