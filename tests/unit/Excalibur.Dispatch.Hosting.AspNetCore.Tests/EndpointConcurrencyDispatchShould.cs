// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Verifies Dispatch endpoint wiring under concurrent multi-endpoint load for minimal APIs and controllers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class EndpointConcurrencyDispatchShould : UnitTestBase
{
	[Fact]
	public async Task MinimalApi_ConcurrentEndpoints_ShouldUseRequestScopedProviderPerDispatch()
	{
		// Arrange
		await using var host = await CreateMinimalApiHostAsync().ConfigureAwait(true);
		using var client = CreateClient(host);
		const int requestsPerEndpoint = 32;

		// Act
		var requests = new List<Task<HttpResponseMessage>>(requestsPerEndpoint * 2);
		for (var i = 0; i < requestsPerEndpoint; i++)
		{
			requests.Add(client.PostAsJsonAsync(
				"/minimal/orders",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
			requests.Add(client.PostAsJsonAsync(
				"/minimal/payments",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
		}

		var responses = await Task.WhenAll(requests).ConfigureAwait(true);

		// Assert
		var failures = await Task.WhenAll(
				responses
					.Where(static response => !response.IsSuccessStatusCode)
					.Select(async response =>
						$"{(int)response.StatusCode}:{await response.Content.ReadAsStringAsync().ConfigureAwait(false)}"))
			.ConfigureAwait(true);
		failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
		var tracker = host.Services.GetRequiredService<RequestScopeTracker>();
		tracker.CountFor("minimal-orders").ShouldBe(requestsPerEndpoint);
		tracker.CountFor("minimal-payments").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("minimal-orders").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("minimal-payments").ShouldBe(requestsPerEndpoint);
	}

	[Fact]
	public async Task Controller_ConcurrentEndpoints_ShouldUseRequestScopedProviderPerDispatch()
	{
		// Arrange
		await using var host = await CreateControllerHostAsync().ConfigureAwait(true);
		using var client = CreateClient(host);
		const int requestsPerEndpoint = 32;

		// Act
		var requests = new List<Task<HttpResponseMessage>>(requestsPerEndpoint * 2);
		for (var i = 0; i < requestsPerEndpoint; i++)
		{
			requests.Add(client.PostAsJsonAsync("/controller/orders", new EndpointRequest { RequestId = Guid.NewGuid() }));
			requests.Add(client.PostAsJsonAsync("/controller/payments", new EndpointRequest { RequestId = Guid.NewGuid() }));
		}

		var responses = await Task.WhenAll(requests).ConfigureAwait(true);

		// Assert
		var failures = await Task.WhenAll(
				responses
					.Where(static response => !response.IsSuccessStatusCode)
					.Select(async response =>
						$"{(int)response.StatusCode}:{await response.Content.ReadAsStringAsync().ConfigureAwait(false)}"))
			.ConfigureAwait(true);
		failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));
		var tracker = host.Services.GetRequiredService<RequestScopeTracker>();
		tracker.CountFor("controller-orders").ShouldBe(requestsPerEndpoint);
		tracker.CountFor("controller-payments").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("controller-orders").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("controller-payments").ShouldBe(requestsPerEndpoint);
	}

	[Fact]
	public async Task MinimalApi_ConcurrentLocalAndRemoteEndpoints_ShouldRouteToExpectedDestination()
	{
		// Arrange
		await using var host = await CreateMixedRouteMinimalApiHostAsync().ConfigureAwait(true);
		using var client = CreateClient(host);
		const int requestsPerEndpoint = 32;

		// Act
		var requests = new List<Task<HttpResponseMessage>>(requestsPerEndpoint * 2);
		for (var i = 0; i < requestsPerEndpoint; i++)
		{
			requests.Add(client.PostAsJsonAsync(
				"/mixed/local",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
			requests.Add(client.PostAsJsonAsync(
				"/mixed/remote",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
		}

		var responses = await Task.WhenAll(requests).ConfigureAwait(true);

		// Assert
		var failures = await Task.WhenAll(
				responses
					.Where(static response => !response.IsSuccessStatusCode)
					.Select(async response =>
						$"{(int)response.StatusCode}:{await response.Content.ReadAsStringAsync().ConfigureAwait(false)}"))
			.ConfigureAwait(true);
		failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));

		var tracker = host.Services.GetRequiredService<RequestScopeTracker>();
		tracker.CountFor("mixed-local").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("mixed-local").ShouldBe(requestsPerEndpoint);
		tracker.CountFor("mixed-remote").ShouldBe(0);

		var remoteSink = host.Services.GetRequiredService<RemoteEndpointSink>();
		remoteSink.CountFor("mixed-remote").ShouldBe(requestsPerEndpoint);
		remoteSink.UniqueScopeCountFor("mixed-remote").ShouldBe(requestsPerEndpoint);
	}

	[Fact]
	public async Task Controller_ConcurrentLocalAndRemoteEndpoints_ShouldRouteToExpectedDestination()
	{
		// Arrange
		await using var host = await CreateControllerHostAsync().ConfigureAwait(true);
		using var client = CreateClient(host);
		const int requestsPerEndpoint = 32;

		// Act
		var requests = new List<Task<HttpResponseMessage>>(requestsPerEndpoint * 2);
		for (var i = 0; i < requestsPerEndpoint; i++)
		{
			requests.Add(client.PostAsJsonAsync(
				"/controller/orders",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
			requests.Add(client.PostAsJsonAsync(
				"/controller/remote",
				new EndpointRequest { RequestId = Guid.NewGuid() }));
		}

		var responses = await Task.WhenAll(requests).ConfigureAwait(true);

		// Assert
		var failures = await Task.WhenAll(
				responses
					.Where(static response => !response.IsSuccessStatusCode)
					.Select(async response =>
						$"{(int)response.StatusCode}:{await response.Content.ReadAsStringAsync().ConfigureAwait(false)}"))
			.ConfigureAwait(true);
		failures.ShouldBeEmpty(string.Join(Environment.NewLine, failures));

		var tracker = host.Services.GetRequiredService<RequestScopeTracker>();
		tracker.CountFor("controller-orders").ShouldBe(requestsPerEndpoint);
		tracker.UniqueScopeCountFor("controller-orders").ShouldBe(requestsPerEndpoint);
		tracker.CountFor("controller-remote").ShouldBe(0);

		var remoteSink = host.Services.GetRequiredService<RemoteEndpointSink>();
		remoteSink.CountFor("controller-remote").ShouldBe(requestsPerEndpoint);
		remoteSink.UniqueScopeCountFor("controller-remote").ShouldBe(requestsPerEndpoint);
	}

	private static async Task<WebApplication> CreateMinimalApiHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		_ = builder.Services.AddDispatch(typeof(EndpointConcurrencyDispatchShould).Assembly);
		builder.Services.AddScoped<RequestScopeProbe>();
		builder.Services.AddSingleton<RequestScopeTracker>();

		var app = builder.Build();
		app.Use(static async (httpContext, next) =>
		{
			httpContext.User = CreateAuthenticatedPrincipal();
			await next().ConfigureAwait(false);
		});
		_ = app.MapPost("/minimal/orders", async (
			[FromBody] EndpointRequest request,
			[FromServices] IDispatcher dispatcher,
			[FromServices] RequestScopeProbe probe,
			HttpContext httpContext,
			CancellationToken cancellationToken) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			var message = new EndpointDispatchAction("minimal-orders", request.RequestId, probe.ScopeId);
			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			return result.ToHttpResult();
		});
		_ = app.MapPost("/minimal/payments", async (
			[FromBody] EndpointRequest request,
			[FromServices] IDispatcher dispatcher,
			[FromServices] RequestScopeProbe probe,
			HttpContext httpContext,
			CancellationToken cancellationToken) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			var message = new EndpointDispatchAction("minimal-payments", request.RequestId, probe.ScopeId);
			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			return result.ToHttpResult();
		});

		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	private static async Task<WebApplication> CreateControllerHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		_ = builder.Services.AddDispatch(typeof(EndpointConcurrencyDispatchShould).Assembly);
		_ = builder.Services.AddControllers()
			.AddApplicationPart(typeof(EndpointConcurrencyDispatchShould).Assembly);
		builder.Services.AddScoped<RequestScopeProbe>();
		builder.Services.AddSingleton<RequestScopeTracker>();
		builder.Services.AddSingleton<RemoteEndpointSink>();
		builder.Services.AddSingleton<TestRemoteEndpointMessageBus>();
		builder.Services.AddSingleton<IDispatchRouter, EndpointRouteByMessageTypeRouter>();
		_ = builder.Services.AddRemoteMessageBus(
			EndpointRouteByMessageTypeRouter.RemoteTransportName,
			static sp => sp.GetRequiredService<TestRemoteEndpointMessageBus>());

		var app = builder.Build();
		app.Use(static async (httpContext, next) =>
		{
			httpContext.User = CreateAuthenticatedPrincipal();
			await next().ConfigureAwait(false);
		});
		_ = app.MapControllers();

		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	private static async Task<WebApplication> CreateMixedRouteMinimalApiHostAsync()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		_ = builder.Services.AddDispatch(typeof(EndpointConcurrencyDispatchShould).Assembly);
		builder.Services.AddScoped<RequestScopeProbe>();
		builder.Services.AddSingleton<RequestScopeTracker>();
		builder.Services.AddSingleton<RemoteEndpointSink>();
		builder.Services.AddSingleton<TestRemoteEndpointMessageBus>();
		builder.Services.AddSingleton<IDispatchRouter, EndpointRouteByMessageTypeRouter>();
		_ = builder.Services.AddRemoteMessageBus(
			EndpointRouteByMessageTypeRouter.RemoteTransportName,
			static sp => sp.GetRequiredService<TestRemoteEndpointMessageBus>());

		var app = builder.Build();
		app.Use(static async (httpContext, next) =>
		{
			httpContext.User = CreateAuthenticatedPrincipal();
			await next().ConfigureAwait(false);
		});

		_ = app.MapPost("/mixed/local", async (
			[FromBody] EndpointRequest request,
			[FromServices] IDispatcher dispatcher,
			[FromServices] RequestScopeProbe probe,
			HttpContext httpContext,
			CancellationToken cancellationToken) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			var message = new EndpointDispatchAction("mixed-local", request.RequestId, probe.ScopeId);
			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			return result.ToHttpResult();
		});

		_ = app.MapPost("/mixed/remote", async (
			[FromBody] EndpointRequest request,
			[FromServices] IDispatcher dispatcher,
			[FromServices] RequestScopeProbe probe,
			HttpContext httpContext,
			CancellationToken cancellationToken) =>
		{
			var context = httpContext.CreateDispatchMessageContext();
			var message = new RemoteEndpointDispatchAction("mixed-remote", request.RequestId, probe.ScopeId);
			var result = await dispatcher.DispatchAsync(message, context, cancellationToken).ConfigureAwait(false);
			return result.ToHttpResult();
		});

		await app.StartAsync().ConfigureAwait(false);
		return app;
	}

	private static ClaimsPrincipal CreateAuthenticatedPrincipal()
	{
		var identity = new ClaimsIdentity(
		[
			new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("N"))
		], "TestAuth");
		return new ClaimsPrincipal(identity);
	}

	private static HttpClient CreateClient(WebApplication host)
		=> host.GetTestClient();

	private sealed class EndpointDispatchActionHandler(RequestScopeProbe probe, RequestScopeTracker tracker)
		: IActionHandler<EndpointDispatchAction>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(EndpointDispatchAction action, CancellationToken cancellationToken)
		{
			_ = cancellationToken;

			var contextRequestServices = Context?.RequestServices
				?? throw new InvalidOperationException("Dispatch context RequestServices was null.");
			var contextProbe = contextRequestServices.GetRequiredService<RequestScopeProbe>();
			if (contextProbe.ScopeId != action.ExpectedScopeId)
			{
				throw new InvalidOperationException(
					"Dispatch context RequestServices did not resolve the request-scoped probe instance.");
			}

			if (probe.ScopeId != action.ExpectedScopeId)
			{
				throw new InvalidOperationException(
					"Handler activation did not use the request-scoped service provider.");
			}

			tracker.Record(action.RouteKey, probe.ScopeId);
			return Task.CompletedTask;
		}
	}

	private sealed class EndpointRouteByMessageTypeRouter : IDispatchRouter
	{
		internal const string RemoteTransportName = "endpoint-remote";
		private const string LocalTransportName = "local";

		public ValueTask<RoutingDecision> RouteAsync(
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			_ = context;
			_ = cancellationToken;

			return message switch
			{
				RemoteEndpointDispatchAction => ValueTask.FromResult(
					RoutingDecision.Success(RemoteTransportName, [RemoteTransportName])),
				EndpointDispatchAction => ValueTask.FromResult(
					RoutingDecision.Success(LocalTransportName, [LocalTransportName])),
				_ => ValueTask.FromResult(
					RoutingDecision.Failure($"No route configured for message type '{message.GetType().Name}'.")),
			};
		}

		public bool CanRouteTo(IDispatchMessage message, string destination)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentException.ThrowIfNullOrWhiteSpace(destination);

			return message switch
			{
				RemoteEndpointDispatchAction => string.Equals(destination, RemoteTransportName, StringComparison.OrdinalIgnoreCase),
				EndpointDispatchAction => string.Equals(destination, LocalTransportName, StringComparison.OrdinalIgnoreCase),
				_ => false,
			};
		}

		public IEnumerable<RouteInfo> GetAvailableRoutes(IDispatchMessage message, IMessageContext context)
		{
			ArgumentNullException.ThrowIfNull(message);
			ArgumentNullException.ThrowIfNull(context);

			return message switch
			{
				RemoteEndpointDispatchAction => [new RouteInfo(RemoteTransportName, RemoteTransportName)],
				EndpointDispatchAction => [new RouteInfo(LocalTransportName, LocalTransportName)],
				_ => [],
			};
		}
	}

	private sealed class TestRemoteEndpointMessageBus(RemoteEndpointSink sink) : IMessageBus
	{
		public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
		{
			_ = cancellationToken;
			if (action is not RemoteEndpointDispatchAction remoteAction)
			{
				return Task.CompletedTask;
			}

			var contextRequestServices = context.RequestServices
				?? throw new InvalidOperationException("Dispatch context RequestServices was null for remote message.");
			var scopedProbe = contextRequestServices.GetRequiredService<RequestScopeProbe>();
			if (scopedProbe.ScopeId != remoteAction.ExpectedScopeId)
			{
				throw new InvalidOperationException(
					"Remote publish did not preserve request-scoped provider through dispatch context.");
			}

			sink.Record(remoteAction.RouteKey, remoteAction.ExpectedScopeId);
			return Task.CompletedTask;
		}

		public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
		{
			_ = evt;
			_ = context;
			_ = cancellationToken;
			return Task.CompletedTask;
		}

		public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
		{
			_ = doc;
			_ = context;
			_ = cancellationToken;
			return Task.CompletedTask;
		}
	}
}

