using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Requests.Validation;

/// <summary>
///     Validates that an object implementing <see cref="IAmAuthorizableForResource" /> contains the required resource-related information
///     for authorization.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks> Ensures that the resource identifier and resource types are populated and valid. </remarks>
public class AuthorizableForResourceValidator<TRequest> : RulesFor<TRequest, IAmAuthorizableForResource>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizableForResourceValidator{TRequest}" /> class.
	/// </summary>
	public AuthorizableForResourceValidator()
	{
		_ = RuleFor(x => x.ResourceId).NotEmpty();
		_ = RuleFor(x => x.ResourceTypes).NotEmpty();
		_ = RuleForEach(x => x.ResourceTypes).NotEmpty();
	}
}
