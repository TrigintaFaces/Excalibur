// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Serializer for snapshot data used during snapshot upgrading.
/// </summary>
/// <remarks>
/// <para>
/// Provides serialization between typed snapshot objects and their binary representation (byte[]).
/// Used by <see cref="ISnapshotUpgrader"/> implementations that require typed access to snapshot data.
/// </para>
/// </remarks>
public interface ISnapshotDataSerializer
{
	/// <summary>
	/// Serializes a snapshot object to bytes.
	/// </summary>
	/// <typeparam name="T">The type to serialize.</typeparam>
	/// <param name="value">The snapshot data to serialize.</param>
	/// <returns>The serialized bytes.</returns>
	byte[] Serialize<T>(T value) where T : class;

	/// <summary>
	/// Deserializes bytes to a typed snapshot object.
	/// </summary>
	/// <typeparam name="T">The target type.</typeparam>
	/// <param name="data">The serialized snapshot data.</param>
	/// <returns>The deserialized snapshot object.</returns>
	T? Deserialize<T>(byte[] data) where T : class;
}
