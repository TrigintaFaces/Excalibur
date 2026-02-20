// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Domain.Model;

/// <summary>
/// Provides snapshot and history replay capabilities for an aggregate root.
/// </summary>
/// <remarks>
/// <para>
/// This interface is separated from <see cref="IAggregateRoot"/> following the
/// Interface Segregation Principle (ISP). Not all consumers of aggregates need
/// snapshot or history replay capabilities; many only need core event sourcing
/// operations (raising events, reading uncommitted events, committing).
/// </para>
/// <para>
/// Implementations that support snapshots should implement both <see cref="IAggregateRoot"/>
/// and <see cref="IAggregateSnapshotSupport"/>. The <see cref="AggregateRoot{TKey}"/> base class
/// implements both interfaces.
/// </para>
/// </remarks>
public interface IAggregateSnapshotSupport
{
	/// <summary>
	/// Gets the type name of the aggregate for persistence.
	/// </summary>
	/// <value>The type name of the aggregate for persistence.</value>
	string AggregateType { get; }

	/// <summary>
	/// Gets or sets the ETag for optimistic concurrency control.
	/// </summary>
	/// <value>
	/// The ETag value representing the current state version, or <see langword="null"/> if not set.
	/// Used for detecting concurrent modifications when saving aggregates.
	/// </value>
	string? ETag { get; set; }

	/// <summary>
	/// Loads the aggregate state from a history of events.
	/// </summary>
	/// <param name="history">The sequence of historical events to replay.</param>
	/// <remarks>
	/// This method replays events to rebuild aggregate state without adding them
	/// to the uncommitted events collection. Use this for event sourcing replay.
	/// </remarks>
	void LoadFromHistory(IEnumerable<IDomainEvent> history);

	/// <summary>
	/// Loads the aggregate state from a snapshot.
	/// </summary>
	/// <param name="snapshot"> The snapshot to load from. </param>
	void LoadFromSnapshot(ISnapshot snapshot);

	/// <summary>
	/// Creates a snapshot of the current aggregate state.
	/// </summary>
	/// <returns> A snapshot representing the current state. </returns>
	/// <remarks>
	/// This method may require runtime type inspection and dynamic code generation,
	/// which are not compatible with Native AOT compilation scenarios. Consider using
	/// source generation for snapshot creation in AOT-compiled applications.
	/// </remarks>
	[RequiresUnreferencedCode("Snapshot creation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot creation may require dynamic code generation which is not compatible with AOT compilation.")]
	ISnapshot CreateSnapshot();
}
