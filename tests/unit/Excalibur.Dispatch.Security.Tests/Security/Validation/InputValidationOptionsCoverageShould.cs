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
        options.InjectionPrevention.BlockControlCharacters.ShouldBeTrue();
        options.InjectionPrevention.BlockHtmlContent.ShouldBeTrue();
        options.InjectionPrevention.BlockSqlInjection.ShouldBeTrue();
        options.InjectionPrevention.BlockNoSqlInjection.ShouldBeTrue();
        options.InjectionPrevention.BlockCommandInjection.ShouldBeTrue();
        options.InjectionPrevention.BlockPathTraversal.ShouldBeTrue();
        options.InjectionPrevention.BlockLdapInjection.ShouldBeTrue();
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
            InjectionPrevention = new InputInjectionPreventionOptions
            {
                BlockControlCharacters = false,
                BlockHtmlContent = false,
                BlockSqlInjection = false,
                BlockNoSqlInjection = false,
                BlockCommandInjection = false,
                BlockPathTraversal = false,
                BlockLdapInjection = false,
            },
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
        options.InjectionPrevention.BlockControlCharacters.ShouldBeFalse();
        options.InjectionPrevention.BlockHtmlContent.ShouldBeFalse();
        options.InjectionPrevention.BlockSqlInjection.ShouldBeFalse();
        options.InjectionPrevention.BlockNoSqlInjection.ShouldBeFalse();
        options.InjectionPrevention.BlockCommandInjection.ShouldBeFalse();
        options.InjectionPrevention.BlockPathTraversal.ShouldBeFalse();
        options.InjectionPrevention.BlockLdapInjection.ShouldBeFalse();
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
