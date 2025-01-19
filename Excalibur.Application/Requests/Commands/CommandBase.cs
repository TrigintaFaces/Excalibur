using System.Transactions;

using MediatR;

namespace Excalibur.Application.Requests.Commands;

/// <summary>
///     Represents the base class for commands that do not return a value.
/// </summary>
public abstract class CommandBase : CommandBase<Unit>, IRequest
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CommandBase" /> class with a specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID associated with the command. </param>
	/// <param name="tenantId"> The tenant ID associated with the command. Defaults to null. </param>
	protected CommandBase(Guid correlationId, string? tenantId = null)
		: base(correlationId, tenantId)
	{
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="CommandBase" /> class with default values.
	/// </summary>
	protected CommandBase()
	{
	}
}

/// <summary>
///     Represents the base class for commands with a specific response type.
/// </summary>
/// <typeparam name="TResponse"> The type of the response returned by the command. </typeparam>
public abstract class CommandBase<TResponse> : ICommand<TResponse>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CommandBase{TResponse}" /> class with a specified correlation ID and tenant ID.
	/// </summary>
	/// <param name="correlationId"> The correlation ID associated with the command. </param>
	/// <param name="tenantId"> The tenant ID associated with the command. Defaults to "NotSpecified" if null. </param>
	protected CommandBase(Guid correlationId, string? tenantId = null)
	{
		CorrelationId = correlationId;
		TenantId = tenantId ?? "NotSpecified";
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="CommandBase{TResponse}" /> class.
	/// </summary>
	protected CommandBase() : this(Guid.Empty)
	{
	}

	/// <inheritdoc />
	ActivityType IActivity.ActivityType => ActivityType.Command;

	/// <inheritdoc />
	public string ActivityName => $"{GetType().Namespace}:{GetType().Name}";

	/// <inheritdoc />
	public abstract string ActivityDisplayName { get; }

	/// <inheritdoc />
	public abstract string ActivityDescription { get; }

	/// <inheritdoc />
	public Guid CorrelationId { get; protected init; }

	/// <inheritdoc />
	public string TenantId { get; protected init; }

	/// <inheritdoc />
	public virtual TransactionScopeOption TransactionBehavior { get; protected internal init; } = TransactionScopeOption.Required;

	/// <inheritdoc />
	public virtual IsolationLevel TransactionIsolation { get; protected internal init; } = IsolationLevel.ReadCommitted;

	/// <inheritdoc />
	public virtual TimeSpan TransactionTimeout { get; protected internal init; } = TimeSpan.FromMinutes(1);
}
