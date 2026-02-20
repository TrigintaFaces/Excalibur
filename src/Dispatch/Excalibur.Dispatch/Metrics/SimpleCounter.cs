// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// A lightweight, non-thread-safe counter implementation optimized for single-threaded scenarios or testing.
/// </summary>
/// <remarks>
/// This counter provides minimal overhead for scenarios where thread-safety is not required, such as:
/// <para> <strong> Use Cases: </strong> </para>
/// - Unit testing and mock implementations
/// - Single-threaded applications or isolated components
/// - Prototype development and benchmarking
/// - Internal metrics where performance is prioritized over thread-safety.
/// <para> <strong> Performance Characteristics: </strong> </para>
/// - Zero synchronization overhead
/// - Direct memory access with minimal CPU instructions
/// - No atomic operations or memory barriers
/// - Ideal for high-frequency counting in single-threaded contexts.
/// <para> <strong> Thread Safety Warning: </strong> </para>
/// This implementation is NOT thread-safe. Concurrent access from multiple threads can result in lost updates, incorrect values, or other
/// race conditions. For multi-threaded scenarios, use <see cref="CacheAlignedCounter" /> or other thread-safe implementations.
/// </remarks>
public class SimpleCounter : ICounterMetric
{
	/// <inheritdoc />
	/// <remarks>
	/// The value is stored as a simple field with no synchronization. In single-threaded scenarios, this provides the fastest possible read
	/// access with no overhead.
	/// </remarks>
	public double Value { get; private set; }

	/// <inheritdoc />
	/// <remarks>
	/// This implementation performs a direct addition without any thread-safety measures. The operation is atomic at the CPU instruction
	/// level for the addition itself, but the overall read-modify-write operation is not protected against concurrent access.
	/// </remarks>
	public void Increment(double amount = 1.0) => Value += amount;
}
