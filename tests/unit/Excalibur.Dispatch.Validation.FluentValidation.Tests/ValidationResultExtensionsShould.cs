// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;

using ValidationResult = global::FluentValidation.Results.ValidationResult;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationResultExtensionsShould
{
    // ---- ToDispatchResult ----

    [Fact]
    public void ThrowWhenFluentResultIsNull()
    {
        // Act & Assert
        ValidationResult? nullResult = null;
        Should.Throw<ArgumentNullException>(() => nullResult!.ToDispatchResult());
    }

    [Fact]
    public void ReturnSuccessResultForValidFluentResult()
    {
        // Arrange
        var fluentResult = new ValidationResult();

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnFailedResultForInvalidFluentResult()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Name", "Name is required"),
        ]);

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
    }

    [Fact]
    public void PreservePropertyNameInDispatchResult()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Email", "Invalid email format"),
        ]);

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        var error = result.Errors.First().ShouldBeOfType<Excalibur.Dispatch.Abstractions.Validation.ValidationError>();
        error.PropertyName.ShouldBe("Email");
    }

    [Fact]
    public void PreserveErrorMessageInDispatchResult()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Name", "Name cannot be empty"),
        ]);

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        var error = result.Errors.First().ShouldBeOfType<Excalibur.Dispatch.Abstractions.Validation.ValidationError>();
        error.Message.ShouldBe("Name cannot be empty");
    }

    [Fact]
    public void PreserveErrorCodeInDispatchResult()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Name", "Name is required") { ErrorCode = "NotEmptyValidator" },
        ]);

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        var error = result.Errors.First().ShouldBeOfType<Excalibur.Dispatch.Abstractions.Validation.ValidationError>();
        error.ErrorCode.ShouldBe("NotEmptyValidator");
    }

    [Fact]
    public void MapMultipleErrorsCorrectly()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Name", "Name is required"),
            new global::FluentValidation.Results.ValidationFailure("Email", "Email is invalid"),
            new global::FluentValidation.Results.ValidationFailure("Age", "Age must be positive"),
        ]);

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(3);
    }

    [Fact]
    public void ReturnSerializableValidationResultType()
    {
        // Arrange
        var fluentResult = new ValidationResult();

        // Act
        var result = fluentResult.ToDispatchResult();

        // Assert
        result.ShouldBeOfType<SerializableValidationResult>();
    }

    // ---- ToExcaliburResult (alias) ----

    [Fact]
    public void ReturnSameResultAsToDispatchResult()
    {
        // Arrange
        var fluentResult = new ValidationResult(
        [
            new global::FluentValidation.Results.ValidationFailure("Field", "Error"),
        ]);

        // Act
        var dispatch = fluentResult.ToDispatchResult();
        var excalibur = fluentResult.ToExcaliburResult();

        // Assert
        dispatch.IsValid.ShouldBe(excalibur.IsValid);
        dispatch.Errors.Count.ShouldBe(excalibur.Errors.Count);
    }

    // ---- ValidateWith<TMessage, TValidator> ----

    [Fact]
    public void ReturnSuccessForValidMessageWithSyncValidation()
    {
        // Arrange
        var message = new ValidationResultExtensionsTestMessage { Name = "Valid" };

        // Act — access properties directly to avoid CS8920 with IValidationResult
        var result = message.ValidateWith<ValidationResultExtensionsTestMessage, ValidationResultExtensionsTestMessageValidator>();

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ReturnFailedForInvalidMessageWithSyncValidation()
    {
        // Arrange
        var message = new ValidationResultExtensionsTestMessage { Name = "" };

        // Act
        var result = message.ValidateWith<ValidationResultExtensionsTestMessage, ValidationResultExtensionsTestMessageValidator>();

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    // ---- ValidateWithAsync<TMessage, TValidator> ----

    [Fact]
    public async Task ReturnSuccessForValidMessageWithAsyncValidation()
    {
        // Arrange
        var message = new ValidationResultExtensionsTestMessage { Name = "Valid" };

        // Act
        var result = await message.ValidateWithAsync<ValidationResultExtensionsTestMessage, ValidationResultExtensionsTestMessageValidator>(
            CancellationToken.None);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnFailedForInvalidMessageWithAsyncValidation()
    {
        // Arrange
        var message = new ValidationResultExtensionsTestMessage { Name = "" };

        // Act
        var result = await message.ValidateWithAsync<ValidationResultExtensionsTestMessage, ValidationResultExtensionsTestMessageValidator>(
            CancellationToken.None);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ReturnSerializableValidationResultFromAsyncValidation()
    {
        // Arrange
        var message = new ValidationResultExtensionsTestMessage { Name = "Test" };

        // Act
        var result = await message.ValidateWithAsync<ValidationResultExtensionsTestMessage, ValidationResultExtensionsTestMessageValidator>(
            CancellationToken.None);

        // Assert
        result.ShouldBeOfType<SerializableValidationResult>();
    }
}

// ---- Test Infrastructure (file-level to avoid CA1034) ----

internal sealed class ValidationResultExtensionsTestMessage : IDispatchMessage
{
    public string Name { get; set; } = "";
}

internal sealed class ValidationResultExtensionsTestMessageValidator : AbstractValidator<ValidationResultExtensionsTestMessage>
{
    public ValidationResultExtensionsTestMessageValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
