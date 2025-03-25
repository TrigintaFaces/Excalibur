using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeGrant;

/// <summary>
///     Validates the <see cref="RevokeGrantCommand" /> to ensure that all required fields are provided and valid.
/// </summary>
public class RevokeGrantCommandValidator : RequestValidator<RevokeGrantCommand>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="RevokeGrantCommandValidator" /> class.
	/// </summary>
	public RevokeGrantCommandValidator()
	{
		// Ensure the GrantType property is not empty
		_ = RuleFor(x => x.GrantType).NotEmpty();

		// Ensure the Qualifier property is not empty
		_ = RuleFor(x => x.Qualifier).NotEmpty();

		// Ensure the UserId property is not empty
		_ = RuleFor(x => x.UserId).NotEmpty();
	}
}
