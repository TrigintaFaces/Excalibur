// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Snapshot strategy based on event count interval.
/// </summary>
/// <remarks>
/// <para>
/// Creates a snapshot after a specified number of events have been applied to an aggregate.
/// This is the most common snapshot strategy, providing predictable snapshot frequency.
/// </para>
/// </remarks>
public sealed class IntervalSnapshotStrategy : ISnapshotStrategy
{
	private readonly int _interval;

	/// <summary>
	/// Initializes a new instance of the <see cref="IntervalSnapshotStrategy"/> class.
	/// </summary>
	/// <param name="interval">The number of events between snapshots. Default is 100. Must be greater than zero.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is less than or equal to zero. Validating here fails fast at
	/// configuration time rather than throwing <see cref="DivideByZeroException"/> on the snapshot-decision
	/// path for every aggregate (interval==0), or producing nonsensical results (negative interval).
	/// </exception>
	public IntervalSnapshotStrategy(int interval = 100)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(interval);
		_interval = interval;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode(
		"Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		return aggregate.Version > 0 && aggregate.Version % _interval == 0;
	}
}
