// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Validation.FluentValidation;

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
    public void ThrowNotSupportedExceptionForAnyMessage()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "test" };

        // Act & Assert
        // AOT resolver always throws because it requires source-generated dispatch logic
        Should.Throw<NotSupportedException>(() => sut.TryValidate(message));
    }

    [Fact]
    public void IncludeHelpfulMessageInNotSupportedException()
    {
        // Arrange
        var provider = new ServiceCollection().BuildServiceProvider();
        var sut = new AotFluentValidatorResolver(provider);
        var message = new AotResolverTestMessage { Name = "test" };

        // Act
        var ex = Should.Throw<NotSupportedException>(() => sut.TryValidate(message));

        // Assert
        ex.Message.ShouldContain("source generator");
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
}

// ---- Test Infrastructure (file-level to avoid CA1034) ----

internal sealed class AotResolverTestMessage : IDispatchMessage
{
    public string Name { get; set; } = "";
}
