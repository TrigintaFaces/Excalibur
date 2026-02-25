// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Provides serialization for aggregate snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface handle the conversion of aggregate state
/// to and from byte arrays for storage in <see cref="ISnapshotStore"/>.
/// </para>
/// </remarks>
public interface ISnapshotSerializer
{
	/// <summary>
	/// Serializes aggregate state to snapshot data.
	/// </summary>
	/// <typeparam name="TState">The type of the aggregate state.</typeparam>
	/// <param name="state">The aggregate state to serialize.</param>
	/// <returns>The serialized snapshot data.</returns>
	byte[] Serialize<TState>(TState state);

	/// <summary>
	/// Deserializes snapshot data to aggregate state.
	/// </summary>
	/// <typeparam name="TState">The type of the aggregate state.</typeparam>
	/// <param name="data">The snapshot data to deserialize.</param>
	/// <returns>The deserialized aggregate state.</returns>
	TState Deserialize<TState>(byte[] data);
}
