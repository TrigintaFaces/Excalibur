using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Requests.Validation;

/// <summary>
///     Validates that an object implementing <see cref="IAmAuthorizable" /> contains the necessary access token for authorization.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks> Ensures that the access token is provided and is not empty. </remarks>
public class AuthorizableValidator<TRequest> : RulesFor<TRequest, IAmAuthorizable>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizableValidator{TRequest}" /> class.
	/// </summary>
	public AuthorizableValidator()
	{
		_ = RuleFor(x => x.AccessToken).NotEmpty();
	}
}
