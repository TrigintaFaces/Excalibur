// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Depth tests for <see cref="RateLimitingMiddleware"/>.
/// Covers rate limit exhaustion, key extraction (user, API key, client IP, message type),
/// tenant-specific limits, disposal, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitingMiddlewareDepthShould
{
	private readonly IDispatchMessage _message;
	private readonly DispatchRequestDelegate _nextDelegate;
	private readonly IMessageResult _successResult;
	private readonly ILogger<RateLimitingMiddleware> _logger;

	public RateLimitingMiddlewareDepthShould()
	{
		_message = A.Fake<IDispatchMessage>();
		_successResult = A.Fake<IMessageResult>();
		A.CallTo(() => _successResult.Succeeded).Returns(true);
		_nextDelegate = A.Fake<DispatchRequestDelegate>();
		A.CallTo(() => _nextDelegate(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(_successResult));
		_logger = NullLogger<RateLimitingMiddleware>.Instance;
	}

	private static IMessageContext CreateContextWithItems(Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(items ?? new Dictionary<string, object>(StringComparer.Ordinal));
		return context;
	}

	[Fact]
	public async Task ExhaustTokenBucketAndReturnRateLimitExceeded()
	{
		// Arrange - very low limit (1 token, no replenishment in test timeframe)
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits
			{
				TokenLimit = 1,
				QueueLimit = 0,
				TokensPerPeriod = 1,
				ReplenishmentPeriodSeconds = 3600,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Act - first request should succeed
		var result1 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Act - second request should be rate limited
		var result2 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.ShouldBeAssignableTo<RateLimitExceededResult>();
		result2.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ExhaustFixedWindowAndReturnRateLimitExceeded()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.FixedWindow,
			DefaultLimits = new RateLimits
			{
				PermitLimit = 1,
				QueueLimit = 0,
				WindowSeconds = 3600,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Act
		var result1 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);
		var result2 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ExhaustSlidingWindowAndReturnRateLimitExceeded()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.SlidingWindow,
			DefaultLimits = new RateLimits
			{
				PermitLimit = 1,
				QueueLimit = 0,
				WindowSeconds = 3600,
				SegmentsPerWindow = 2,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Act
		var result1 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);
		var result2 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ExhaustConcurrencyAndReturnRateLimitExceeded()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.Concurrency,
			DefaultLimits = new RateLimits
			{
				ConcurrencyLimit = 1,
				QueueLimit = 0,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Arrange - first delegate never completes (holds the concurrency slot)
		var tcs = new TaskCompletionSource<IMessageResult>();
		var blockingDelegate = A.Fake<DispatchRequestDelegate>();
		A.CallTo(() => blockingDelegate(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(tcs.Task));

		// Act - first request holds the concurrency slot
		var task1 = sut.InvokeAsync(_message, context, blockingDelegate, CancellationToken.None);

		// Second request should be rejected because concurrency is at 1
		var result2 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Cleanup
		tcs.SetResult(_successResult);
		_ = await task1;

		// Assert
		result2.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task UseUserIdForRateLimitKey()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["UserId"] = "user-123",
		});

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task UseApiKeyForRateLimitKey()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ApiKey"] = "secret-api-key-value",
		});

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task UseClientIpForRateLimitKey()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ClientIp"] = "192.168.1.100",
		});

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task UseGlobalKeyWhenNoContextIdentifiers()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Act - no tenant/user/apikey/ip in context
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ApplyTenantSpecificLimits()
	{
		// Arrange - tenant with very low limit
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits { TokenLimit = 100, TokensPerPeriod = 100, ReplenishmentPeriodSeconds = 1 },
			TenantLimits = new Dictionary<string, RateLimits>
			{
				["restricted-tenant"] = new RateLimits
				{
					TokenLimit = 1,
					QueueLimit = 0,
					TokensPerPeriod = 1,
					ReplenishmentPeriodSeconds = 3600,
				},
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TenantId"] = "restricted-tenant",
		});

		// Act
		var result1 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);
		var result2 = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task RateLimitExceededResultHasRetryAfterMs()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultRetryAfterMilliseconds = 5000,
			DefaultLimits = new RateLimits
			{
				TokenLimit = 1,
				QueueLimit = 0,
				TokensPerPeriod = 1,
				ReplenishmentPeriodSeconds = 3600,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		// Exhaust the limit
		_ = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		var rateLimitResult = result.ShouldBeAssignableTo<RateLimitExceededResult>();
		rateLimitResult!.RetryAfterMilliseconds.ShouldBeGreaterThan(0);
		rateLimitResult.RateLimitKey.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task RateLimitExceededResultHasProblemDetails()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.FixedWindow,
			DefaultLimits = new RateLimits { PermitLimit = 1, QueueLimit = 0, WindowSeconds = 3600 },
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);
		var context = CreateContextWithItems();

		_ = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Act
		var result = await sut.InvokeAsync(_message, context, _nextDelegate, CancellationToken.None);

		// Assert
		var rateLimitResult = result.ShouldBeAssignableTo<RateLimitExceededResult>();
		rateLimitResult!.ProblemDetails.ShouldNotBeNull();
	}

	[Fact]
	public void DoubleDisposeShouldNotThrow()
	{
		// Arrange
		var sut = new RateLimitingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()),
			_logger);

		// Act & Assert
		sut.Dispose();
		sut.Dispose(); // should not throw
	}

	[Fact]
	public async Task DoubleDisposeAsyncShouldNotThrow()
	{
		// Arrange
		var sut = new RateLimitingMiddleware(
			Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions()),
			_logger);

		// Act & Assert
		await sut.DisposeAsync();
		await sut.DisposeAsync(); // should not throw
	}

	[Fact]
	public async Task IsolateDifferentTenantsRateLimits()
	{
		// Arrange - tenant-specific limits should not interfere with each other
		var options = new RateLimitingOptions
		{
			Enabled = true,
			Algorithm = RateLimitAlgorithm.TokenBucket,
			DefaultLimits = new RateLimits
			{
				TokenLimit = 1,
				QueueLimit = 0,
				TokensPerPeriod = 1,
				ReplenishmentPeriodSeconds = 3600,
			},
		};
		using var sut = new RateLimitingMiddleware(Microsoft.Extensions.Options.Options.Create(options), _logger);

		var contextA = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TenantId"] = "tenant-A",
		});
		var contextB = CreateContextWithItems(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TenantId"] = "tenant-B",
		});

		// Act - exhaust tenant A's limit
		_ = await sut.InvokeAsync(_message, contextA, _nextDelegate, CancellationToken.None);
		var resultA2 = await sut.InvokeAsync(_message, contextA, _nextDelegate, CancellationToken.None);

		// Tenant B should still have its own limit
		var resultB1 = await sut.InvokeAsync(_message, contextB, _nextDelegate, CancellationToken.None);

		// Assert
		resultA2.Succeeded.ShouldBeFalse(); // tenant A exhausted
		resultB1.Succeeded.ShouldBeTrue();  // tenant B still has tokens
	}
}
