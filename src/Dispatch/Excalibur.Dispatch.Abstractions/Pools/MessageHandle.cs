// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a handle to a rented message from the pool.
/// </summary>
/// <typeparam name="TMessage"> The type of message. </typeparam>
/// <param name="pool"> The message pool. </param>
/// <param name="message"> The rented message. </param>
/// <remarks> Initializes a new instance of the <see cref="MessageHandle{TMessage}" /> struct. </remarks>
public readonly struct MessageHandle<TMessage>(IMessagePool pool, TMessage message)
	: IDisposable, IEquatable<MessageHandle<TMessage>>
	where TMessage : class
{
	private readonly IMessagePool _pool = pool ?? throw new ArgumentNullException(nameof(pool));

	/// <summary>
	/// Gets the rented message.
	/// </summary>
	public TMessage Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Determines whether two handles are equal.
	/// </summary>
	/// <param name="left"> The first handle to compare. </param>
	/// <param name="right"> The second handle to compare. </param>
	public static bool operator ==(MessageHandle<TMessage> left, MessageHandle<TMessage> right) => left.Equals(right);

	/// <summary>
	/// Determines whether two handles are not equal.
	/// </summary>
	/// <param name="left"> The first handle to compare. </param>
	/// <param name="right"> The second handle to compare. </param>
	public static bool operator !=(MessageHandle<TMessage> left, MessageHandle<TMessage> right) => !left.Equals(right);

	/// <summary>
	/// Returns the message to the pool.
	/// </summary>
	public void Dispose() =>

		// Return the message to the pool
		_pool?.ReturnToPool(Message);

	/// <summary>
	/// Determines whether the specified handle is equal to the current handle.
	/// </summary>
	/// <param name="other"> The handle to compare with the current handle. </param>
	public bool Equals(MessageHandle<TMessage> other) => ReferenceEquals(_pool, other._pool) && ReferenceEquals(Message, other.Message);

	/// <summary>
	/// Determines whether the specified object is equal to the current handle.
	/// </summary>
	/// <param name="obj"> The object to compare with the current handle. </param>
	public override bool Equals(object? obj) => obj is MessageHandle<TMessage> other && Equals(other);

	/// <summary>
	/// Returns the hash code for this handle.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(_pool, Message);
}
