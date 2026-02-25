// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Security;

/// <summary>
/// Unit tests for <see cref="SecurityMiddlewareExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "DI")]
public sealed class SecurityMiddlewareExtensionsShould
{
    [Fact]
    public void RegisterEncryptionServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();

        // Act
        services.AddMessageEncryption(opt =>
        {
            opt.Enabled = true;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var encryptionService = provider.GetService<IMessageEncryptionService>();
        encryptionService.ShouldNotBeNull();
    }

    [Fact]
    public void RegisterSigningServicesWithKeyProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<IKeyProvider>());

        // Act
        services.AddMessageSigning(opt =>
        {
            opt.Enabled = true;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var signingService = provider.GetService<IMessageSigningService>();
        signingService.ShouldNotBeNull();
    }

    [Fact]
    public void RegisterRateLimitingServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRateLimiting(opt =>
        {
            opt.Enabled = true;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var middleware = provider.GetService<RateLimitingMiddleware>();
        middleware.ShouldNotBeNull();
    }

    [Fact]
    public void RegisterJwtAuthenticationServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<ITelemetrySanitizer>());

        // Act
        services.AddJwtAuthentication(opt =>
        {
            opt.Enabled = true;
            opt.Credentials.SigningKey = "ThisIsAVeryLongSigningKeyForHmacSha256ThatExceedsMinimumLength!";
            opt.Credentials.ValidIssuer = "test-issuer";
            opt.Credentials.ValidAudience = "test-audience";
        });

        // Assert
        var sp = services.BuildServiceProvider();
        sp.GetService<IDispatchMiddleware>().ShouldNotBeNull();
    }

    [Fact]
    public void ThrowWhenServicesIsNullForEncryption()
    {
        Should.Throw<ArgumentNullException>(() =>
            SecurityMiddlewareExtensions.AddMessageEncryption(null!));
    }

    [Fact]
    public void ThrowWhenServicesIsNullForSigning()
    {
        Should.Throw<ArgumentNullException>(() =>
            SecurityMiddlewareExtensions.AddMessageSigning(null!));
    }

    [Fact]
    public void ThrowWhenServicesIsNullForRateLimiting()
    {
        Should.Throw<ArgumentNullException>(() =>
            SecurityMiddlewareExtensions.AddRateLimiting(null!));
    }

    [Fact]
    public void ThrowWhenServicesIsNullForJwtAuth()
    {
        Should.Throw<ArgumentNullException>(() =>
            SecurityMiddlewareExtensions.AddJwtAuthentication(null!));
    }

    [Fact]
    public void RegisterEncryptionMiddlewareAsIDispatchMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMessageEncryption();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IDispatchMiddleware) &&
            sd.ImplementationType == typeof(MessageEncryptionMiddleware));
    }

    [Fact]
    public void RegisterSigningMiddlewareAsIDispatchMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMessageSigning();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IDispatchMiddleware) &&
            sd.ImplementationType == typeof(MessageSigningMiddleware));
    }

    [Fact]
    public void RegisterJwtOptionsValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJwtAuthentication();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<JwtAuthenticationOptions>));
    }
}
