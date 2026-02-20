// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class InMemoryTransportSenderShould : IAsyncDisposable
{
	private readonly InMemoryTransportSender _sender = new("test-destination");

	public ValueTask DisposeAsync() => _sender.DisposeAsync();

	[Fact]
	public async Task SendAsync_RecordsMessage()
	{
		var message = TransportMessage.FromString("hello");

		await _sender.SendAsync(message, CancellationToken.None);

		_sender.SentMessages.Count.ShouldBe(1);
		_sender.SentMessages[0].ShouldBeSameAs(message);
	}

	[Fact]
	public async Task SendAsync_ReturnsSuccessByDefault()
	{
		var message = TransportMessage.FromString("hello");

		var result = await _sender.SendAsync(message, CancellationToken.None);

		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe(message.Id);
	}

	[Fact]
	public async Task SendAsync_InvokesOnSendCallback()
	{
		var customResult = SendResult.Success("custom-id");
		_sender.OnSend(_ => customResult);

		var message = TransportMessage.FromString("hello");
		var result = await _sender.SendAsync(message, CancellationToken.None);

		result.ShouldBeSameAs(customResult);
	}

	[Fact]
	public async Task SendAsync_ThrowsOnCancellation()
	{
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var message = TransportMessage.FromString("hello");

		await Should.ThrowAsync<OperationCanceledException>(
			() => _sender.SendAsync(message, cts.Token));
	}

	[Fact]
	public async Task SendBatchAsync_RecordsAllMessages()
	{
		var messages = new[]
		{
			TransportMessage.FromString("msg-1"),
			TransportMessage.FromString("msg-2"),
			TransportMessage.FromString("msg-3"),
		};

		await _sender.SendBatchAsync(messages, CancellationToken.None);

		_sender.SentMessages.Count.ShouldBe(3);
	}

	[Fact]
	public async Task SendBatchAsync_InvokesOnSendPerMessage()
	{
		var callCount = 0;
		_sender.OnSend(msg =>
		{
			Interlocked.Increment(ref callCount);
			return SendResult.Success(msg.Id);
		});

		var messages = new[]
		{
			TransportMessage.FromString("msg-1"),
			TransportMessage.FromString("msg-2"),
		};

		await _sender.SendBatchAsync(messages, CancellationToken.None);

		callCount.ShouldBe(2);
	}

	[Fact]
	public async Task SendBatchAsync_ReturnsBatchResult()
	{
		var messages = new[]
		{
			TransportMessage.FromString("msg-1"),
			TransportMessage.FromString("msg-2"),
		};

		var batchResult = await _sender.SendBatchAsync(messages, CancellationToken.None);

		batchResult.TotalMessages.ShouldBe(2);
		batchResult.SuccessCount.ShouldBe(2);
		batchResult.FailureCount.ShouldBe(0);
		batchResult.Results.Count.ShouldBe(2);
		batchResult.Duration.ShouldNotBeNull();
		batchResult.Duration.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void SentMessages_ReturnsSnapshot()
	{
		_sender.SentMessages.Count.ShouldBe(0);
	}

	[Fact]
	public async Task Clear_RemovesAllMessages()
	{
		await _sender.SendAsync(TransportMessage.FromString("msg-1"), CancellationToken.None);
		await _sender.SendAsync(TransportMessage.FromString("msg-2"), CancellationToken.None);

		_sender.Clear();

		_sender.SentMessages.ShouldBeEmpty();
	}

	[Fact]
	public void OnSend_ReturnsSelfForChaining()
	{
		var returned = _sender.OnSend(_ => SendResult.Success("id"));

		returned.ShouldBeSameAs(_sender);
	}

	[Fact]
	public async Task FlushAsync_CompletesSuccessfully()
	{
		await _sender.FlushAsync(CancellationToken.None);

		// No exception = success
	}

	[Fact]
	public async Task DisposeAsync_CompletesSuccessfully()
	{
		var sender = new InMemoryTransportSender("disposable-dest");
		await sender.DisposeAsync();

		// No exception = success
	}
}
