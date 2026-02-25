// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// In-memory implementation of <see cref="ITransportSubscriber"/> for testing.
/// Allows tests to push messages to the handler and inspect processing results.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="SubscribeAsync"/> to register a handler (mirrors the real subscription lifecycle),
/// then use <see cref="PushAsync"/> from test code to deliver messages to the handler.
/// Each processed message and its resulting <see cref="MessageAction"/> are recorded in
/// <see cref="ProcessedMessages"/>.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var subscriber = new InMemoryTransportSubscriber("orders-topic");
///
/// // Start subscription in background
/// var cts = new CancellationTokenSource();
/// var subscribeTask = subscriber.SubscribeAsync(
///     (msg, ct) => Task.FromResult(MessageAction.Acknowledge), cts.Token);
///
/// // Push a test message
/// var action = await subscriber.PushAsync(
///     new TransportReceivedMessage { Id = "msg-1" }, CancellationToken.None);
///
/// action.ShouldBe(MessageAction.Acknowledge);
/// subscriber.ProcessedMessages.Count.ShouldBe(1);
///
/// // Stop subscription
/// cts.Cancel();
/// </code>
/// </para>
/// </remarks>
public sealed class InMemoryTransportSubscriber : ITransportSubscriber
{
	private readonly ConcurrentBag<ProcessedMessage> _processed = new();
	private volatile Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>? _handler;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryTransportSubscriber"/> class.
	/// </summary>
	/// <param name="source">The source name this subscriber is configured for.</param>
	public InMemoryTransportSubscriber(string source)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		Source = source;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <summary>
	/// Gets a value indicating whether a handler is currently registered via <see cref="SubscribeAsync"/>.
	/// </summary>
	/// <value><see langword="true"/> if a subscription is active; otherwise, <see langword="false"/>.</value>
	public bool IsSubscribed => _handler is not null;

	/// <summary>
	/// Gets all messages that have been processed, along with the handler's returned action.
	/// </summary>
	/// <value>A snapshot of processed messages and their actions.</value>
	public IReadOnlyList<ProcessedMessage> ProcessedMessages => _processed.ToArray();

	/// <inheritdoc />
	/// <remarks>
	/// Stores the handler and waits until the <paramref name="cancellationToken"/> is cancelled.
	/// Messages are delivered to the handler via <see cref="PushAsync"/> from test code.
	/// </remarks>
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		_handler = handler;

		try
		{
			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Normal shutdown — subscription cancelled
		}
		finally
		{
			_handler = null;
		}
	}

	/// <summary>
	/// Pushes a message to the registered handler for processing.
	/// </summary>
	/// <param name="message">The message to push to the handler.</param>
	/// <param name="cancellationToken">Cancellation token for the handler invocation.</param>
	/// <returns>The <see cref="MessageAction"/> returned by the handler.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if no subscription is active. Call <see cref="SubscribeAsync"/> first.
	/// </exception>
	public async Task<MessageAction> PushAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var handler = _handler ?? throw new InvalidOperationException(
			"No subscription is active. Call SubscribeAsync before pushing messages.");

		var action = await handler(message, cancellationToken).ConfigureAwait(false);
		_processed.Add(new ProcessedMessage(message, action));
		return action;
	}

	/// <summary>
	/// Clears the handler and all processed messages.
	/// </summary>
	public void Clear()
	{
		_handler = null;

		while (_processed.TryTake(out _))
		{
			// Drain processed bag
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_handler = null;
		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// Records a message that was processed by the subscriber handler and the resulting action.
/// </summary>
/// <param name="Message">The processed message.</param>
/// <param name="Action">The action returned by the handler.</param>
public sealed record ProcessedMessage(
	TransportReceivedMessage Message,
	MessageAction Action);
