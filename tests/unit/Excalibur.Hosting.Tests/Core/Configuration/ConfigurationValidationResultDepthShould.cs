using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Core.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ConfigurationValidationResultDepthShould
{
    [Fact]
    public void Success_ReturnValidResult()
    {
        // Act
        var result = ConfigurationValidationResult.Success();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_WithMessage_ReturnInvalidResult()
    {
        // Act
        var result = ConfigurationValidationResult.Failure("Connection string missing");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldBe("Connection string missing");
        result.Errors[0].ConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Failure_WithMessageAndPath_ReturnInvalidResult()
    {
        // Act
        var result = ConfigurationValidationResult.Failure("Invalid value", "Database:ConnectionString");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Message.ShouldBe("Invalid value");
        result.Errors[0].ConfigurationPath.ShouldBe("Database:ConnectionString");
    }

    [Fact]
    public void Failure_WithMultipleErrors_ReturnAllErrors()
    {
        // Arrange
        var errors = new List<ConfigurationValidationError>
        {
            new("Error 1", "Path1"),
            new("Error 2", "Path2"),
            new("Error 3"),
        };

        // Act
        var result = ConfigurationValidationResult.Failure(errors);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(3);
    }

    [Fact]
    public void Combine_AllValid_ReturnSuccess()
    {
        // Arrange
        var r1 = ConfigurationValidationResult.Success();
        var r2 = ConfigurationValidationResult.Success();

        // Act
        var combined = ConfigurationValidationResult.Combine(r1, r2);

        // Assert
        combined.IsValid.ShouldBeTrue();
        combined.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Combine_MixedResults_ReturnFailureWithAllErrors()
    {
        // Arrange
        var r1 = ConfigurationValidationResult.Success();
        var r2 = ConfigurationValidationResult.Failure("Error A");
        var r3 = ConfigurationValidationResult.Failure("Error B", "SomePath");

        // Act
        var combined = ConfigurationValidationResult.Combine(r1, r2, r3);

        // Assert
        combined.IsValid.ShouldBeFalse();
        combined.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void Combine_AllInvalid_ReturnAllErrors()
    {
        // Arrange
        var r1 = ConfigurationValidationResult.Failure("E1");
        var r2 = ConfigurationValidationResult.Failure("E2");

        // Act
        var combined = ConfigurationValidationResult.Combine(r1, r2);

        // Assert
        combined.IsValid.ShouldBeFalse();
        combined.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void Combine_EmptyArray_ReturnSuccess()
    {
        // Act
        var combined = ConfigurationValidationResult.Combine();

        // Assert
        combined.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullErrors_DefaultToEmptyList()
    {
        // Act
        var result = new ConfigurationValidationResult(true, null);

        // Assert
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
    }
}
