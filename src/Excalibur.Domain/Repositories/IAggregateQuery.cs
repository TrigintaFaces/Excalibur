using Excalibur.Domain.Model;

namespace Excalibur.Domain.Repositories;

/// <summary>
///     Represents a query for retrieving aggregates of type <typeparamref name="TAggregate" />.
/// </summary>
/// <typeparam name="TAggregate"> The type of the aggregate being queried. Must implement <see cref="IAggregateRoot" />. </typeparam>
/// <remarks> This interface serves as a marker for query types used with repositories, enabling type-safe queries. </remarks>
public interface IAggregateQuery<TAggregate> where TAggregate : class, IAggregateRoot
{
}
