// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

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
/// <param name="interval">The number of events between snapshots. Default is 100.</param>
public sealed class IntervalSnapshotStrategy(int interval = 100) : ISnapshotStrategy
{
	/// <inheritdoc />
	[RequiresUnreferencedCode(
		"Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate);
		return aggregate.Version > 0 && aggregate.Version % interval == 0;
	}
}
