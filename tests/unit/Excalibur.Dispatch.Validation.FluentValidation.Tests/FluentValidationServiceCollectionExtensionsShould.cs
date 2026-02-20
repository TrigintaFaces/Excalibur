// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Validation.FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FluentValidationServiceCollectionExtensionsShould
{
    [Fact]
    public void ThrowWhenBuilderIsNullForWithFluentValidation()
    {
        // Act & Assert
        IDispatchBuilder? builder = null;
        Should.Throw<ArgumentNullException>(() => builder!.WithFluentValidation());
    }

    [Fact]
    public void ThrowWhenBuilderIsNullForWithAotFluentValidation()
    {
        // Act & Assert
        IDispatchBuilder? builder = null;
        Should.Throw<ArgumentNullException>(() => builder!.WithAotFluentValidation());
    }

    [Fact]
    public void RegisterFluentValidatorResolverAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.WithFluentValidation();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IValidatorResolver));
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(FluentValidatorResolver));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterAotFluentValidatorResolverAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.WithAotFluentValidation();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IValidatorResolver));
        descriptor.ShouldNotBeNull();
        descriptor.ImplementationType.ShouldBe(typeof(AotFluentValidatorResolver));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void ReturnBuilderForChainingWithFluentValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.WithFluentValidation();

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ReturnBuilderForChainingWithAotFluentValidation()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.WithAotFluentValidation();

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ResolveFluentValidatorResolverFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.WithFluentValidation();
        var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetService<IValidatorResolver>();

        // Assert
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<FluentValidatorResolver>();
    }

    [Fact]
    public void ResolveAotFluentValidatorResolverFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.WithAotFluentValidation();
        var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetService<IValidatorResolver>();

        // Assert
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<AotFluentValidatorResolver>();
    }

    [Fact]
    public void AllowBothRegistrationsSequentially()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IDispatchBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act - register FluentValidation then AOT
        builder.WithFluentValidation();
        builder.WithAotFluentValidation();

        // Assert - both are registered
        var descriptors = services.Where(d => d.ServiceType == typeof(IValidatorResolver)).ToList();
        descriptors.Count.ShouldBe(2);
    }
}
