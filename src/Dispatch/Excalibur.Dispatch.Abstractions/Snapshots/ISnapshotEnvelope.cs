// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the contract for a snapshot envelope that contains aggregate state.
/// </summary>
/// <typeparam name="TKey"> The type of the aggregate identifier. </typeparam>
public interface ISnapshotEnvelope<TKey>
{
	/// <summary>
	/// Gets the identifier of the aggregate this snapshot belongs to.
	/// </summary>
	/// <value>
	/// The identifier of the aggregate this snapshot belongs to.
	/// </value>
	TKey AggregateId { get; init; }

	/// <summary>
	/// Gets the serialized application state data.
	/// </summary>
	/// <value>
	/// The serialized application state data.
	/// </value>
	string ApplicationState { get; init; }

	/// <summary>
	/// Gets the metadata associated with this snapshot.
	/// </summary>
	/// <value>
	/// The metadata associated with this snapshot.
	/// </value>
	string SnapshotMetadata { get; init; }
}