public sealed class EndpointRequest
{
	public Guid RequestId { get; set; }
}

public sealed record EndpointDispatchAction(string RouteKey, Guid RequestId, Guid ExpectedScopeId) : IDispatchAction;
public sealed record RemoteEndpointDispatchAction(string RouteKey, Guid RequestId, Guid ExpectedScopeId) : IDispatchAction;

public sealed class RequestScopeProbe
{
	public Guid ScopeId { get; } = Guid.NewGuid();
}

public sealed class RequestScopeTracker
{
	private readonly ConcurrentDictionary<string, ConcurrentQueue<Guid>> _scopeIdsByRoute = new();

	public void Record(string routeKey, Guid scopeId)
	{
		var queue = _scopeIdsByRoute.GetOrAdd(routeKey, static _ => new ConcurrentQueue<Guid>());
		queue.Enqueue(scopeId);
	}

	public int CountFor(string routeKey)
	{
		return _scopeIdsByRoute.TryGetValue(routeKey, out var queue)
			? queue.Count
			: 0;
	}

	public int UniqueScopeCountFor(string routeKey)
	{
		if (!_scopeIdsByRoute.TryGetValue(routeKey, out var queue))
		{
			return 0;
		}

		return queue.Distinct().Count();
	}
}

public sealed class RemoteEndpointSink
{
	private readonly ConcurrentDictionary<string, ConcurrentQueue<Guid>> _scopeIdsByRoute = new();

