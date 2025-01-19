using Excalibur.Application.Requests.Validation;

namespace Excalibur.A3.Authorization.Requests.Validation;

/// <summary>
///     Provides a base validator for requests that may be authorizable.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks>
///     This validator automatically includes other relevant validators depending on the interfaces implemented by <typeparamref name="TRequest" />:
///     - If <typeparamref name="TRequest" /> implements <see cref="IAmAuthorizable" />, the <see cref="AuthorizableValidator{TRequest}" />
///     is included.
///     - If <typeparamref name="TRequest" /> implements <see cref="IAmAuthorizableForResource" />, the
///       <see cref="AuthorizableForResourceValidator{TRequest}" /> is included.
/// </remarks>
public abstract class AuthorizableTypeRequestValidator<TRequest> : RequestValidator<TRequest>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AuthorizableTypeRequestValidator{TRequest}" /> class.
	/// </summary>
	protected AuthorizableTypeRequestValidator()
		: base()
	{
		if (typeof(TRequest).IsAssignableTo(typeof(IAmAuthorizable)))
		{
			Include(new AuthorizableValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IAmAuthorizableForResource)))
		{
			Include(new AuthorizableForResourceValidator<TRequest>());
		}
	}
}
