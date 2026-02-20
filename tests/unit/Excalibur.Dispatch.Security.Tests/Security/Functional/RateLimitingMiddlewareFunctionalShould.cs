// Functional tests for RateLimitingMiddleware — algorithm behavior, tenant limits, disabled bypass, key extraction

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

#pragma warning disable CA2012

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class RateLimitingMiddlewareFunctionalShould : IDisposable
{
    private readonly IDispatchMessage _message;
    private readonly IMessageContext _context;
    private readonly Dictionary<string, object> _items;
    private readonly DispatchRequestDelegate _successNext;
    private readonly IMessageResult _successResult;

    public RateLimitingMiddlewareFunctionalShould()
    {
        _message = A.Fake<IDispatchMessage>();
        _context = A.Fake<IMessageContext>();
        _items = new Dictionary<string, object>();
        A.CallTo(() => _context.Items).Returns(_items);
        A.CallTo(() => _context.Properties).Returns(new Dictionary<string, object?>());

        _successResult = A.Fake<IMessageResult>();
        A.CallTo(() => _successResult.Succeeded).Returns(true);
        _successNext = (msg, ctx, ct) => new ValueTask<IMessageResult>(_successResult);
    }

    private static RateLimitingMiddleware CreateMiddleware(RateLimitingOptions? opts = null)
    {
        opts ??= new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits
            {
                TokenLimit = 100,
                TokensPerPeriod = 100,
                ReplenishmentPeriodSeconds = 1,
            },
            CleanupIntervalMinutes = 60,
        };

        return new RateLimitingMiddleware(
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<RateLimitingMiddleware>.Instance);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions { Enabled = false });

        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);

        result.ShouldBe(_successResult);
    }

    [Fact]
    public async Task AcquirePermitWithTokenBucket()
    {
        using var sut = CreateMiddleware();

        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExhaustTokenBucketAndReturnRateLimitExceeded()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits
            {
                TokenLimit = 2,
                TokensPerPeriod = 1,
                ReplenishmentPeriodSeconds = 300, // Very slow replenish
                QueueLimit = 0, // Reject immediately when tokens exhausted
            },
            CleanupIntervalMinutes = 60,
        });

        // First two should succeed
        var r1 = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        r1.ShouldNotBeOfType<RateLimitExceededResult>();

        var r2 = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        r2.ShouldNotBeOfType<RateLimitExceededResult>();

        // Third should be rate limited
        var r3 = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        r3.ShouldBeOfType<RateLimitExceededResult>();
        r3.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task UseTenantIdAsRateLimitKey()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits { TokenLimit = 1, TokensPerPeriod = 1, ReplenishmentPeriodSeconds = 300, QueueLimit = 0 },
            CleanupIntervalMinutes = 60,
        });

        // Set TenantId in items -- extension method reads from Items dict
        _items["TenantId"] = "tenant-A";

        // First request succeeds
        await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);

        // Second request for same tenant should be limited
        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        result.ShouldBeOfType<RateLimitExceededResult>();

        // Different tenant should still have permits
        var otherContext = A.Fake<IMessageContext>();
        var otherItems = new Dictionary<string, object> { ["TenantId"] = "tenant-B" };
        A.CallTo(() => otherContext.Items).Returns(otherItems);
        A.CallTo(() => otherContext.Properties).Returns(new Dictionary<string, object?>());

        var otherResult = await sut.InvokeAsync(_message, otherContext, _successNext, CancellationToken.None);
        otherResult.ShouldNotBeOfType<RateLimitExceededResult>();
    }

    [Fact]
    public async Task CreateSlidingWindowLimiter()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60 },
            CleanupIntervalMinutes = 60,
        });

        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        result.ShouldNotBeNull();
        result.ShouldNotBeOfType<RateLimitExceededResult>();
    }

    [Fact]
    public async Task CreateFixedWindowLimiter()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            DefaultLimits = new RateLimits { PermitLimit = 100, WindowSeconds = 60 },
            CleanupIntervalMinutes = 60,
        });

        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        result.ShouldNotBeNull();
        result.ShouldNotBeOfType<RateLimitExceededResult>();
    }

    [Fact]
    public async Task CreateConcurrencyLimiter()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.Concurrency,
            DefaultLimits = new RateLimits { ConcurrencyLimit = 10, QueueLimit = 10 },
            CleanupIntervalMinutes = 60,
        });

        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        result.ShouldNotBeNull();
        result.ShouldNotBeOfType<RateLimitExceededResult>();
    }

    [Fact]
    public async Task ReturnRateLimitExceededResultWithRetryAfter()
    {
        using var sut = CreateMiddleware(new RateLimitingOptions
        {
            Enabled = true,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            DefaultLimits = new RateLimits
            {
                TokenLimit = 1,
                TokensPerPeriod = 1,
                ReplenishmentPeriodSeconds = 300,
                QueueLimit = 0, // Reject immediately when tokens exhausted
            },
            DefaultRetryAfterMilliseconds = 5000,
            CleanupIntervalMinutes = 60,
        });

        // Exhaust permits
        await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);
        var result = await sut.InvokeAsync(_message, _context, _successNext, CancellationToken.None);

        var rateLimited = result.ShouldBeOfType<RateLimitExceededResult>();
        rateLimited.RetryAfterMilliseconds.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void HaveCorrectStage()
    {
        using var sut = CreateMiddleware();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.RateLimiting);
    }

    [Fact]
    public void HaveCorrectApplicableMessageKinds()
    {
        using var sut = CreateMiddleware();
        sut.ApplicableMessageKinds.ShouldBe(MessageKinds.All);
    }

    [Fact]
    public async Task ThrowOnNullMessage()
    {
        using var sut = CreateMiddleware();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(null!, _context, _successNext, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullContext()
    {
        using var sut = CreateMiddleware();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, null!, _successNext, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullNextDelegate()
    {
        using var sut = CreateMiddleware();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await sut.InvokeAsync(_message, _context, null!, CancellationToken.None));
    }

    [Fact]
    public void DisposeMultipleTimesSafely()
    {
        var sut = CreateMiddleware();
        sut.Dispose();
        sut.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisposeAsyncMultipleTimesSafely()
    {
        var sut = CreateMiddleware();
        await sut.DisposeAsync();
        await sut.DisposeAsync(); // Should not throw
    }

    public void Dispose()
    {
        // Intentionally left empty - each test creates and disposes its own middleware
    }
}
