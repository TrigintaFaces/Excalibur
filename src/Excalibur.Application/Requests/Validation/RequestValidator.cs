using FluentValidation;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
///     Base class for validating requests. Dynamically includes validators based on implemented interfaces.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public abstract class RequestValidator<TRequest> : AbstractValidator<TRequest>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="RequestValidator{TRequest}" /> class. Includes validators for supported interfaces
	///     such as <see cref="IAmMultiTenant" />, <see cref="IAmCorrelatable" />, <see cref="IActivity" />, and <see cref="IAmTransactional" />.
	/// </summary>
	protected RequestValidator()
	{
		if (typeof(TRequest).IsAssignableTo(typeof(IAmMultiTenant)))
		{
			Include(new MultiTenantValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IAmCorrelatable)))
		{
			Include(new CorrelationValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IActivity)))
		{
			Include(new ActivityValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IAmTransactional)))
		{
			Include(new TransactionalValidator<TRequest>());
		}
	}
}
