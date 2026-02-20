// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitingMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "RateLimiting")]
public sealed class RateLimitingMiddlewareShould : IAsyncDisposable
{
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;
    private readonly IMessageResult _successResult;
    private readonly RateLimitingMiddleware _sut;

    public RateLimitingMiddlewareShould()
    {
        _logger = new NullLogger<RateLimitingMiddleware>();
        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        _nextDelegate = A.Fake<DispatchRequestDelegate>();
        _successResult = A.Fake<IMessageResult>();

        A.CallTo(() => _successResult.Succeeded).Returns(true);
        A.CallTo(() => _nextDelegate(_message, _context, A<CancellationToken>._))
            .Returns(new ValueTask<IMessageResult>(_successResult));

        // Wire up Items so extension method TryGetValue works via context.Items
        A.CallTo(() => _context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

        _sut = new RateLimitingMiddleware(
            Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions { Enabled = true }),
            _logger);
    }

    public async ValueTask DisposeAsync() => await _sut.DisposeAsync();

    [Fact]
    public void ImplementIDispatchMiddleware()
    {
        _sut.ShouldBeAssignableTo<IDispatchMiddleware>();
    }

    [Fact]
    public void ImplementIDisposable()
    {
        _sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void ImplementIAsyncDisposable()
    {
        _sut.ShouldBeAssignableTo<IAsyncDisposable>();
    }

    [Fact]
    public void HaveRateLimitingStage()
    {
        _sut.Stage.ShouldBe(DispatchMiddlewareStage.RateLimiting);
    }

    [Fact]
    public void HaveAllMessageKinds()
    {
        _sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
    }

    [Fact]
    public void BePublicAndSealed()
    {
        typeof(RateLimitingMiddleware).IsPublic.ShouldBeTrue();
        typeof(RateLimitingMiddleware).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void ThrowWhenOptionsIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimitingMiddleware(null!, _logger));
    }

    [Fact]
    public void ThrowWhenLoggerIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()), null!));
    }

    [Fact]
    public async Task SkipRateLimitingWhenDisabled()
    {
        // Arrange
        using var sut = new RateLimitingMiddleware(
            Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions { Enabled = false }),
            _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AllowRequestWithinRateLimit()
    {
        // Arrange
        var options = new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
        };
        using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.InvokeAsync(null!, _context, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenContextIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.InvokeAsync(_message, null!, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenNextDelegateIsNull()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await _sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task UseTenantIdForRateLimitKey()
    {
        // Arrange
        var options = new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
        };
        using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Set TenantId in Items dictionary (used by TryGetValue extension)
        var items = new Dictionary<string, object>(StringComparer.Ordinal) { ["TenantId"] = "tenant-abc" };
        A.CallTo(() => _context.Items).Returns(items);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UseSlidingWindowAlgorithm()
    {
        // Arrange
        var options = new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60, SegmentsPerWindow = 4 },
        };
        using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UseFixedWindowAlgorithm()
    {
        // Arrange
        var options = new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60 },
        };
        using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UseConcurrencyAlgorithm()
    {
        // Arrange
        var options = new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.Concurrency,
            DefaultLimits = new RateLimits { ConcurrencyLimit = 100 },
        };
        using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void DisposeWithoutException()
    {
        // Arrange
        var sut = new RateLimitingMiddleware(
            Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()),
            _logger);

        // Act & Assert
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public async Task DisposeAsyncWithoutException()
    {
        // Arrange
        var sut = new RateLimitingMiddleware(
            Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()),
            _logger);

        // Act & Assert
        await Should.NotThrowAsync(async () => await sut.DisposeAsync());
    }
}
