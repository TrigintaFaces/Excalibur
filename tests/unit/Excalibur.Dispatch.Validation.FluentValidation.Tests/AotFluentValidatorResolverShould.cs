// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AotFluentValidatorResolverShould
{
    [Fact]
    public void ThrowArgumentNullExceptionWhenProviderIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new AotFluentValidatorResolver(null!));
    }

    [Fact]
    public void ThrowArgumentNullExceptionWhenMessageIsNull()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => sut.TryValidate(null!));
    }

    [Fact]
    public void ThrowInvalidOperationExceptionWhenNoDispatcherRegistered()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "test" };

        // Act & Assert
        // AOT resolver throws because no IAotValidationDispatcher is registered
        Should.Throw<InvalidOperationException>(() => sut.TryValidate(message));
    }

    [Fact]
    public void IncludeHelpfulMessageInInvalidOperationException()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "test" };

        // Act
        var ex = Should.Throw<InvalidOperationException>(() => sut.TryValidate(message));

        // Assert
        ex.Message.ShouldContain("SourceGenerators");
    }

    [Fact]
    public void ImplementIValidatorResolverInterface()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();

        // Act
        var sut = new AotFluentValidatorResolver(provider);

        // Assert
        sut.ShouldBeAssignableTo<IValidatorResolver>();
    }

    [Fact]
    public void ReturnSuccessWhenDispatcherRegisteredAndValidationPasses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AotResolverTestMessage>, AotResolverTestMessageValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new AotTestDispatcher());
        var provider = services.BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "ValidName" };

        // Act
        var result = sut.TryValidate(message);

        // Assert
        result!.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnFailedResultWhenDispatcherRegisteredAndValidationFails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AotResolverTestMessage>, AotResolverTestMessageValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new AotTestDispatcher());
        var provider = services.BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "" };

        // Act
        var result = sut.TryValidate(message);

        // Assert
        result!.IsValid.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void ReturnNullWhenDispatcherRegisteredButNoValidatorsForMessageType()
    {
        // Arrange — dispatcher registered but no IValidator<AotResolverUnknownMessage>
        var services = new ServiceCollection();
        services.AddSingleton<IAotValidationDispatcher>(new AotTestDispatcher());
        var provider = services.BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverUnknownMessage();

        // Act
        var result = sut.TryValidate(message);

        // Assert — dispatcher returns null for unrecognized message types
        (result is null).ShouldBeTrue();
    }

    [Fact]
    public void ReturnSerializableValidationResultType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AotResolverTestMessage>, AotResolverTestMessageValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new AotTestDispatcher());
        var provider = services.BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "" };

        // Act
        var result = sut.TryValidate(message);

        // Assert
        result.ShouldBeOfType<SerializableValidationResult>();
    }
}

// ---- Test Infrastructure (file-level to avoid CA1034) ----

internal sealed class AotResolverTestMessage : IDispatchMessage
{
    public string Name { get; set; } = "";
}

internal sealed class AotResolverUnknownMessage : IDispatchMessage;

internal sealed class AotResolverTestMessageValidator : AbstractValidator<AotResolverTestMessage>
{
    public AotResolverTestMessageValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

/// <summary>
/// Hand-written AOT dispatcher for testing (mirrors source-generated output).
/// </summary>
internal sealed class AotTestDispatcher : IAotValidationDispatcher
{
    public IValidationResult? TryValidate(IDispatchMessage message, IServiceProvider provider)
    {
        return message switch
        {
            AotResolverTestMessage m => ValidateTyped(m, provider),
            _ => null
        };
    }

    private static IValidationResult? ValidateTyped<TMessage>(TMessage message, IServiceProvider provider)
        where TMessage : IDispatchMessage
    {
        var validators = ServiceProviderServiceExtensions
            .GetServices<IValidator<TMessage>>(provider)
            .ToArray();

        if (validators.Length == 0)
        {
            return null;
        }

        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = validator.Validate(message);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count == 0)
        {
            return SerializableValidationResult.Success();
        }

        var errors = new object[failures.Count];
        for (var i = 0; i < failures.Count; i++)
        {
            var f = failures[i];
            errors[i] = new ValidationError(f.PropertyName, f.ErrorMessage) { ErrorCode = f.ErrorCode };
        }

        return SerializableValidationResult.Failed(errors);
    }
}
