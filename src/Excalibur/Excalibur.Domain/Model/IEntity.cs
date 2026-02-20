// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Represents a base entity in the domain-driven design (DDD) pattern.
/// </summary>
/// <remarks>
/// This interface serves as a marker for all entities within the domain model, providing a common abstraction. Entities are defined by
/// their identity rather than their attributes.
/// </remarks>
public interface IEntity;

/// <summary>
/// Represents a typed entity in the domain-driven design (DDD) pattern.
/// </summary>
/// <typeparam name="TKey"> The type of the key used to uniquely identify the entity. </typeparam>
/// <remarks>
/// Extends the base <see cref="IEntity" /> interface by adding a strongly-typed identifier. Entities are uniquely identified by their key
/// within the domain.
/// </remarks>
public interface IEntity<out TKey> : IEntity
{
	/// <summary>
	/// Gets the unique identifier for the entity.
	/// </summary>
	/// <remarks>
	/// The <see cref="Key" /> property defines the identity of the entity. Two entities are considered equal if they are of the same type
	/// and have the same key.
	/// </remarks>
	/// <value>
	/// The unique identifier for the entity.
	/// </value>
	TKey Key { get; }
}
