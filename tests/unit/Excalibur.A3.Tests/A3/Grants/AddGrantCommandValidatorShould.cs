using Excalibur.A3.Authorization.Grants;

using FluentValidation;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AddGrantCommandValidatorShould
{
	private readonly AddGrantCommandValidator _sut = new();

	[Fact]
	public void Pass_for_valid_command()
	{
		// Arrange
		var command = new AddGrantCommand("user-123", "John Doe", "ActivityGroup", "admin", null, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Fail_when_user_id_is_empty()
	{
		// Arrange
		var command = new AddGrantCommand(string.Empty, "John Doe", "ActivityGroup", "admin", null, Guid.NewGuid());

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
		var command = new AddGrantCommand("user-123", string.Empty, "ActivityGroup", "admin", null, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "FullName");
	}

	[Fact]
	public void Fail_when_grant_type_is_empty()
	{
		// Arrange
		var command = new AddGrantCommand("user-123", "John Doe", string.Empty, "admin", null, Guid.NewGuid());

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
		var command = new AddGrantCommand("user-123", "John Doe", "ActivityGroup", string.Empty, null, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Qualifier");
	}

	[Fact]
	public void Pass_when_expires_on_is_null()
	{
		// Arrange
		var command = new AddGrantCommand("user-123", "John Doe", "ActivityGroup", "admin", null, Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Pass_when_expires_on_is_in_the_future()
	{
		// Arrange
		var command = new AddGrantCommand("user-123", "John Doe", "ActivityGroup", "admin",
			DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Fail_when_expires_on_is_in_the_past()
	{
		// Arrange
		var command = new AddGrantCommand("user-123", "John Doe", "ActivityGroup", "admin",
			DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid());

		// Act
		var result = _sut.Validate(command);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "ExpiresOn");
	}
}
