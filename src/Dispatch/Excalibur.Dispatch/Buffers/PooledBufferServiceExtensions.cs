// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Extension methods for IPooledBufferService.
/// </summary>
public static class PooledBufferServiceExtensions
{
	/// <summary>
	/// Rents a buffer wrapped in a PooledBuffer struct that auto-returns on disposal.
	/// </summary>
	/// <param name="manager"> The buffer manager. </param>
	/// <param name="minimumLength"> The minimum length of the array needed. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer contents before returning. </param>
	/// <param name="clearOnReturn"> Whether to clear the buffer when returning to pool. </param>
	/// <returns> A PooledBuffer that must be disposed. </returns>
	public static PooledBuffer RentPooledBuffer(
		this IPooledBufferService manager,
		int minimumLength,
		bool clearBuffer = false,
		bool clearOnReturn = true)
	{
		ArgumentNullException.ThrowIfNull(manager);

		var buffer = manager.RentBuffer(minimumLength, clearBuffer);
		return new PooledBuffer(manager, buffer.Buffer, clearOnReturn);
	}

	/// <summary>
	/// Executes an action with a rented buffer that is automatically returned.
	/// </summary>
	public static void UseBuffer(
		this IPooledBufferService manager,
		int minimumLength,
		Action<byte[]> action) =>
		UseBuffer(manager, minimumLength, action, clearBuffer: false, clearOnReturn: true);

	/// <summary>
	/// Executes an action with a rented buffer that is automatically returned.
	/// </summary>
	public static void UseBuffer(
		this IPooledBufferService manager,
		int minimumLength,
		Action<byte[]> action,
		bool clearBuffer,
		bool clearOnReturn)
	{
		ArgumentNullException.ThrowIfNull(manager);

		ArgumentNullException.ThrowIfNull(action);

		var buffer = manager.RentBuffer(minimumLength, clearBuffer);
		try
		{
			action(buffer.Buffer);
		}
		finally
		{
			manager.ReturnBuffer(buffer, clearOnReturn);
		}
	}

	/// <summary>
	/// Executes a function with a rented buffer that is automatically returned.
	/// </summary>
	public static TResult UseBuffer<TResult>(
		this IPooledBufferService manager,
		int minimumLength,
		Func<byte[], TResult> function) =>
		UseBuffer(manager, minimumLength, function, clearBuffer: false, clearOnReturn: true);

	/// <summary>
	/// Executes a function with a rented buffer that is automatically returned.
	/// </summary>
	public static TResult UseBuffer<TResult>(
		this IPooledBufferService manager,
		int minimumLength,
		Func<byte[], TResult> function,
		bool clearBuffer,
		bool clearOnReturn)
	{
		ArgumentNullException.ThrowIfNull(manager);
		ArgumentNullException.ThrowIfNull(function);

		var buffer = manager.RentBuffer(minimumLength, clearBuffer);
		try
		{
			return function(buffer.Buffer);
		}
		finally
		{
			manager.ReturnBuffer(buffer, clearOnReturn);
		}
	}
}
