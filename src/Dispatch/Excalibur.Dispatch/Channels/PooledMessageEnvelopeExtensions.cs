// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.ObjectPool;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Channels;

/// <summary>
/// Extension methods for working with pooled message envelopes.
/// </summary>
public static class PooledMessageEnvelopeExtensions
{
	/// <summary>
	/// Converts a regular MessageEnvelope to a pooled one using the specified message pool.
	/// </summary>
	public static MessageEnvelope ToPooled(
		this MessageEnvelope envelope,
		ObjectPool<IDispatchMessage> messagePool)
	{
		_ = messagePool; // Reserved for future pooling implementation
		return envelope; // MessageEnvelope is not poolable, return as-is
	}

	/// <summary>
	/// Processes a message envelope with automatic pooling.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task ProcessWithPoolingAsync(
		this MessageEnvelope envelope,
		ObjectPool<IDispatchMessage> messagePool,
		Func<MessageEnvelope, Task> processor)
	{
		ArgumentNullException.ThrowIfNull(processor);

		var pooledEnvelope = envelope.ToPooled(messagePool);

		await processor(pooledEnvelope).ConfigureAwait(false);
	}

	/// <summary>
	/// Processes a message envelope with automatic pooling and returns a result.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task<TResult> ProcessWithPoolingAsync<TResult>(
		this MessageEnvelope envelope,
		ObjectPool<IDispatchMessage> messagePool,
		Func<MessageEnvelope, Task<TResult>> processor)
	{
		ArgumentNullException.ThrowIfNull(processor);

		var pooledEnvelope = envelope.ToPooled(messagePool);

		return await processor(pooledEnvelope).ConfigureAwait(false);
	}
}
