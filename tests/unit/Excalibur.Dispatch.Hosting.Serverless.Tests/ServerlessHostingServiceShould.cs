// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessHostingServiceShould
{
    [Fact]
    public async Task StartAsync_WithAvailableProvider_ShouldComplete()
    {
        // Arrange
        var provider = A.Fake<IServerlessHostProvider>();
        A.CallTo(() => provider.Platform).Returns(ServerlessPlatform.AwsLambda);
        A.CallTo(() => provider.IsAvailable).Returns(true);

        var logger = EnabledTestLogger.Create<ServerlessHostingService>();
        var sut = CreateService(provider, logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert - should complete without throwing
    }

    [Fact]
    public async Task StartAsync_WithUnavailableProvider_ShouldComplete()
    {
        // Arrange
        var provider = A.Fake<IServerlessHostProvider>();
        A.CallTo(() => provider.Platform).Returns(ServerlessPlatform.AzureFunctions);
        A.CallTo(() => provider.IsAvailable).Returns(false);

        var logger = EnabledTestLogger.Create<ServerlessHostingService>();
        var sut = CreateService(provider, logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert - should complete without throwing even when unavailable
    }

    [Fact]
    public async Task StopAsync_ShouldComplete()
    {
        // Arrange
        var provider = A.Fake<IServerlessHostProvider>();
        A.CallTo(() => provider.Platform).Returns(ServerlessPlatform.GoogleCloudFunctions);
        A.CallTo(() => provider.IsAvailable).Returns(true);

        var logger = EnabledTestLogger.Create<ServerlessHostingService>();
        var sut = CreateService(provider, logger);

        // Act
        await sut.StopAsync(CancellationToken.None);

        // Assert - should complete without throwing
    }

    [Fact]
    public async Task StartAsync_ThenStopAsync_ShouldCompleteLifecycle()
    {
        // Arrange
        var provider = A.Fake<IServerlessHostProvider>();
        A.CallTo(() => provider.Platform).Returns(ServerlessPlatform.AwsLambda);
        A.CallTo(() => provider.IsAvailable).Returns(true);

        var logger = EnabledTestLogger.Create<ServerlessHostingService>();
        var sut = CreateService(provider, logger);

        // Act
        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Assert - full lifecycle should complete
    }

    [Fact]
    public void Constructor_WithNullProvider_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => CreateService(null!, EnabledTestLogger.Create<ServerlessHostingService>()));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var provider = A.Fake<IServerlessHostProvider>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => CreateService(provider, null!));
    }

    /// <summary>
    /// Uses reflection to instantiate the internal ServerlessHostingService.
    /// Unwraps TargetInvocationException so guard-clause tests see the original exception.
    /// </summary>
    private static Microsoft.Extensions.Hosting.IHostedService CreateService(
        IServerlessHostProvider provider,
        ILogger<ServerlessHostingService> logger)
    {
        var type = typeof(ServerlessHostProviderFactory).Assembly
            .GetType("Excalibur.Dispatch.Hosting.Serverless.ServerlessHostingService")!;

        try
        {
            return (Microsoft.Extensions.Hosting.IHostedService)Activator.CreateInstance(type, provider, logger)!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // Unreachable but satisfies compiler
        }
    }
}
