using System.Transactions;

using Excalibur.Application.Requests;
using Excalibur.Domain;

using MediatR;

namespace Excalibur.Application.Behaviors;

/// <summary>
///     Implements a pipeline behavior that wraps transactional requests in a <see cref="TransactionScope" />.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	private readonly IDomainDb _db;

	/// <summary>
	///     Initializes a new instance of the <see cref="TransactionBehavior{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="db"> The domain database instance used to manage database connections. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is null. </exception>
	public TransactionBehavior(IDomainDb db)
	{
		ArgumentNullException.ThrowIfNull(db);

		_db = db;
	}

	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		if (request is not IAmTransactional activity)
		{
			return await next().ConfigureAwait(false);
		}

		var transactionOptions = new TransactionOptions
		{
			Timeout = activity.TransactionTimeout,
			IsolationLevel = activity.TransactionIsolation
		};

		using var transaction = new TransactionScope(
			activity.TransactionBehavior,
			transactionOptions,
			TransactionScopeAsyncFlowOption.Enabled);

		try
		{
			_db.Open();

			var response = await next().ConfigureAwait(false);

			transaction.Complete();

			return response;
		}
		finally
		{
			_db.Close();
		}
	}
}
