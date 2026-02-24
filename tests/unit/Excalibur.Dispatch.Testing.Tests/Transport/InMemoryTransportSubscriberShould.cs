// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class InMemoryTransportSubscriberShould : IAsyncDisposable
{
	private readonly InMemoryTransportSubscriber _subscriber = new("test-source");

	public ValueTask DisposeAsync() => _subscriber.DisposeAsync();

	[Fact]
	public async Task SubscribeAsync_SetsIsSubscribed()
	{
		using var cts = new CancellationTokenSource();
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		_subscriber.IsSubscribed.ShouldBeTrue();

		cts.Cancel();
		await subscribeTask;
	}

	[Fact]
	public async Task SubscribeAsync_WaitsUntilCancellation()
	{
		using var cts = new CancellationTokenSource();
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		subscribeTask.IsCompleted.ShouldBeFalse();

		cts.Cancel();
		await subscribeTask;

		subscribeTask.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public async Task PushAsync_InvokesHandler()
	{
		using var cts = new CancellationTokenSource();
		var handlerInvoked = false;
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) =>
			{
				handlerInvoked = true;
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		var msg = new TransportReceivedMessage { Id = "msg-1" };
		await _subscriber.PushAsync(msg, CancellationToken.None);

		handlerInvoked.ShouldBeTrue();

		cts.Cancel();
		await subscribeTask;
	}

	[Fact]
	public async Task PushAsync_RecordsProcessedMessage()
	{
		using var cts = new CancellationTokenSource();
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		var msg = new TransportReceivedMessage { Id = "msg-1" };
		var action = await _subscriber.PushAsync(msg, CancellationToken.None);

		action.ShouldBe(MessageAction.Reject);
		_subscriber.ProcessedMessages.Count.ShouldBe(1);
		_subscriber.ProcessedMessages[0].Message.ShouldBeSameAs(msg);
		_subscriber.ProcessedMessages[0].Action.ShouldBe(MessageAction.Reject);

		cts.Cancel();
		await subscribeTask;
	}

	[Fact]
	public async Task PushAsync_ThrowsWhenNotSubscribed()
	{
		var msg = new TransportReceivedMessage { Id = "msg-1" };

		await Should.ThrowAsync<InvalidOperationException>(
			() => _subscriber.PushAsync(msg, CancellationToken.None));
	}

	[Fact]
	public async Task PushAsync_PassesCancellationToken()
	{
		using var cts = new CancellationTokenSource();
		CancellationToken receivedToken = default;
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, ct) =>
			{
				receivedToken = ct;
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		using var pushCts = new CancellationTokenSource();
		var msg = new TransportReceivedMessage { Id = "msg-1" };
		await _subscriber.PushAsync(msg, pushCts.Token);

		receivedToken.ShouldBe(pushCts.Token);

		cts.Cancel();
		await subscribeTask;
	}

	[Fact]
	public async Task Clear_ResetsState()
	{
		using var cts = new CancellationTokenSource();
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		await _subscriber.PushAsync(new TransportReceivedMessage { Id = "msg-1" }, CancellationToken.None);

		_subscriber.Clear();

		_subscriber.IsSubscribed.ShouldBeFalse();
		_subscriber.ProcessedMessages.ShouldBeEmpty();

		cts.Cancel();
		await subscribeTask;
	}

	[Fact]
	public async Task DisposeAsync_ClearsHandler()
	{
		using var cts = new CancellationTokenSource();
		var subscribeTask = _subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
		_subscriber.IsSubscribed.ShouldBeTrue();

		await _subscriber.DisposeAsync();

		_subscriber.IsSubscribed.ShouldBeFalse();

		cts.Cancel();
		await subscribeTask;
	}
}
