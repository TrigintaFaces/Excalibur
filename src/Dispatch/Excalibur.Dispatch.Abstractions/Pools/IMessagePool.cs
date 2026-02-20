// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines a message pool for zero-allocation message processing.
/// </summary>
public interface IMessagePool : IDisposable
{
	/// <summary>
	/// Gets the number of messages currently rented from the pool.
	/// </summary>
	int RentedCount { get; }

	/// <summary>
	/// Gets the total capacity of the pool.
	/// </summary>
	int Capacity { get; }

	/// <summary>
	/// Gets the number of available messages in the pool.
	/// </summary>
	int AvailableCount { get; }

	/// <summary>
	/// Rents a message from the pool.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message. </typeparam>
	/// <returns> A handle to the rented message that must be disposed to return it to the pool. </returns>
	MessageHandle<TMessage> Rent<TMessage>()
		where TMessage : class, new();

	/// <summary>
	/// Attempts to rent a message from the pool without blocking.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message. </typeparam>
	/// <param name="handle"> The rented message handle if successful. </param>
	/// <returns> true if a message was rented; otherwise, false. </returns>
	bool TryRent<TMessage>(out MessageHandle<TMessage> handle)
		where TMessage : class, new();

	/// <summary>
	/// Clears all pooled messages.
	/// </summary>
	void Clear();

	/// <summary>
	/// Returns a message to the pool.
	/// </summary>
	/// <typeparam name="TMessage"> The type of message. </typeparam>
	/// <param name="message"> The message to return. </param>
	void ReturnToPool<TMessage>(TMessage message)
		where TMessage : class;
}
