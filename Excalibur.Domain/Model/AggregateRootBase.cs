using Excalibur.Domain.Events;
using Excalibur.Extensions;

namespace Excalibur.Domain.Model;

/// <summary>
///     Base class for aggregate roots with a default key type of <see cref="string" />.
/// </summary>
/// <remarks> This class provides default implementations for managing domain events and an ETag property. </remarks>
public abstract class AggregateRootBase : AggregateRootBase<string>
{
}

/// <summary>
///     Base class for aggregate roots with a specified key type.
/// </summary>
/// <typeparam name="TKey"> The type of the key used to identify the aggregate root. </typeparam>
/// <remarks> This class implements <see cref="IAggregateRoot{TKey}" /> and provides mechanisms for handling domain events. </remarks>
public abstract class AggregateRootBase<TKey> : EntityBase<TKey>, IAggregateRoot<TKey>
{
	private readonly List<IDomainEvent> _domainEvents = [];

	/// <summary>
	///     Initializes a new instance of the <see cref="AggregateRootBase{TKey}" /> class.
	/// </summary>
	protected AggregateRootBase()
	{
	}

	/// <summary>
	///     A string used for optimistic concurrency control.
	/// </summary>
	/// <remarks> Defaults to a unique identifier generated using <see cref="Uuid7Extensions.GenerateString" />. </remarks>
	public string ETag { get; set; } = Uuid7Extensions.GenerateString();

	/// <inheritdoc />
	IEnumerable<IDomainEvent> IAggregateRoot.DomainEvents => _domainEvents;

	/// <summary>
	///     Raises a domain event and adds it to the internal collection of domain events.
	/// </summary>
	/// <typeparam name="TEvent"> The type of the domain event. </typeparam>
	/// <param name="event"> The domain event to raise. </param>
	protected void RaiseEvent<TEvent>(TEvent @event)
		where TEvent : IDomainEvent => _domainEvents.Add(@event);
}
