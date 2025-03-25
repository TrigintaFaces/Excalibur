namespace Excalibur.Application.Requests.Validation;

/// <summary>
///     Validator for <see cref="IAmCorrelatable" /> properties. Ensures the correlation ID is valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public class CorrelationValidator<TRequest> : RulesFor<TRequest, IAmCorrelatable>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="CorrelationValidator{TRequest}" /> class.
	/// </summary>
	public CorrelationValidator()
	{
		_ = RuleFor(x => x.CorrelationId).IsValidCorrelationId();
	}
}
