// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Testing.Tracking;

/// <summary>
/// Thread-safe log of dispatched messages for test assertions.
/// </summary>
public sealed class DispatchedMessageLog : IDispatchedMessageLog
{
	private readonly ConcurrentQueue<DispatchedMessage> _messages = new();

	/// <inheritdoc />
	public IReadOnlyList<DispatchedMessage> All => _messages.ToArray();

	/// <inheritdoc />
	public IReadOnlyList<DispatchedMessage> Select<TMessage>() where TMessage : IDispatchMessage =>
		_messages.Where(m => m.Message is TMessage).ToArray();

	/// <inheritdoc />
	public bool Any<TMessage>() where TMessage : IDispatchMessage =>
		_messages.Any(m => m.Message is TMessage);

	/// <inheritdoc />
	public int Count => _messages.Count;

	/// <inheritdoc />
	public void Clear()
	{
		while (_messages.TryDequeue(out _))
		{
			// drain queue
		}
	}

	/// <summary>
	/// Records a dispatched message. Called internally by <see cref="TestTrackingMiddleware"/>.
	/// </summary>
	/// <param name="message">The dispatched message record.</param>
	internal void Record(DispatchedMessage message) => _messages.Enqueue(message);
}
