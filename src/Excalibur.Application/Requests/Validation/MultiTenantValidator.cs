namespace Excalibur.Application.Requests.Validation;

/// <summary>
///     Validator for <see cref="IAmMultiTenant" /> properties. Ensures the tenant ID is valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public class MultiTenantValidator<TRequest> : RulesFor<TRequest, IAmMultiTenant>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="MultiTenantValidator{TRequest}" /> class.
	/// </summary>
	public MultiTenantValidator()
	{
		_ = RuleFor(x => x.TenantId).IsValidTenantId();
	}
}
