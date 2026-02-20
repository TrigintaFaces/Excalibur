// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain.Model;

/// <summary>
/// Provides a base class for entities with a default key type of <see cref="string" />.
/// </summary>
/// <remarks> This class is intended to serve as a base for domain entities with a default key type. </remarks>
public abstract class EntityBase : EntityBase<string>;

// SA1402: Generic and non-generic versions with same name must coexist in one file due to SA1649 file naming requirement
// R0.8: File may only contain a single type
#pragma warning disable SA1402

/// <summary>
/// Provides a base class for entities with a specified key type.
/// </summary>
/// <typeparam name="TKey"> The type of the key used to uniquely identify the entity. </typeparam>
/// <remarks> This class implements <see cref="IEntity{TKey}" /> and provides equality comparison based on the key and type of the entity. </remarks>
public abstract class EntityBase<TKey> : IEntity<TKey>, IEquatable<IEntity<TKey>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="EntityBase{TKey}" /> class.
	/// </summary>
	protected EntityBase()
	{
	}

	/// <summary>
	/// Gets the key that uniquely identifies the entity.
	/// </summary>
	/// <value>
	/// The key that uniquely identifies the entity.
	/// </value>
	public abstract TKey Key { get; }

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as IEntity<TKey>);

	/// <inheritdoc />
	/// <summary>
	/// Determines whether the current entity is equal to another entity of the same type.
	/// </summary>
	/// <param name="other"> The other entity to compare with. </param>
	/// <returns>
	/// <c> true </c> if the specified entity is of the same type and has the same key as the current entity; otherwise, <c> false </c>.
	/// </returns>
	public virtual bool Equals(IEntity<TKey>? other)
	{
		if (other == null)
		{
			return false;
		}

		return other.GetType() == GetType() && other.Key.Equals(Key);
	}

	/// <inheritdoc />
	/// <summary>
	/// Serves as the default hash function for the entity.
	/// </summary>
	/// <returns> A hash code for the current entity. </returns>
	public override int GetHashCode() => HashCode.Combine(GetType(), Key);
}
