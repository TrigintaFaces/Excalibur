using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants.Application.Commands.RevokeAllGrants;

/// <summary>
///     Validates the <see cref="RevokeAllGrantsCommand" /> to ensure all required properties are provided.
/// </summary>
public class RevokeAllGrantsCommandValidator : RequestValidator<RevokeAllGrantsCommand>
{
	/// <summary>
	///     Initializes a new instance of the <see cref="RevokeAllGrantsCommandValidator" /> class.
	/// </summary>
	/// <remarks>
	///     Adds validation rules for the <see cref="RevokeAllGrantsCommand" /> to ensure that the
	///     <see cref="RevokeAllGrantsCommand.UserId" /> and <see cref="RevokeAllGrantsCommand.FullName" /> properties are not empty.
	/// </remarks>
	public RevokeAllGrantsCommandValidator()
	{
		// Validate that the FullName property is not empty
		_ = RuleFor(x => x.FullName).NotEmpty();

		// Validate that the UserId property is not empty
		_ = RuleFor(x => x.UserId).NotEmpty();
	}
}
