// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationOptionsCoverageShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Act
        var options = new InputValidationOptions();

        // Assert
        options.EnableValidation.ShouldBeTrue();
        options.MaxStringLength.ShouldBeGreaterThan(0);
        options.MaxMessageSizeBytes.ShouldBeGreaterThan(0);
        options.MaxObjectDepth.ShouldBeGreaterThan(0);
        options.BlockControlCharacters.ShouldBeTrue();
        options.BlockHtmlContent.ShouldBeTrue();
        options.BlockSqlInjection.ShouldBeTrue();
        options.BlockNoSqlInjection.ShouldBeTrue();
        options.BlockCommandInjection.ShouldBeTrue();
        options.BlockPathTraversal.ShouldBeTrue();
        options.BlockLdapInjection.ShouldBeTrue();
    }

    [Fact]
    public void SetAllProperties()
    {
        // Act
        var options = new InputValidationOptions
        {
            EnableValidation = false,
            MaxStringLength = 500,
            MaxMessageSizeBytes = 1024,
            MaxObjectDepth = 3,
            BlockControlCharacters = false,
            BlockHtmlContent = false,
            BlockSqlInjection = false,
            BlockNoSqlInjection = false,
            BlockCommandInjection = false,
            BlockPathTraversal = false,
            BlockLdapInjection = false,
            AllowNullProperties = true,
            AllowEmptyStrings = true,
            RequireCorrelationId = false,
            FailOnValidatorException = false,
            MaxMessageAgeDays = 7,
        };

        // Assert
        options.EnableValidation.ShouldBeFalse();
        options.MaxStringLength.ShouldBe(500);
        options.MaxMessageSizeBytes.ShouldBe(1024);
        options.MaxObjectDepth.ShouldBe(3);
        options.BlockControlCharacters.ShouldBeFalse();
        options.BlockHtmlContent.ShouldBeFalse();
        options.BlockSqlInjection.ShouldBeFalse();
        options.BlockNoSqlInjection.ShouldBeFalse();
        options.BlockCommandInjection.ShouldBeFalse();
        options.BlockPathTraversal.ShouldBeFalse();
        options.BlockLdapInjection.ShouldBeFalse();
        options.AllowNullProperties.ShouldBeTrue();
        options.AllowEmptyStrings.ShouldBeTrue();
        options.RequireCorrelationId.ShouldBeFalse();
        options.FailOnValidatorException.ShouldBeFalse();
        options.MaxMessageAgeDays.ShouldBe(7);
    }

    [Fact]
    public void InputValidationExceptionWithErrors()
    {
        // Arrange
        var errors = new List<string> { "error1", "error2" };

        // Act
        var ex = new InputValidationException("Validation failed", errors);

        // Assert
        ex.Message.ShouldBe("Validation failed");
        ex.ValidationErrors.ShouldBe(errors);
    }

    [Fact]
    public void InputValidationResultValid()
    {
        // Act
        var result = InputValidationResult.Success();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void InputValidationResultInvalid()
    {
        // Act
        var result = InputValidationResult.Failure("field is required");

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("field is required");
    }
}
