// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FluentValidatorResolverShould
{
    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () => await sut.TryValidateAsync(null!));
    }

    [Fact]
    public async Task ReturnNullWhenNoValidatorsRegistered()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "test" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — avoid CS8920 by not calling Shouldly on IValidationResult? directly
        (result is null).ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnSuccessWhenValidatorPassesAllRules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "ValidName" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result!.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnFailedResultWhenValidationFails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result!.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ReturnAllValidationErrorsFromSingleValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverStrictTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "", Value = -1 };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result!.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task AggregateErrorsFromMultipleValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverAdditionalTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        // Name is empty (fails first validator) and Value is -1 (fails second validator)
        var message = new FluentResolverTestMessage { Name = "", Value = -1 };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result!.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CacheValidatorTypeAcrossMultipleCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "Valid" };

        // Act - call multiple times to exercise cache
        var result1 = await sut.TryValidateAsync(message);
        var result2 = await sut.TryValidateAsync(message);
        var result3 = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result1!.IsValid.ShouldBeTrue();
        result2!.IsValid.ShouldBeTrue();
        result3!.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleDifferentMessageTypesIndependently()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        services.AddSingleton<IValidator<FluentResolverAnotherTestMessage>, FluentResolverAnotherTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);

        var validMessage = new FluentResolverTestMessage { Name = "Valid" };
        var invalidOther = new FluentResolverAnotherTestMessage { Code = "" };

        // Act
        var result1 = await sut.TryValidateAsync(validMessage);
        var result2 = await sut.TryValidateAsync(invalidOther);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result1!.IsValid.ShouldBeTrue();

        result2!.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnSerializableValidationResultType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert
        result.ShouldBeOfType<SerializableValidationResult>();
    }

    [Fact]
    public async Task IncludePropertyNameInValidationErrors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert — access properties directly to avoid CS8920 with IValidationResult
        result!.IsValid.ShouldBeFalse();

        var errors = result.Errors.ToList();
        errors.ShouldNotBeEmpty();
        var firstError = errors[0].ShouldBeOfType<Excalibur.Dispatch.Validation.ValidationError>();
        firstError.PropertyName.ShouldBe("Name");
    }

    [Fact]
    public async Task ReturnSuccessResultTypeWhenAllValidatorsPass()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentResolverTestMessage>, FluentResolverTestMessageValidator>();
        var provider = services.BuildServiceProvider();
        var sut = new FluentValidatorResolver(provider);
        var message = new FluentResolverTestMessage { Name = "ValidName" };

        // Act
        var result = await sut.TryValidateAsync(message);

        // Assert
        var svr = result.ShouldBeOfType<SerializableValidationResult>();
        svr.IsValid.ShouldBeTrue();
        svr.Errors.ShouldBeEmpty();
    }
}

// ---- Test Infrastructure (file-level to avoid CA1034) ----

internal sealed class FluentResolverTestMessage : IDispatchMessage
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

internal sealed class FluentResolverAnotherTestMessage : IDispatchMessage
{
    public string Code { get; set; } = "";
}

internal sealed class FluentResolverTestMessageValidator : AbstractValidator<FluentResolverTestMessage>
{
    public FluentResolverTestMessageValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

internal sealed class FluentResolverStrictTestMessageValidator : AbstractValidator<FluentResolverTestMessage>
{
    public FluentResolverStrictTestMessageValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

internal sealed class FluentResolverAdditionalTestMessageValidator : AbstractValidator<FluentResolverTestMessage>
{
    public FluentResolverAdditionalTestMessageValidator()
    {
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

internal sealed class FluentResolverAnotherTestMessageValidator : AbstractValidator<FluentResolverAnotherTestMessage>
{
    public FluentResolverAnotherTestMessageValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}
