// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Lifecycle and behavior tests for <see cref="SecurityEventLogger"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Auditing")]
public sealed class SecurityEventLoggerLifecycleShould : IDisposable
{
    private readonly ISecurityEventStore _eventStore;
    private readonly SecurityEventLogger _sut;

    public SecurityEventLoggerLifecycleShould()
    {
        _eventStore = A.Fake<ISecurityEventStore>();

        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);

        _sut = new SecurityEventLogger(
            NullLogger<SecurityEventLogger>.Instance,
            _eventStore);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void ImplementISecurityEventLogger()
    {
        _sut.ShouldBeAssignableTo<ISecurityEventLogger>();
    }

    [Fact]
    public void ImplementIHostedService()
    {
        _sut.ShouldBeAssignableTo<IHostedService>();
    }

    [Fact]
    public void ImplementIDisposable()
    {
        _sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void BePublicAndSealed()
    {
        typeof(SecurityEventLogger).IsPublic.ShouldBeTrue();
        typeof(SecurityEventLogger).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecurityEventLogger(null!, _eventStore));
    }

    [Fact]
    public void ThrowWhenEventStoreIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SecurityEventLogger(NullLogger<SecurityEventLogger>.Instance, null!));
    }

    [Fact]
    public async Task StartAsync_CompleteSuccessfully()
    {
        // Act & Assert - should not throw
        await _sut.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_CompleteSuccessfully()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act & Assert - should not throw
        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WithoutStarting()
    {
        // Act & Assert - should not throw even if never started
        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LogSecurityEventAsync_QueueEventForProcessing()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act
        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthenticationSuccess,
            "User authenticated successfully",
            SecuritySeverity.Low,
            CancellationToken.None);

        // Allow time for background processing
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(2000);
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => _eventStore.StoreEventsAsync(
            A<IEnumerable<SecurityEvent>>.That.Matches(e => e.Any()),
            A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task LogSecurityEventAsync_IncludeContextInformation()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
        var items = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["User:MessageId"] = "user-123",
            ["Client:IP"] = "192.168.1.1",
            ["Client:UserAgent"] = "TestAgent/1.0",
            ["Message:Type"] = "TestCommand",
            ["Security:Role"] = "Admin",
        };
        A.CallTo(() => context.Items).Returns(items);

        // Act
        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthorizationSuccess,
            "Authorization granted",
            SecuritySeverity.Medium,
            CancellationToken.None,
            context);

        // Allow time for background processing
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(2000);
        await _sut.StopAsync(CancellationToken.None);

        // Assert - verify events were stored
        A.CallTo(() => _eventStore.StoreEventsAsync(
            A<IEnumerable<SecurityEvent>>._,
            A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task LogSecurityEventAsync_HandleNullContext()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act - should not throw with null context
        await _sut.LogSecurityEventAsync(
            SecurityEventType.AuthenticationFailure,
            "Auth failed",
            SecuritySeverity.High,
            CancellationToken.None,
            context: null);

        // Allow time for background processing
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(2000);
        await _sut.StopAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => _eventStore.StoreEventsAsync(
            A<IEnumerable<SecurityEvent>>._,
            A<CancellationToken>._))
            .MustHaveHappened();
    }

    [Fact]
    public async Task LogSecurityEventAsync_HandleStoreFailureGracefully()
    {
        // Arrange
        A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Store failure"));

        await _sut.StartAsync(CancellationToken.None);

        // Act - should not throw even when store fails
        await _sut.LogSecurityEventAsync(
            SecurityEventType.ValidationFailure,
            "Validation failed",
            SecuritySeverity.Medium,
            CancellationToken.None);

        // Allow time for background processing
        await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(2000);

        // Assert - should not throw during stop either
        await _sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Dispose_CanBeCalledSafely()
    {
        // Act & Assert - should not throw
        using var logger = new SecurityEventLogger(
            NullLogger<SecurityEventLogger>.Instance,
            _eventStore);

        logger.Dispose();
    }
}
