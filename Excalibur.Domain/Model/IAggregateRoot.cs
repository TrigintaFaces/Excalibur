using Excalibur.Domain.Events;

namespace Excalibur.Domain.Model;

/// <summary>
///     Represents an aggregate root in the domain-driven design (DDD) pattern. An aggregate root is the entry point to an aggregate,
///     encapsulating its internal state and behavior.
/// </summary>
/// <remarks> This interface extends <see cref="IEntity" /> and adds functionality for domain event management and entity tagging. </remarks>
public interface IAggregateRoot : IEntity
{
	/// <summary>
	///     Gets or sets the entity tag (ETag), used for optimistic concurrency control.
	/// </summary>
	/// <remarks> The ETag serves as a version indicator for the aggregate, typically updated when the aggregate's state changes. </remarks>
	public string ETag { get; set; }

	/// <summary>
	///     Gets the collection of domain events associated with the aggregate.
	/// </summary>
	/// <remarks>
	///     Domain events represent significant business events that have occurred within the aggregate. These events can be used for
	///     event-driven architectures or for maintaining consistency across aggregates.
	/// </remarks>
	public IEnumerable<IDomainEvent> DomainEvents { get; }
}

/// <summary>
///     Represents a typed aggregate root in the domain-driven design (DDD) pattern.
/// </summary>
/// <typeparam name="TKey"> The type of the key used to uniquely identify the aggregate. </typeparam>
/// <remarks>
///     This interface extends <see cref="IAggregateRoot" /> and <see cref="IEntity{TKey}" />, adding type safety for the key.
/// </remarks>
public interface IAggregateRoot<out TKey> : IAggregateRoot, IEntity<TKey>
{
}
