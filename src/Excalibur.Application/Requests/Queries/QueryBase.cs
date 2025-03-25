using System.Transactions;

namespace Excalibur.Application.Requests.Queries;

/// <summary>
///     Provides a base implementation for queries in the system.
/// </summary>
/// <typeparam name="TResponse"> The type of response the query produces. </typeparam>
public abstract class QueryBase<TResponse> : IQuery<TResponse>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="QueryBase{TResponse}" /> class with the specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID for the query. </param>
	/// <param name="tenantId"> The tenant ID associated with the query. Defaults to "NotSpecified" if not provided. </param>
	protected QueryBase(Guid correlationId, string? tenantId = null)
	{
		CorrelationId = correlationId;
		TenantId = tenantId ?? "NotSpecified";
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="QueryBase{TResponse}" /> class with default values.
	/// </summary>
	protected QueryBase() : this(Guid.Empty)
	{
	}

	/// <inheritdoc />
	public ActivityType ActivityType => ActivityType.Query;

	/// <inheritdoc />
	public string ActivityName => GetType().Name;

	/// <inheritdoc />
	public abstract string ActivityDisplayName { get; }

	/// <inheritdoc />
	public abstract string ActivityDescription { get; }

	/// <inheritdoc />
	public Guid CorrelationId { get; protected init; }

	/// <inheritdoc />
	public string? TenantId { get; protected init; }

	/// <inheritdoc />
	public TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <inheritdoc />
	public IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <inheritdoc />
	public TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(2);
}
