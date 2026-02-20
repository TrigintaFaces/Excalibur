// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the strategy for creating snapshots of aggregates.
/// </summary>
public interface ISnapshotStrategy
{
	/// <summary>
	/// Determines whether a snapshot should be created for the aggregate.
	/// </summary>
	/// <param name="aggregate">The aggregate to check.</param>
	/// <returns>True if a snapshot should be created, otherwise false.</returns>
	[RequiresUnreferencedCode("Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	bool ShouldCreateSnapshot(IAggregateRoot aggregate);
}
