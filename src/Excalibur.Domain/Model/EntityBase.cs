namespace Excalibur.Domain.Model;

/// <summary>
///     Provides a base class for entities with a default key type of <see cref="string" />.
/// </summary>
/// <remarks> This class is intended to serve as a base for domain entities with a default key type. </remarks>
public abstract class EntityBase : EntityBase<string>
{
}

/// <summary>
///     Provides a base class for entities with a specified key type.
/// </summary>
/// <typeparam name="TKey"> The type of the key used to uniquely identify the entity. </typeparam>
/// <remarks>
///     This class implements <see cref="IEntity{TKey}" /> and provides equality comparison based on the key and type of the entity.
/// </remarks>
public abstract class EntityBase<TKey> : IEntity<TKey>, IEquatable<IEntity<TKey>>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="EntityBase{TKey}" /> class.
	/// </summary>
	protected EntityBase()
	{
	}

	/// <summary>
	///     Gets the key that uniquely identifies the entity.
	/// </summary>
	public abstract TKey Key { get; }

	/// <inheritdoc />
	public override bool Equals(object? obj) => Equals(obj as IEntity<TKey>);

	/// <inheritdoc />
	/// <summary>
	///     Determines whether the current entity is equal to another entity of the same type.
	/// </summary>
	/// <param name="other"> The other entity to compare with. </param>
	/// <returns>
	///     <c> true </c> if the specified entity is of the same type and has the same key as the current entity; otherwise, <c> false </c>.
	/// </returns>
	public virtual bool Equals(IEntity<TKey>? other)
	{
		if (other == null)
		{
			return false;
		}

		return other.GetType() == GetType() && other.Key!.Equals(Key);
	}

	/// <inheritdoc />
	/// <summary>
	///     Serves as the default hash function for the entity.
	/// </summary>
	/// <returns> A hash code for the current entity. </returns>
	public override int GetHashCode() => 571 ^ GetType().GetHashCode() ^ Key!.GetHashCode();
}
