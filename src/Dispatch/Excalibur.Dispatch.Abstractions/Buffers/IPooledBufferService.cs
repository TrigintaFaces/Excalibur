// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Service interface for managing pooled byte buffers using ArrayPool for efficient memory usage.
/// </summary>
public interface IPooledBufferService
{
	/// <summary>
	/// Gets the current number of buffers rented from the pool.
	/// </summary>
	int RentedBuffers { get; }

	/// <summary>
	/// Gets the total number of rent operations performed.
	/// </summary>
	long TotalRentOperations { get; }

	/// <summary>
	/// Gets the total number of return operations performed.
	/// </summary>
	long TotalReturnOperations { get; }

	/// <summary>
	/// Gets the largest buffer size requested.
	/// </summary>
	int LargestBufferRequested { get; }

	/// <summary>
	/// Rents a buffer of at least the specified minimum length.
	/// </summary>
	/// <param name="minimumLength"> The minimum length of the array needed. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer contents before returning. </param>
	/// <returns> A disposable pooled buffer that returns to the pool on disposal. </returns>
	/// <remarks>
	/// The returned buffer implements <see cref="IDisposablePooledBuffer"/> and must be
	/// disposed to return the underlying memory to the pool. Using a <c>using</c> statement
	/// or <c>using</c> declaration is recommended.
	/// </remarks>
	IDisposablePooledBuffer RentBuffer(int minimumLength, bool clearBuffer = false);

	/// <summary>
	/// Returns a rented buffer to the pool.
	/// </summary>
	/// <param name="buffer"> The buffer to return. </param>
	/// <param name="clearBuffer"> Whether to clear the buffer contents before returning to pool. </param>
	void ReturnBuffer(IPooledBuffer buffer, bool clearBuffer = true);

	/// <summary>
	/// Gets buffer usage statistics.
	/// </summary>
	/// <returns> Buffer usage statistics. </returns>
	BufferPoolStatistics GetStatistics();
}
