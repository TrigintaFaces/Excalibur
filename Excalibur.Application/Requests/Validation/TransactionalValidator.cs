using System.Transactions;

using FluentValidation;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
///     Validator for <see cref="IAmTransactional" /> properties. Ensures transaction settings such as timeout and isolation level are valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public class TransactionalValidator<TRequest> : RulesFor<TRequest, IAmTransactional>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="TransactionalValidator{TRequest}" /> class.
	/// </summary>
	public TransactionalValidator()
	{
		_ = RuleFor(x => x.TransactionTimeout).NotEmpty();
		_ = RuleFor(x => x.TransactionIsolation)
			.Must(i => i is not IsolationLevel.Unspecified)
			.WithMessage("{PropertyName} must be specified.");
	}
}
