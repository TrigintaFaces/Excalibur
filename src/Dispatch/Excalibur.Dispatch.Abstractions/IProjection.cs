// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a read model projection that is built from domain events.
/// </summary>
/// <typeparam name="TKey"> The type of the key that uniquely identifies this projection. </typeparam>
/// <remarks>
/// Projections are read models that are derived from event streams and are used to support query scenarios in CQRS and Event Sourcing architectures.
/// </remarks>
public interface IProjection<out TKey>
	where TKey : notnull
{
	/// <summary>
	/// Gets the unique identifier for this projection.
	/// </summary>
	/// <value> The unique projection identifier. </value>
	TKey Id { get; }

	/// <summary>
	/// Gets the version of this projection, typically incremented when events are applied.
	/// </summary>
	/// <value> The projection version number. </value>
	long Version { get; }

	/// <summary>
	/// Gets the timestamp when this projection was last updated.
	/// </summary>
	/// <value> The timestamp of the last update. </value>
	DateTimeOffset LastModified { get; }
}