	public void Record(string routeKey, Guid scopeId)
	{
		var queue = _scopeIdsByRoute.GetOrAdd(routeKey, static _ => new ConcurrentQueue<Guid>());
		queue.Enqueue(scopeId);
	}

	public int CountFor(string routeKey)
	{
		return _scopeIdsByRoute.TryGetValue(routeKey, out var queue)
			? queue.Count
			: 0;
	}

	public int UniqueScopeCountFor(string routeKey)
	{
		if (!_scopeIdsByRoute.TryGetValue(routeKey, out var queue))
		{
			return 0;
		}

		return queue.Distinct().Count();
	}
}

[ApiController]
[Route("controller")]
public sealed class DispatchTestController : ControllerBase
{
	[HttpPost("orders")]
	public async Task<IActionResult> Orders([FromBody] EndpointRequest request, CancellationToken cancellationToken)
	{
		var scopeProbe = HttpContext.RequestServices.GetRequiredService<RequestScopeProbe>();
		var dispatcher = HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = HttpContext.CreateDispatchMessageContext();
		var result = await dispatcher.DispatchAsync(
				new EndpointDispatchAction("controller-orders", request.RequestId, scopeProbe.ScopeId),
				context,
				cancellationToken)
			.ConfigureAwait(false);
		return this.ToHttpActionResult(result);
	}

