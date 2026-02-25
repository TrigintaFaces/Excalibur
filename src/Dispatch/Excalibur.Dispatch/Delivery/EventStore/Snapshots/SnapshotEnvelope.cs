// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Default implementation of a snapshot envelope that contains aggregate state.
/// </summary>
/// <typeparam name="TKey"> The type of the aggregate identifier. </typeparam>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public sealed class SnapshotEnvelope<TKey> : ISnapshotEnvelope<TKey>
{
	/// <summary>
	/// Gets the identifier of the aggregate this snapshot belongs to.
	/// </summary>
	/// <value>The current <see cref="AggregateId"/> value.</value>
	public required TKey AggregateId { get; init; }

	/// <summary>
	/// Gets the serialized application state data.
	/// </summary>
	/// <value>The current <see cref="ApplicationState"/> value.</value>
	public required string ApplicationState { get; init; }

	/// <summary>
	/// Gets the metadata associated with this snapshot.
	/// </summary>
	/// <value>The current <see cref="SnapshotMetadata"/> value.</value>
	public required string SnapshotMetadata { get; init; }
}
