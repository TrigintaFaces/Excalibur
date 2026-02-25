// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="EndpointRouteBuilderExtensions"/> verifying that each HTTP verb overload
/// registers an endpoint on the route builder without throwing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code")]
[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.")]
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Test types must be public for ASP.NET minimal API generic constraints")]
public sealed class EndpointRouteBuilderExtensionsShould : UnitTestBase
{
	#region POST — DispatchPostAction<TRequest, TAction>

	[Fact]
	public void DispatchPostAction_WithRequestFactory_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostAction<TestRequest, TestAction>(
			"/api/test",
			(request, _) => new TestAction());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchPostAction_WithRequestFactory_AcceptCustomResponseHandler()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostAction<TestRequest, TestAction>(
			"/api/test",
			(request, _) => new TestAction(),
			(_, result) => Results.Ok("custom"));

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region POST — DispatchPostAction<TRequest, TAction, TResponse>

	[Fact]
	public void DispatchPostAction_WithRequestFactoryAndResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostAction<TestRequest, TestActionWithResponse, TestResponse>(
			"/api/test-response",
			(request, _) => new TestActionWithResponse());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region POST — DispatchPostAction<TAction>

	[Fact]
	public void DispatchPostAction_Simplified_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostAction<TestAction>("/api/simple-post");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region POST — DispatchPostAction<TAction, TResponse>

	[Fact]
	public void DispatchPostAction_SimplifiedWithResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostAction<TestActionWithResponse, TestResponse>("/api/simple-post-response");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region POST — DispatchPostEvent<TRequest, TEvent>

	[Fact]
	public void DispatchPostEvent_WithRequestFactory_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPostEvent<TestRequest, TestEvent>(
			"/api/event",
			(request, _) => new TestEvent());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region GET — DispatchGetAction<TRequest, TAction, TResponse>

	[Fact]
	public void DispatchGetAction_WithRequestFactory_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchGetAction<TestRequest, TestActionWithResponse, TestResponse>(
			"/api/get-test",
			(request, _) => new TestActionWithResponse());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region GET — DispatchGetAction<TAction, TResponse>

	[Fact]
	public void DispatchGetAction_Simplified_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchGetAction<TestActionWithResponse, TestResponse>("/api/simple-get");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region PUT — DispatchPutAction variants

	[Fact]
	public void DispatchPutAction_WithRequestFactory_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPutAction<TestRequest, TestAction>(
			"/api/put-test",
			(request, _) => new TestAction());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchPutAction_WithRequestFactoryAndResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPutAction<TestRequest, TestActionWithResponse, TestResponse>(
			"/api/put-response",
			(request, _) => new TestActionWithResponse());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchPutAction_Simplified_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPutAction<TestAction>("/api/simple-put");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchPutAction_SimplifiedWithResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchPutAction<TestActionWithResponse, TestResponse>("/api/simple-put-response");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region DELETE — DispatchDeleteAction variants

	[Fact]
	public void DispatchDeleteAction_WithRequestFactory_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchDeleteAction<TestRequest, TestAction>(
			"/api/delete-test",
			(request, _) => new TestAction());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchDeleteAction_WithRequestFactoryAndResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchDeleteAction<TestRequest, TestActionWithResponse, TestResponse>(
			"/api/delete-response",
			(request, _) => new TestActionWithResponse());

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchDeleteAction_Simplified_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchDeleteAction<TestAction>("/api/simple-delete");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void DispatchDeleteAction_SimplifiedWithResponse_RegisterEndpoint()
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddDispatch();
		builder.Services.AddRouting();
		var app = builder.Build();

		// Act
		var routeBuilder = app.DispatchDeleteAction<TestActionWithResponse, TestResponse>("/api/simple-delete-response");

		// Assert
		routeBuilder.ShouldNotBeNull();
	}

	#endregion

	#region Cancellation propagation

	[Theory]
	[InlineData("POST", "/api/cancel-post")]
	[InlineData("GET", "/api/cancel-get")]
	[InlineData("PUT", "/api/cancel-put")]
	[InlineData("DELETE", "/api/cancel-delete")]
	public async Task DispatchActionEndpoints_PropagateRequestAbortedCancellationToken(string method, string route)
	{
		// Arrange
		var builder = WebApplication.CreateBuilder();
		builder.Services.AddRouting();

		var dispatcher = new RecordingDispatcher();
		_ = builder.Services.AddSingleton<IDispatcher>(dispatcher);

		var app = builder.Build();
		_ = method switch
		{
			"POST" => app.DispatchPostAction<TestAction>(route),
			"GET" => app.DispatchGetAction<TestActionWithResponse, TestResponse>(route),
			"PUT" => app.DispatchPutAction<TestAction>(route),
			"DELETE" => app.DispatchDeleteAction<TestAction>(route),
			_ => throw new InvalidOperationException($"Unsupported HTTP method: {method}")
		};

		var endpoint = GetRouteEndpoint(app, route, method);
		var cts = new CancellationTokenSource();
		var context = new DefaultHttpContext
		{
			RequestServices = app.Services
		};

		context.Request.Method = method;
		context.Request.Path = route;
		context.RequestAborted = cts.Token;
		context.User = new ClaimsPrincipal(
			new ClaimsIdentity(
				[new Claim(ClaimTypes.NameIdentifier, "test-user")],
				authenticationType: "test"));
		context.SetEndpoint(endpoint);

		// Act
		await endpoint.RequestDelegate!(context);

		// Assert
		dispatcher.LastDispatchCancellationToken.ShouldBe(cts.Token);
		dispatcher.LastDispatchCancellationToken.CanBeCanceled.ShouldBeTrue();
	}

	private static RouteEndpoint GetRouteEndpoint(WebApplication app, string route, string method)
	{
		var endpoint = ((IEndpointRouteBuilder)app).DataSources
			.SelectMany(static dataSource => dataSource.Endpoints)
			.OfType<RouteEndpoint>()
			.Single(endpoint =>
				string.Equals(endpoint.RoutePattern.RawText, route, StringComparison.Ordinal) &&
				endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

		return endpoint;
	}

	#endregion

	#region Test Types

	public sealed class TestRequest;

	public sealed class TestAction : IDispatchAction;

	public sealed class TestActionWithResponse : IDispatchAction<TestResponse>;

	public sealed class TestResponse;

	public sealed class TestEvent : IDispatchEvent;

	private sealed class RecordingDispatcher : IDispatcher
	{
		public IServiceProvider? ServiceProvider => null;

		public CancellationToken LastDispatchCancellationToken { get; private set; } = CancellationToken.None;

		public Task<IMessageResult> DispatchAsync<TAction>(
			TAction message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TAction : IDispatchMessage
		{
			LastDispatchCancellationToken = cancellationToken;
			return Task.FromResult(MessageResult.Success());
		}

		public Task<IMessageResult<TResponse>> DispatchAsync<TAction, TResponse>(
			TAction message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TAction : IDispatchAction<TResponse>
		{
			LastDispatchCancellationToken = cancellationToken;
			return Task.FromResult(MessageResult.Success(default(TResponse)!));
		}

		public IAsyncEnumerable<TOutput> DispatchStreamingAsync<TDocument, TOutput>(
			TDocument document,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
		{
			LastDispatchCancellationToken = cancellationToken;
			return EmptyAsync<TOutput>();
		}

		public Task DispatchStreamAsync<TDocument>(
			IAsyncEnumerable<TDocument> documents,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
		{
			LastDispatchCancellationToken = cancellationToken;
			return Task.CompletedTask;
		}

		public IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
			IAsyncEnumerable<TInput> input,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TInput : IDispatchDocument
		{
			LastDispatchCancellationToken = cancellationToken;
			return EmptyAsync<TOutput>();
		}

		public Task DispatchWithProgressAsync<TDocument>(
			TDocument document,
			IMessageContext context,
			IProgress<DocumentProgress> progress,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument
		{
			LastDispatchCancellationToken = cancellationToken;
			return Task.CompletedTask;
		}

		private static async IAsyncEnumerable<T> EmptyAsync<T>()
		{
			yield break;
		}
	}

	#endregion
}