	[HttpPost("payments")]
	public async Task<IActionResult> Payments([FromBody] EndpointRequest request, CancellationToken cancellationToken)
	{
		var scopeProbe = HttpContext.RequestServices.GetRequiredService<RequestScopeProbe>();
		var dispatcher = HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = HttpContext.CreateDispatchMessageContext();
		var result = await dispatcher.DispatchAsync(
				new EndpointDispatchAction("controller-payments", request.RequestId, scopeProbe.ScopeId),
				context,
				cancellationToken)
			.ConfigureAwait(false);
		return this.ToHttpActionResult(result);
	}

	[HttpPost("remote")]
	public async Task<IActionResult> Remote([FromBody] EndpointRequest request, CancellationToken cancellationToken)
	{
		var scopeProbe = HttpContext.RequestServices.GetRequiredService<RequestScopeProbe>();
		var dispatcher = HttpContext.RequestServices.GetRequiredService<IDispatcher>();
		var context = HttpContext.CreateDispatchMessageContext();
		var result = await dispatcher.DispatchAsync(
				new RemoteEndpointDispatchAction("controller-remote", request.RequestId, scopeProbe.ScopeId),
				context,
				cancellationToken)
			.ConfigureAwait(false);
		return this.ToHttpActionResult(result);
	}
}
