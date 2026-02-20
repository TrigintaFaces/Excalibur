// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Represents a snapshot of an aggregate's state at a specific version.
/// </summary>
public interface ISnapshot
{
	/// <summary>
	/// Gets the unique identifier for this snapshot.
	/// </summary>
	/// <value>The unique identifier for this snapshot.</value>
	string SnapshotId { get; }

	/// <summary>
	/// Gets the aggregate identifier this snapshot belongs to.
	/// </summary>
	/// <value>The aggregate identifier this snapshot belongs to.</value>
	string AggregateId { get; }

	/// <summary>
	/// Gets the version of the aggregate when this snapshot was created.
	/// </summary>
	/// <value>The version of the aggregate when this snapshot was created.</value>
	long Version { get; }

	/// <summary>
	/// Gets the timestamp when this snapshot was created.
	/// </summary>
	/// <value>The timestamp when this snapshot was created.</value>
	DateTimeOffset CreatedAt { get; }

	/// <summary>
	/// Gets the serialized state data.
	/// </summary>
	/// <value>The serialized state data.</value>
	byte[] Data { get; }

	/// <summary>
	/// Gets the type of the aggregate for deserialization.
	/// </summary>
	/// <value>The type of the aggregate for deserialization.</value>
	string AggregateType { get; }

	/// <summary>
	/// Gets optional metadata about the snapshot.
	/// </summary>
	/// <value>Optional metadata about the snapshot.</value>
	IDictionary<string, object>? Metadata { get; }
}
