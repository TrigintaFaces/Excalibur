using System.Transactions;

namespace Excalibur.Application.Requests;

/// <summary>
///     Represents an entity that participates in a transaction.
/// </summary>
public interface IAmTransactional
{
	/// <summary>
	///     Gets the transaction scope behavior for the entity.
	/// </summary>
	public TransactionScopeOption TransactionBehavior { get; }

	/// <summary>
	///     Gets the isolation level for the transaction associated with the entity.
	/// </summary>
	public IsolationLevel TransactionIsolation { get; }

	/// <summary>
	///     Gets the timeout duration for the transaction associated with the entity.
	/// </summary>
	public TimeSpan TransactionTimeout { get; }
}
