using Excalibur.A3.Audit;

namespace Excalibur.Tests.A3.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuditableResultShould
{
	[Fact]
	public void Store_result_value()
	{
		// Arrange & Act
		var result = new AuditableResult<int>(42);

		// Assert
		result.Result.ShouldBe(42);
	}

	[Fact]
	public void Store_audit_message()
	{
		// Arrange & Act
		var result = new AuditableResult<int>(42, "Operation succeeded");

		// Assert
		result.AuditMessage.ShouldBe("Operation succeeded");
	}

	[Fact]
	public void Have_null_audit_message_by_default()
	{
		// Arrange & Act
		var result = new AuditableResult<int>(42);

		// Assert
		result.AuditMessage.ShouldBeNull();
	}

	[Fact]
	public void Return_audit_message_from_ToString_when_set()
	{
		// Arrange
		var result = new AuditableResult<int>(42, "Custom message");

		// Act & Assert
		result.ToString().ShouldBe("Custom message");
	}

	[Fact]
	public void Return_result_string_from_ToString_when_no_audit_message()
	{
		// Arrange
		var result = new AuditableResult<int>(42);

		// Act & Assert
		result.ToString().ShouldBe("42");
	}

	[Fact]
	public void Return_empty_from_ToString_when_result_is_null_and_no_audit_message()
	{
		// Arrange
		var result = new AuditableResult<string?>(null);

		// Act & Assert
		result.ToString().ShouldBe(string.Empty);
	}

	[Fact]
	public void Return_result_from_ToString_when_audit_message_is_empty()
	{
		// Arrange
		var result = new AuditableResult<int>(42, string.Empty);

		// Act & Assert
		result.ToString().ShouldBe("42");
	}

	[Fact]
	public void Allow_setting_result()
	{
		// Arrange
		var result = new AuditableResult<int>(42);

		// Act
		result.Result = 99;

		// Assert
		result.Result.ShouldBe(99);
	}

	[Fact]
	public void Allow_setting_audit_message()
	{
		// Arrange
		var result = new AuditableResult<int>(42);

		// Act
		result.AuditMessage = "Updated message";

		// Assert
		result.AuditMessage.ShouldBe("Updated message");
	}

	[Fact]
	public void Work_with_boolean_result_type()
	{
		// Arrange & Act
		var result = new AuditableResult<bool>(true, "Grant added");

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldBe("Grant added");
	}

	[Fact]
	public void Work_with_complex_result_type()
	{
		// Arrange
		var data = new { Id = 1, Name = "test" };

		// Act
		var result = new AuditableResult<object>(data);

		// Assert
		result.Result.ShouldBeSameAs(data);
	}
}
