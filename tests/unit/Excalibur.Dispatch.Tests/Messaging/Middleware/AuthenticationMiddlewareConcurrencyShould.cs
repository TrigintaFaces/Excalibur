// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Sprint 689 T.4 (bcbvh): Regression test for AuthenticationMiddleware Dictionary→ConcurrentDictionary fix.
// Verifies concurrent cache access does not throw or corrupt state.

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.Collections.Concurrent;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;
using IMessageResult = Excalibur.Dispatch.Abstractions.IMessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Regression tests for T.4 (bcbvh): AuthenticationMiddleware concurrent cache access.
/// Before fix: Dictionary&lt;string, ...&gt; caused InvalidOperationException under concurrent reads/writes.
/// After fix: ConcurrentDictionary with TryRemove ensures thread-safe cache operations.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AuthenticationMiddlewareConcurrencyShould
{
	private readonly IAuthenticationService _authService = A.Fake<IAuthenticationService>();
	private readonly ITelemetrySanitizer _sanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ILogger<AuthenticationMiddleware> _logger;

	public AuthenticationMiddlewareConcurrencyShould()
	{
		_logger = A.Fake<ILogger<AuthenticationMiddleware>>();
		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
		A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
		A.CallTo(() => _sanitizer.SanitizeTag(A<string>._, A<string?>._))
			.ReturnsLazily(call => call.GetArgument<string?>(1));

		// Auth service returns a valid principal for any bearer token
		A.CallTo(() => _authService.AuthenticateBearerTokenAsync(
				A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult<ClaimsPrincipal?>(
				new ClaimsPrincipal(new ClaimsIdentity(
					[new Claim(ClaimTypes.Name, "TestUser")], "Bearer"))));
	}

	[Fact]
	public async Task HandleConcurrentCacheAccessWithoutException()
	{
		// Arrange -- caching enabled with short TTL to exercise expiry path
		var middleware = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions
			{
				Enabled = true,
				RequireAuthentication = true,
				EnableCaching = true,
				CacheDuration = TimeSpan.FromMilliseconds(50),
			}),
			_authService, _sanitizer, _logger);

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		var exceptions = new ConcurrentBag<Exception>();
		const int concurrency = 50;
		const int iterationsPerThread = 20;

		// Act -- concurrent auth requests with overlapping tokens to stress cache reads/writes/removes
		await Task.WhenAll(Enumerable.Range(0, concurrency).Select(threadIdx => Task.Run(async () =>
		{
			for (var i = 0; i < iterationsPerThread; i++)
			{
				try
				{
					// Use a small set of tokens to maximize concurrent cache contention
					var token = $"Bearer token-{threadIdx % 5}";
					var message = A.Fake<IDispatchMessage>();
					var context = new MessageContext();
					context.SetItem("Authorization", token);

					await middleware.InvokeAsync(message, context, next, CancellationToken.None)
						.ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is not UnauthorizedAccessException)
				{
					exceptions.Add(ex);
				}
			}
		}))).ConfigureAwait(false);

		// Assert -- no exceptions from concurrent cache access (Dictionary would throw InvalidOperationException)
		exceptions.ShouldBeEmpty(
			"ConcurrentDictionary cache should handle concurrent reads/writes without exceptions");
	}

	[Fact]
	public async Task HandleConcurrentCacheExpiryWithoutRace()
	{
		// Arrange -- very short TTL to force frequent cache misses and re-authentication
		var middleware = new AuthenticationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions
			{
				Enabled = true,
				RequireAuthentication = true,
				EnableCaching = true,
				CacheDuration = TimeSpan.FromMilliseconds(1),
			}),
			_authService, _sanitizer, _logger);

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		var exceptions = new ConcurrentBag<Exception>();

		// Act -- all threads use the same token so cache expires and is re-populated concurrently
		await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
		{
			for (var i = 0; i < 50; i++)
			{
				try
				{
					var message = A.Fake<IDispatchMessage>();
					var context = new MessageContext();
					context.SetItem("Authorization", "Bearer shared-token");

					await middleware.InvokeAsync(message, context, next, CancellationToken.None)
						.ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is not UnauthorizedAccessException)
				{
					exceptions.Add(ex);
				}
			}
		}))).ConfigureAwait(false);

		// Assert
		exceptions.ShouldBeEmpty(
			"Concurrent cache expiry and re-population should not cause race conditions");
	}
}
