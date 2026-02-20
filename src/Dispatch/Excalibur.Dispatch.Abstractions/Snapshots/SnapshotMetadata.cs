// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Contains metadata information about a snapshot.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class SnapshotMetadata
{
	/// <summary>
	/// Gets the timestamp of the last applied event.
	/// </summary>
	/// <value>The current <see cref="LastAppliedEventTimestamp"/> value.</value>
	public required DateTimeOffset LastAppliedEventTimestamp { get; init; }

	/// <summary>
	/// Gets the ID of the last applied event.
	/// </summary>
	/// <value>The current <see cref="LastAppliedEventId"/> value.</value>
	public required string LastAppliedEventId { get; init; }

	/// <summary>
	/// Gets the version of the snapshot format.
	/// </summary>
	/// <value>The current <see cref="SnapshotVersion"/> value.</value>
	public required string SnapshotVersion { get; init; }

	/// <summary>
	/// Gets the version of the serializer used to create the snapshot.
	/// </summary>
	/// <value>The current <see cref="SerializerVersion"/> value.</value>
	public required string SerializerVersion { get; init; }
}
