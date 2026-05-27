// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

/// <summary>
/// Dual-path tests proving JIT (<see cref="FluentValidatorResolver"/>) and AOT
/// (<see cref="AotFluentValidatorResolver"/>) resolvers produce identical results
/// for the same input messages and validators.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DualPathValidationShould
{
    [Fact]
    public void ProduceSameSuccessResultFromBothPaths()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<DualPathTestCommand>, DualPathTestCommandValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new TestAotValidationDispatcher());
        var provider = services.BuildServiceProvider();

        var jitResolver = new FluentValidatorResolver(provider);
        var aotResolver = new AotFluentValidatorResolver(provider);

        var message = new DualPathTestCommand { Name = "ValidName", Value = 10 };

        // Act
        var jitResult = jitResolver.TryValidate(message);
        var aotResult = aotResolver.TryValidate(message);

        // Assert — both should report success
        jitResult!.IsValid.ShouldBeTrue();
        aotResult!.IsValid.ShouldBeTrue();

        jitResult.Errors.ShouldBeEmpty();
        aotResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void ProduceSameFailureResultFromBothPaths()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<DualPathTestCommand>, DualPathTestCommandValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new TestAotValidationDispatcher());
        var provider = services.BuildServiceProvider();

        var jitResolver = new FluentValidatorResolver(provider);
        var aotResolver = new AotFluentValidatorResolver(provider);

        var message = new DualPathTestCommand { Name = "", Value = -1 };

        // Act
        var jitResult = jitResolver.TryValidate(message);
        var aotResult = aotResolver.TryValidate(message);

        // Assert — both should report failure with the same error count
        jitResult!.IsValid.ShouldBeFalse();
        aotResult!.IsValid.ShouldBeFalse();

        jitResult.Errors.Count.ShouldBe(aotResult.Errors.Count);
    }

    [Fact]
    public void ProduceSameNullResultForUnregisteredMessageType()
    {
        // Arrange — no validators for DualPathUnknownCommand
        var services = new ServiceCollection();
        services.AddSingleton<IAotValidationDispatcher>(new TestAotValidationDispatcher());
        var provider = services.BuildServiceProvider();

        var jitResolver = new FluentValidatorResolver(provider);
        var aotResolver = new AotFluentValidatorResolver(provider);

        var message = new DualPathUnknownCommand { Code = "abc" };

        // Act
        var jitResult = jitResolver.TryValidate(message);
        var aotResult = aotResolver.TryValidate(message);

        // Assert — both should return null (no validators)
        (jitResult is null).ShouldBeTrue();
        (aotResult is null).ShouldBeTrue();
    }

    [Fact]
    public void ProduceSameErrorPropertyNamesFromBothPaths()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<DualPathTestCommand>, DualPathTestCommandValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new TestAotValidationDispatcher());
        var provider = services.BuildServiceProvider();

        var jitResolver = new FluentValidatorResolver(provider);
        var aotResolver = new AotFluentValidatorResolver(provider);

        var message = new DualPathTestCommand { Name = "", Value = -1 };

        // Act
        var jitResult = jitResolver.TryValidate(message);
        var aotResult = aotResolver.TryValidate(message);

        // Assert — both should include the same property names in errors
        var jitProps = jitResult!.Errors.Cast<ValidationError>().Select(e => e.PropertyName).OrderBy(p => p).ToArray();
        var aotProps = aotResult!.Errors.Cast<ValidationError>().Select(e => e.PropertyName).OrderBy(p => p).ToArray();

        jitProps.ShouldBe(aotProps);
    }

    [Fact]
    public void ReturnSerializableValidationResultFromBothPaths()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<DualPathTestCommand>, DualPathTestCommandValidator>();
        services.AddSingleton<IAotValidationDispatcher>(new TestAotValidationDispatcher());
        var provider = services.BuildServiceProvider();

        var jitResolver = new FluentValidatorResolver(provider);
        var aotResolver = new AotFluentValidatorResolver(provider);

        var message = new DualPathTestCommand { Name = "", Value = 5 };

        // Act
        var jitResult = jitResolver.TryValidate(message);
        var aotResult = aotResolver.TryValidate(message);

        // Assert — both return SerializableValidationResult
        jitResult.ShouldBeOfType<SerializableValidationResult>();
        aotResult.ShouldBeOfType<SerializableValidationResult>();
    }
}

// ---- Test infrastructure (file-level to avoid CA1034) ----

internal sealed class DualPathTestCommand : IDispatchMessage
{
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

internal sealed class DualPathUnknownCommand : IDispatchMessage
{
    public string Code { get; set; } = "";
}

internal sealed class DualPathTestCommandValidator : AbstractValidator<DualPathTestCommand>
{
    public DualPathTestCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Hand-written AOT dispatcher that mirrors what the source generator produces.
/// Maps known message types to strongly-typed validator resolution without reflection.
/// </summary>
internal sealed class TestAotValidationDispatcher : IAotValidationDispatcher
{
    public IValidationResult? TryValidate(IDispatchMessage message, IServiceProvider provider)
    {
        return message switch
        {
            DualPathTestCommand m => ValidateTyped(m, provider),
            _ => null
        };
    }

    private static IValidationResult? ValidateTyped<TMessage>(TMessage message, IServiceProvider provider)
        where TMessage : IDispatchMessage
    {
        var validators = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
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
