using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.AddGrant;

/// <summary>
///     Validates the <see cref="AddGrantCommand" /> to ensure it contains valid data before processing.
/// </summary>
public class AddGrantCommandValidator : RequestValidator<AddGrantCommand>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="AddGrantCommandValidator" /> class.
	/// </summary>
	public AddGrantCommandValidator()
	{
		// Ensure ExpiresOn is either null or a future timestamp.
		_ = RuleFor(x => x.ExpiresOn)
			.Must(x => x == null || (x.GetValueOrDefault().ToUniversalTime() >= DateTimeOffset.UtcNow))
			.WithMessage("{propertyName} must be null or in the future.");

		// Validate that FullName is not empty.
		_ = RuleFor(x => x.FullName).NotEmpty();

		// Validate that GrantType is not empty.
		_ = RuleFor(x => x.GrantType).NotEmpty();

		// Validate that Qualifier is not empty.
		_ = RuleFor(x => x.Qualifier).NotEmpty();

		// Validate that UserId is not empty.
		_ = RuleFor(x => x.UserId).NotEmpty();
	}
}
