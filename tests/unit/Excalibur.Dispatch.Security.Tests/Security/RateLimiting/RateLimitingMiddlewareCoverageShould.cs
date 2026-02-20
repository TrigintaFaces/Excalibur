// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

#pragma warning disable CA2012

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitingMiddlewareCoverageShould : IDisposable
{
    private readonly RateLimitingMiddleware _sut;
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly DispatchRequestDelegate _nextDelegate;

    public RateLimitingMiddlewareCoverageShould()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits
            {
                TokenLimit = 100,
                TokensPerPeriod = 100,
                ReplenishmentPeriodSeconds = 1,
            },
            CleanupIntervalMinutes = 60, // Long interval to avoid timer effects
        });

        _sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        A.CallTo(() => _context.Items).Returns(new Dictionary<string, object>());

        _nextDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
    }

    [Fact]
    public void HaveCorrectStage()
    {
        _sut.Stage.ShouldBe(DispatchMiddlewareStage.RateLimiting);
    }

    [Fact]
    public void HaveCorrectApplicableMessageKinds()
    {
        _sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions { Enabled = false });
        using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        var expectedResult = A.Fake<IMessageResult>();
        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

        // Act
        var result = await sut.InvokeAsync(_message, _context, next, CancellationToken.None);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task AcquirePermitWithTokenBucket()
    {
        // Act
        var result = await _sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert - should succeed (permit acquired)
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task UseGlobalKeyWhenNoContextIdentifier()
    {
        // Arrange - no TenantId/UserId/ApiKey/ClientIp in context
        // TryGetValue is an extension method that checks Items dictionary - no items means global key

        // Act
        var result = await _sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullMessage()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(null!, _context, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullContext()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(_message, null!, _nextDelegate, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowArgumentNullExceptionForNullNextDelegate()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await _sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimitingMiddleware(null!, NullLogger<RateLimitingMiddleware>.Instance));
    }

    [Fact]
    public void ThrowArgumentNullExceptionForNullLogger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()), null!));
    }

    [Fact]
    public void DisposeMultipleTimes()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions());
        var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        // Act & Assert - should not throw
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public async Task DisposeAsyncMultipleTimes()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions());
        var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        // Act & Assert - should not throw
        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task CreateSlidingWindowLimiter()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60 },
        });
        using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateFixedWindowLimiter()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Algorithm = RateLimitAlgorithm.FixedWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60 },
        });
        using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateConcurrencyLimiter()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Algorithm = RateLimitAlgorithm.Concurrency,
            DefaultLimits = new RateLimits { ConcurrencyLimit = 10 },
        });
        using var sut = new RateLimitingMiddleware(options, NullLogger<RateLimitingMiddleware>.Instance);

        // Act
        var result = await sut.InvokeAsync(_message, _context, _nextDelegate, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
    }

    public void Dispose() => _sut.Dispose();
}
