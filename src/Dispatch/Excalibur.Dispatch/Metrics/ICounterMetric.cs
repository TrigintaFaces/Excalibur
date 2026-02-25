// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Represents a counter metric that tracks cumulative values over time through increment operations.
/// </summary>
/// <remarks>
/// <para>
/// Counter metrics are monotonically increasing values used to track events, operations, or occurrences in the messaging system. They are
/// ideal for measuring:
/// </para>
/// <para>
/// - Total number of messages processed
/// - Error counts by category
/// - Request counts and throughput
/// - Resource usage accumulation
/// </para>
/// <para>
/// Counter values never decrease (except on reset) and provide the foundation for rate calculations and trend analysis in observability
/// systems. Thread-safe implementations ensure accurate counting in concurrent scenarios.
/// </para>
/// </remarks>
public interface ICounterMetric
{
	/// <summary>
	/// Gets the current cumulative value of the counter.
	/// </summary>
	/// <value>
	/// The total accumulated value since counter initialization. This value is monotonically increasing and represents the sum of all
	/// increments applied to the counter.
	/// </value>
	/// <remarks>
	/// The value is thread-safe for reading and reflects the most recent state of the counter. In high-throughput scenarios, brief
	/// inconsistencies may occur during concurrent updates but will resolve to accurate values once operations complete.
	/// </remarks>
	double Value { get; }

	/// <summary>
	/// Increments the counter by the specified positive amount.
	/// </summary>
	/// <param name="amount"> The positive amount to add to the counter. Must be greater than or equal to 0. Default is 1.0. </param>
	/// <remarks>
	/// <para>
	/// This operation is thread-safe and atomic, ensuring accurate increments in concurrent scenarios. The increment amount should be
	/// non-negative to maintain counter semantics. Zero increments are allowed but have no effect on the counter value.
	/// </para>
	/// <para>
	/// For high-frequency updates, implementations may use optimizations like atomic operations or lock-free data structures to minimize
	/// contention and maximize throughput.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException"> Thrown when amount is negative, as counters should only increase. </exception>
	void Increment(double amount = 1.0);
}
