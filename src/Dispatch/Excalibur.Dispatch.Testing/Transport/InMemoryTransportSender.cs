// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Transport;

/// <summary>
/// In-memory implementation of <see cref="ITransportSender"/> for testing.
/// Records all sent messages and supports configurable send behavior via <see cref="OnSend"/>.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>InMemoryChatClient</c> pattern from Microsoft.Extensions.AI —
/// captures all interactions for test assertions while providing controllable behavior.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var sender = new InMemoryTransportSender("orders-topic");
/// await sender.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);
///
/// sender.SentMessages.Count.ShouldBe(1);
/// </code>
/// </para>
/// </remarks>
public sealed class InMemoryTransportSender : ITransportSender
{
	private readonly ConcurrentQueue<TransportMessage> _sentMessages = new();
	private Func<TransportMessage, SendResult>? _onSend;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryTransportSender"/> class.
	/// </summary>
	/// <param name="destination">The destination name this sender is configured for.</param>
	public InMemoryTransportSender(string destination)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);
		Destination = destination;
	}

	/// <inheritdoc />
	public string Destination { get; }

	/// <summary>
	/// Gets all messages that have been sent through this sender, in chronological order.
	/// </summary>
	/// <value>A snapshot of sent messages.</value>
	public IReadOnlyList<TransportMessage> SentMessages => _sentMessages.ToArray();

	/// <summary>
	/// Configures a callback to control send behavior. The callback receives each message
	/// and returns a <see cref="SendResult"/>. If not configured, all sends return
	/// <see cref="SendResult.Success"/> with the message's <see cref="TransportMessage.Id"/>.
	/// </summary>
	/// <param name="handler">The callback to invoke for each sent message.</param>
	/// <returns>This sender for chaining.</returns>
	public InMemoryTransportSender OnSend(Func<TransportMessage, SendResult> handler)
	{
		ArgumentNullException.ThrowIfNull(handler);
		_onSend = handler;
		return this;
	}

	/// <inheritdoc />
	public Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		_sentMessages.Enqueue(message);
		var result = _onSend?.Invoke(message) ?? SendResult.Success(message.Id);
		return Task.FromResult(result);
	}

	/// <inheritdoc />
	public Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messages);
		cancellationToken.ThrowIfCancellationRequested();

		var stopwatch = Stopwatch.StartNew();
		var results = new List<SendResult>(messages.Count);

		foreach (var message in messages)
		{
			_sentMessages.Enqueue(message);
			var result = _onSend?.Invoke(message) ?? SendResult.Success(message.Id);
			results.Add(result);
		}

		stopwatch.Stop();

		var successCount = 0;
		var failureCount = 0;

		foreach (var result in results)
		{
			if (result.IsSuccess)
			{
				successCount++;
			}
			else
			{
				failureCount++;
			}
		}

		var batchResult = new BatchSendResult
		{
			TotalMessages = messages.Count,
			SuccessCount = successCount,
			FailureCount = failureCount,
			Results = results,
			Duration = stopwatch.Elapsed,
		};

		return Task.FromResult(batchResult);
	}

	/// <inheritdoc />
	public Task FlushAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Clears all recorded sent messages.
	/// </summary>
	public void Clear()
	{
		while (_sentMessages.TryDequeue(out _))
		{
			// Drain the queue
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
