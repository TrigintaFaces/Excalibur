// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Handle for a rented message envelope that ensures proper return to pool.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying envelope reference.</description></item>
/// <item><description>Disposing any copy returns the envelope to the pool, invalidating all copies.</description></item>
/// <item><description>After disposal, accessing <see cref="Envelope"/> on any copy may reference a reused envelope.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var handle = pool.RentEnvelope&lt;TMessage&gt;();
/// var envelope = handle.Envelope;
/// // Process envelope
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TMessage">The type of message contained in the envelope.</typeparam>
public readonly struct MessageEnvelopeHandle<TMessage> : IDisposable, IEquatable<MessageEnvelopeHandle<TMessage>>
	where TMessage : class, IDispatchMessage
{
	private readonly TMessage? _rentedMessage;
	private readonly MessageEnvelopePool? _pool;

	internal MessageEnvelopeHandle(
		MessageEnvelope<TMessage> envelope,
		TMessage? rentedMessage,
		MessageEnvelopePool? pool)
	{
		Envelope = envelope;
		_rentedMessage = rentedMessage;
		_pool = pool;
	}

	/// <summary>
	/// Gets the envelope.
	/// </summary>
	/// <value>
	/// The envelope.
	/// </value>
	public MessageEnvelope<TMessage> Envelope
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
	}

	/// <summary>
	/// Determines whether two handles are equal.
	/// </summary>
	public static bool operator ==(MessageEnvelopeHandle<TMessage> left, MessageEnvelopeHandle<TMessage> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two handles are not equal.
	/// </summary>
	public static bool operator !=(MessageEnvelopeHandle<TMessage> left, MessageEnvelopeHandle<TMessage> right) => !left.Equals(right);

	/// <summary>
	/// Disposes the handle and returns resources to the pool.
	/// </summary>
	public void Dispose() => _pool?.Return(_rentedMessage);

	/// <summary>
	/// Determines whether the specified handle is equal to the current handle.
	/// </summary>
	public bool Equals(MessageEnvelopeHandle<TMessage> other) =>
		Envelope.Equals(other.Envelope) &&
		ReferenceEquals(_rentedMessage, other._rentedMessage) &&
		ReferenceEquals(_pool, other._pool);

	/// <summary>
	/// Determines whether the specified object is equal to the current handle.
	/// </summary>
	public override bool Equals(object? obj) => obj is MessageEnvelopeHandle<TMessage> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this handle.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(Envelope, _rentedMessage, _pool);
}
