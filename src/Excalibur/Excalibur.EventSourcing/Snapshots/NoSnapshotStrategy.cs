// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Snapshot strategy that never creates snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Use this strategy when snapshots are not desired, such as for aggregates
/// with short event histories or when event replay is always acceptable.
/// </para>
/// </remarks>
public sealed class NoSnapshotStrategy : ISnapshotStrategy
{
	/// <summary>
	/// Gets the singleton instance of the no-op snapshot strategy.
	/// </summary>
	public static NoSnapshotStrategy Instance { get; } = new();

	/// <inheritdoc />
	[RequiresUnreferencedCode("Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate) => false;
}
