// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// Abstract behavioral conformance tests for <see cref="ITransportSubscriber"/> implementations.
/// Verifies that any push-based transport subscriber correctly implements the contract defined in ADR-116.
/// </summary>
/// <remarks>
/// <para>
/// To use, inherit this class and implement <see cref="CreateSubscriberAsync"/> and
/// <see cref="PushMessageToSubscriberAsync"/> to provide a subscriber and deliver test messages.
/// </para>
/// </remarks>
public abstract class TransportSubscriberConformanceTests
{
	/// <summary>
	/// Creates a new <see cref="ITransportSubscriber"/> instance for testing.
	/// </summary>
	/// <returns>A subscriber instance configured for testing.</returns>
	protected abstract Task<ITransportSubscriber> CreateSubscriberAsync();

	/// <summary>
	/// Pushes a message to the subscriber for delivery to the handler.
	/// For InMemory transports, this calls <c>PushAsync</c>.
	/// For real transports, this sends a message via a sender to the subscribed topic.
	/// </summary>
	/// <param name="subscriber">The subscriber to push a message to.</param>
	/// <param name="message">The message to deliver.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The <see cref="MessageAction"/> returned by the handler.</returns>
	protected abstract Task<MessageAction> PushMessageToSubscriberAsync(
		ITransportSubscriber subscriber,
		TransportReceivedMessage message,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a test <see cref="TransportReceivedMessage"/> with the given body.
	/// </summary>
	protected static TransportReceivedMessage CreateTestMessage(string body, string? id = null) =>
		new()
		{
			Id = id ?? Guid.NewGuid().ToString(),
			Body = Encoding.UTF8.GetBytes(body),
			ContentType = "text/plain",
			EnqueuedAt = DateTimeOffset.UtcNow,
		};

	/// <summary>
	/// Verifies that <see cref="ITransportSubscriber.Source"/> returns a non-empty string.
	/// </summary>
	protected async Task VerifySourceIsNotEmpty()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(subscriber.Source))
		{
			throw new InvalidOperationException(
				"ITransportSubscriber.Source must return a non-empty string.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSubscriber.SubscribeAsync"/> accepts a handler
	/// and stops when the cancellation token is cancelled.
	/// </summary>
	protected async Task VerifySubscribeStartsAndStops()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();

		var subscribeTask = subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		// Give the subscription a moment to start
		await Task.Delay(50).ConfigureAwait(false);

		await cts.CancelAsync().ConfigureAwait(false);

		// Subscribe should complete after cancellation
		await subscribeTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSubscriber.SubscribeAsync"/> throws on null handler.
	/// </summary>
	protected async Task VerifySubscribeThrowsOnNullHandler()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);

		try
		{
			await subscriber.SubscribeAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new InvalidOperationException(
				"SubscribeAsync should throw ArgumentNullException for null handler.");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that the handler receives a pushed message and the Acknowledge action works.
	/// </summary>
	protected async Task VerifyHandlerReceivesMessageAndAcknowledges()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();

		TransportReceivedMessage? handlerMessage = null;

		var subscribeTask = subscriber.SubscribeAsync(
			(msg, _) =>
			{
				handlerMessage = msg;
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		// Give subscription time to register
		await Task.Delay(50).ConfigureAwait(false);

		var testMsg = CreateTestMessage("ack-payload");
		var action = await PushMessageToSubscriberAsync(subscriber, testMsg, CancellationToken.None)
			.ConfigureAwait(false);

		if (action != MessageAction.Acknowledge)
		{
			throw new InvalidOperationException(
				$"Handler should return Acknowledge, got {action}.");
		}

		if (handlerMessage is null)
		{
			throw new InvalidOperationException("Handler was not invoked.");
		}

		var body = Encoding.UTF8.GetString(handlerMessage.Body.Span);

		if (body != "ack-payload")
		{
			throw new InvalidOperationException(
				$"Handler message body should be 'ack-payload', got '{body}'.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await subscribeTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that the handler can return Reject action.
	/// </summary>
	protected async Task VerifyHandlerCanRejectMessage()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();

		var subscribeTask = subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Reject),
			cts.Token);

		await Task.Delay(50).ConfigureAwait(false);

		var action = await PushMessageToSubscriberAsync(
			subscriber, CreateTestMessage("reject-payload"), CancellationToken.None)
			.ConfigureAwait(false);

		if (action != MessageAction.Reject)
		{
			throw new InvalidOperationException(
				$"Handler should return Reject, got {action}.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await subscribeTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that the handler can return Requeue action.
	/// </summary>
	protected async Task VerifyHandlerCanRequeueMessage()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();

		var subscribeTask = subscriber.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Requeue),
			cts.Token);

		await Task.Delay(50).ConfigureAwait(false);

		var action = await PushMessageToSubscriberAsync(
			subscriber, CreateTestMessage("requeue-payload"), CancellationToken.None)
			.ConfigureAwait(false);

		if (action != MessageAction.Requeue)
		{
			throw new InvalidOperationException(
				$"Handler should return Requeue, got {action}.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await subscribeTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that multiple messages can be delivered sequentially to the handler.
	/// </summary>
	protected async Task VerifyHandlerReceivesMultipleMessages()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);
		using var cts = new CancellationTokenSource();

		var receivedCount = 0;

		var subscribeTask = subscriber.SubscribeAsync(
			(_, _) =>
			{
				Interlocked.Increment(ref receivedCount);
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		await Task.Delay(50).ConfigureAwait(false);

		await PushMessageToSubscriberAsync(
			subscriber, CreateTestMessage("multi-1"), CancellationToken.None).ConfigureAwait(false);
		await PushMessageToSubscriberAsync(
			subscriber, CreateTestMessage("multi-2"), CancellationToken.None).ConfigureAwait(false);
		await PushMessageToSubscriberAsync(
			subscriber, CreateTestMessage("multi-3"), CancellationToken.None).ConfigureAwait(false);

		if (receivedCount != 3)
		{
			throw new InvalidOperationException(
				$"Handler should have been invoked 3 times, got {receivedCount}.");
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await subscribeTask.ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSubscriber.GetService"/> returns null for unknown types.
	/// </summary>
	protected async Task VerifyGetServiceReturnsNullForUnknownType()
	{
		await using var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);

		var result = subscriber.GetService(typeof(IDisposable));

		if (result is not null)
		{
			throw new InvalidOperationException(
				"GetService should return null for unknown service types.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="ITransportSubscriber.DisposeAsync"/> can be called multiple times.
	/// </summary>
	protected async Task VerifyDisposeAsyncIsIdempotent()
	{
		var subscriber = await CreateSubscriberAsync().ConfigureAwait(false);

		await subscriber.DisposeAsync().ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);
		// No exception = pass
	}
}
