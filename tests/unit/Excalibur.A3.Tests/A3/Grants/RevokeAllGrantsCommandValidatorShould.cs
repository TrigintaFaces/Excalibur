using Excalibur.A3.Authorization.Grants;

using FluentValidation;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class RevokeAllGrantsCommandValidatorShould
{
	private readonly RevokeAllGrantsCommandValidator _sut = new();

	[Fact]
	public void Pass_for_valid_command()
	{
		// Arrange
		var command = new RevokeAllGrantsCommand("user-123", "John Doe", Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Fail_when_user_id_is_empty()
	{
		// Arrange
		var command = new RevokeAllGrantsCommand(string.Empty, "John Doe", Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "UserId");
	}

	[Fact]
	public void Fail_when_full_name_is_empty()
	{
		// Arrange
		var command = new RevokeAllGrantsCommand("user-123", string.Empty, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "FullName");
	}
}
