// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="RateLimitingMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.4: RateLimitingMiddleware tests (0% → 50%+).
/// Tests rate limiting pattern implementation for resilience.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class RateLimitingMiddlewareShould : IDisposable
{
	private readonly ILogger<RateLimitingMiddleware> _logger;
	private readonly IDispatchMessage _message;
	private readonly IMessageContext _context;
	private readonly DispatchRequestDelegate _successDelegate;
	private readonly List<RateLimitingMiddleware> _middlewaresToDispose = [];

	public RateLimitingMiddlewareShould()
	{
		_logger = A.Fake<ILogger<RateLimitingMiddleware>>();
		_message = A.Fake<IDispatchMessage>();
		_context = A.Fake<IMessageContext>();

		_ = A.CallTo(() => _context.MessageId).Returns("test-message-id");

		_successDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	public void Dispose()
	{
		foreach (var middleware in _middlewaresToDispose)
		{
			middleware.Dispose();
		}
	}

	private RateLimitingMiddleware CreateMiddleware(RateLimitingOptions options)
	{
		var middleware = new RateLimitingMiddleware(MsOptions.Create(options), _logger);
		_middlewaresToDispose.Add(middleware);
		return middleware;
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RateLimitingMiddleware(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new RateLimitingOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RateLimitingMiddleware(options, null!));
	}

	#endregion

	#region Stage Tests

	[Fact]
	public void HavePreProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions());

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void HaveActionAndEventApplicableMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions());

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(_message, null!, _successDelegate, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(_message, _context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Rate Limiting Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new RateLimitingOptions { Enabled = false });

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Bypass Rate Limiting Tests

	[Fact]
	public async Task BypassRateLimiting_WhenMessageTypeIsInBypassList()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			BypassRateLimitingForTypes = [_message.GetType().Name],
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.TokenBucket,
				TokenLimit = 1,
				TokensPerPeriod = 0, // No replenishment
				ReplenishmentPeriod = TimeSpan.FromHours(1)
			}
		};
		var middleware = CreateMiddleware(options);

		// Act - Should not be rate limited even with token limit of 1
		var result1 = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);
		var result2 = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);
		var result3 = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result1.IsSuccess.ShouldBeTrue();
		result2.IsSuccess.ShouldBeTrue();
		result3.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Rate Limit Enforcement Tests

	[Fact]
	public async Task AllowRequest_WhenWithinRateLimit()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.TokenBucket,
				TokenLimit = 100,
				TokensPerPeriod = 100,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1)
			},
			GlobalLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.TokenBucket,
				TokenLimit = 1000,
				TokensPerPeriod = 1000,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1)
			}
		};
		var middleware = CreateMiddleware(options);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowRateLimitExceededException_WhenRateLimitExceeded()
	{
		// Arrange - Use fixed window with permit limit of 1 to ensure we exceed the limit
		var options = new RateLimitingOptions
		{
			Enabled = true,
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.FixedWindow,
				PermitLimit = 1,
				Window = TimeSpan.FromHours(1) // Long window so permits won't replenish
			},
			GlobalLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.FixedWindow,
				PermitLimit = 1000,
				Window = TimeSpan.FromSeconds(1)
			}
		};
		var middleware = CreateMiddleware(options);

		// Act - First request succeeds
		var result1 = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert first request
		result1.IsSuccess.ShouldBeTrue();

		// Second request should be rate limited
		_ = await Should.ThrowAsync<RateLimitExceededException>(
			middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None).AsTask());
	}

	#endregion

	#region Per-Tenant Rate Limiting Tests

	[Fact]
	public async Task UsePerTenantRateLimiting_WhenEnabled()
	{
		// Arrange - Use fixed window to avoid TokensPerPeriod validation
		var options = new RateLimitingOptions
		{
			Enabled = true,
			EnablePerTenantLimiting = true,
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.FixedWindow,
				PermitLimit = 1,
				Window = TimeSpan.FromHours(1)
			},
			GlobalLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.FixedWindow,
				PermitLimit = 1000,
				Window = TimeSpan.FromSeconds(1)
			}
		};
		var middleware = CreateMiddleware(options);

		// Setup tenant1 context
		var context1 = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context1.GetItem<string>("TenantId")).Returns("tenant1");

		// Setup tenant2 context
		var context2 = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context2.GetItem<string>("TenantId")).Returns("tenant2");

		// Act - First request for tenant1 succeeds
		var result1 = await middleware.InvokeAsync(_message, context1, _successDelegate, CancellationToken.None);

		// First request for tenant2 should also succeed (different tenant)
		var result2 = await middleware.InvokeAsync(_message, context2, _successDelegate, CancellationToken.None);

		// Assert
		result1.IsSuccess.ShouldBeTrue();
		result2.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Rate Limit Algorithm Tests

	[Theory]
	[InlineData(RateLimitAlgorithm.TokenBucket)]
	[InlineData(RateLimitAlgorithm.SlidingWindow)]
	[InlineData(RateLimitAlgorithm.FixedWindow)]
	public async Task SupportDifferentAlgorithms(RateLimitAlgorithm algorithm)
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = algorithm,
				TokenLimit = 100,
				TokensPerPeriod = 100,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1),
				PermitLimit = 100,
				Window = TimeSpan.FromSeconds(1),
				SegmentsPerWindow = 4
			},
			GlobalLimit = new RateLimitConfiguration
			{
				Algorithm = algorithm,
				TokenLimit = 1000,
				TokensPerPeriod = 1000,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1),
				PermitLimit = 1000,
				Window = TimeSpan.FromSeconds(1),
				SegmentsPerWindow = 4
			}
		};
		var middleware = CreateMiddleware(options);

		// Act
		var result = await middleware.InvokeAsync(_message, _context, _successDelegate, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutException()
	{
		// Arrange
		var options = new RateLimitingOptions
		{
			Enabled = true,
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.TokenBucket,
				TokenLimit = 100,
				TokensPerPeriod = 100,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1)
			}
		};
		var middleware = new RateLimitingMiddleware(MsOptions.Create(options), _logger);

		// Act & Assert - Should not throw
		Should.NotThrow(() => middleware.Dispose());
	}

	#endregion
}
