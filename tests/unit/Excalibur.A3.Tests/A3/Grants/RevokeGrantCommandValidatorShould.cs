using Excalibur.A3.Authorization.Grants;

using FluentValidation;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class RevokeGrantCommandValidatorShould
{
	private readonly RevokeGrantCommandValidator _sut = new();

	[Fact]
	public void Pass_for_valid_command()
	{
		// Arrange
		var command = new RevokeGrantCommand("user-123", "ActivityGroup", "admin", Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Fail_when_user_id_is_empty()
	{
		// Arrange
		var command = new RevokeGrantCommand(string.Empty, "ActivityGroup", "admin", Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "UserId");
	}

	[Fact]
	public void Fail_when_grant_type_is_empty()
	{
		// Arrange
		var command = new RevokeGrantCommand("user-123", string.Empty, "admin", Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "GrantType");
	}

	[Fact]
	public void Fail_when_qualifier_is_empty()
	{
		// Arrange
		var command = new RevokeGrantCommand("user-123", "ActivityGroup", string.Empty, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Qualifier");
	}
}
