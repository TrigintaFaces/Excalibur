// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// In-memory implementation of <see cref="ITransportReceiver"/> for testing.
/// Provides a controllable message queue and tracks acknowledgments and rejections.
/// </summary>
/// <remarks>
/// <para>
/// Tests enqueue messages via <see cref="Enqueue(TransportReceivedMessage)"/> and then
/// verify processing via <see cref="AcknowledgedMessages"/> and <see cref="RejectedMessages"/>.
/// </para>
/// <para>
/// <see cref="ReceiveAsync"/> is non-blocking — it returns immediately with whatever messages
/// are currently in the queue (up to <c>maxMessages</c>). This is intentional: test code
/// controls timing explicitly by calling <see cref="Enqueue(TransportReceivedMessage)"/> before
/// <see cref="ReceiveAsync"/>.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var receiver = new InMemoryTransportReceiver("orders-queue");
/// receiver.Enqueue(new TransportReceivedMessage { Id = "msg-1", Body = body });
///
/// var messages = await receiver.ReceiveAsync(10, CancellationToken.None);
/// messages.Count.ShouldBe(1);
///
/// await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);
/// receiver.AcknowledgedMessages.Count.ShouldBe(1);
/// </code>
/// </para>
/// </remarks>
public sealed class InMemoryTransportReceiver : ITransportReceiver
{
	private readonly ConcurrentQueue<TransportReceivedMessage> _pending = new();
	private readonly ConcurrentQueue<TransportReceivedMessage> _acknowledged = new();
	private readonly ConcurrentQueue<RejectedMessage> _rejected = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryTransportReceiver"/> class.
	/// </summary>
	/// <param name="source">The source name this receiver is configured for.</param>
	public InMemoryTransportReceiver(string source)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		Source = source;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <summary>
	/// Gets all messages that have been acknowledged.
	/// </summary>
	/// <value>A snapshot of acknowledged messages.</value>
	public IReadOnlyList<TransportReceivedMessage> AcknowledgedMessages => _acknowledged.ToArray();

	/// <summary>
	/// Gets all messages that have been rejected, along with their rejection details.
	/// </summary>
	/// <value>A snapshot of rejected messages with reason and requeue flag.</value>
	public IReadOnlyList<RejectedMessage> RejectedMessages => _rejected.ToArray();

	/// <summary>
	/// Enqueues a message for the next <see cref="ReceiveAsync"/> call.
	/// </summary>
	/// <param name="message">The message to enqueue.</param>
	public void Enqueue(TransportReceivedMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);
		_pending.Enqueue(message);
	}

	/// <summary>
	/// Enqueues multiple messages for the next <see cref="ReceiveAsync"/> call.
	/// </summary>
	/// <param name="messages">The messages to enqueue.</param>
	public void Enqueue(params TransportReceivedMessage[] messages)
	{
		ArgumentNullException.ThrowIfNull(messages);

		foreach (var message in messages)
		{
			_pending.Enqueue(message);
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Non-blocking: returns immediately with up to <paramref name="maxMessages"/>
	/// messages from the internal queue. Returns an empty list if no messages are available.
	/// </remarks>
	public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var result = new List<TransportReceivedMessage>(Math.Min(maxMessages, _pending.Count));

		while (result.Count < maxMessages && _pending.TryDequeue(out var message))
		{
			result.Add(message);
		}

		return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(result);
	}

	/// <inheritdoc />
	public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		_acknowledged.Enqueue(message);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		_rejected.Enqueue(new RejectedMessage(message, reason, requeue));
		return Task.CompletedTask;
	}

	/// <summary>
	/// Clears all pending, acknowledged, and rejected messages.
	/// </summary>
	public void Clear()
	{
		while (_pending.TryDequeue(out _))
		{
			// Drain pending queue
		}

		while (_acknowledged.TryDequeue(out _))
		{
			// Drain acknowledged queue
		}

		while (_rejected.TryDequeue(out _))
		{
			// Drain rejected queue
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Records a message that was rejected by the receiver, along with rejection details.
/// </summary>
/// <param name="Message">The rejected message.</param>
/// <param name="Reason">The reason for rejection, or <see langword="null"/>.</param>
/// <param name="Requeue">Whether the message was requested to be requeued.</param>
public sealed record RejectedMessage(
	TransportReceivedMessage Message,
	string? Reason,
	bool Requeue);
