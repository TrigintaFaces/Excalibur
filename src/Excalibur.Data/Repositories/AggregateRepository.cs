using Excalibur.Data.Outbox;
using Excalibur.Domain;
using Excalibur.Domain.Model;
using Excalibur.Domain.Repositories;

namespace Excalibur.Data.Repositories;

/// <summary>
///     A base implementation of <see cref="IAggregateRepository{TAggregate, TKey}" /> for managing aggregates.
/// </summary>
/// <typeparam name="TAggregate"> The type of the aggregate root being managed. </typeparam>
/// <typeparam name="TKey"> The type of the key used to identify the aggregate root. </typeparam>
/// <remarks>
///     This class inherits from <see cref="AggregateRepositoryBase{TAggregate, TKey, IAggregateQuery{TAggregate}}" /> and provides default
///     support for <see cref="IAggregateQuery{TAggregate}" /> as the query type. It is intended to be used as a concrete base for
///     repository implementations where a simple query mechanism is sufficient.
/// </remarks>
public abstract class AggregateRepository<TAggregate, TKey>(IActivityContext context, IOutbox outbox)
	: AggregateRepositoryBase<TAggregate, TKey, IAggregateQuery<TAggregate>>(context, outbox)
	where TAggregate : class, IAggregateRoot<TKey>
{
}
