// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Extensions for MinimalAPI integration.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
	/// <summary>
	/// Registers a POST endpoint that dispatches an action to the Dispatch pipeline from a request DTO.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresUnreferencedCode(
		"Minimal API registration uses reflection for dependency injection and parameter binding which may reference types not preserved during trimming.")]
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	public static RouteHandlerBuilder DispatchPostAction<TRequest, TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction =>
		endpoints.MapPost(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a POST endpoint that dispatches an action to the Dispatch pipeline from a request DTO and returns a typed response.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresUnreferencedCode(
		"Minimal API registration uses reflection for dependency injection and parameter binding which may reference types not preserved during trimming.")]
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	public static RouteHandlerBuilder DispatchPostAction<TRequest, TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapPost(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a POST endpoint that dispatches an action to the Dispatch pipeline.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPostAction<TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction =>
		endpoints.MapPost(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a POST endpoint that dispatches an action to the Dispatch pipeline and returns a typed response.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPostAction<TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapPost(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction, TResponse>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a POST endpoint that dispatches an event to the Dispatch pipeline from a request DTO.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the event is created.</typeparam>
	/// <typeparam name="TEvent">The type of the event to dispatch, must implement <see cref="IDispatchEvent"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="eventFactory">A function that creates the event from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPostEvent<TRequest, TEvent>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TEvent> eventFactory,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TRequest : class
		where TEvent : class, IDispatchEvent =>
		endpoints.MapPost(route, RouteMessageHandlerFactory.CreateEventHandler(
			eventFactory, responseHandler ?? (static (_, result) => result.ToHttpResult())));

	/// <summary>
	/// Registers a GET endpoint that dispatches an action to the Dispatch pipeline from a request DTO and returns a typed response.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchGetAction<TRequest, TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapGet(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a GET endpoint that dispatches an action to the Dispatch pipeline and returns a typed response.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchGetAction<TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapGet(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction, TResponse>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a PUT endpoint that dispatches an action to the Dispatch pipeline from a request DTO.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPutAction<TRequest, TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction =>
		endpoints.MapPut(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a PUT endpoint that dispatches an action to the Dispatch pipeline from a request DTO and returns a typed response.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPutAction<TRequest, TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapPut(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a PUT endpoint that dispatches an action to the Dispatch pipeline.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPutAction<TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction =>
		endpoints.MapPut(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a PUT endpoint that dispatches an action to the Dispatch pipeline and returns a typed response.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchPutAction<TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapPut(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction, TResponse>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a DELETE endpoint that dispatches an action to the Dispatch pipeline from a request DTO.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchDeleteAction<TRequest, TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction =>
		endpoints.MapDelete(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a DELETE endpoint that dispatches an action to the Dispatch pipeline from a request DTO and returns a typed response.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request DTO from which the action is created.</typeparam>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="actionFactory">A function that creates the action from the request DTO and HTTP context.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchDeleteAction<TRequest, TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<TRequest, HttpContext, TAction> actionFactory,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TRequest : class
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapDelete(route, RouteMessageHandlerFactory.CreateMessageHandler(
			actionFactory, responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a DELETE endpoint that dispatches an action to the Dispatch pipeline.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction"/>.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchDeleteAction<TAction>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction =>
		endpoints.MapDelete(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));

	/// <summary>
	/// Registers a DELETE endpoint that dispatches an action to the Dispatch pipeline and returns a typed response.
	/// </summary>
	/// <typeparam name="TAction">The type of the action to dispatch, must implement <see cref="IDispatchAction{TResponse}"/>.</typeparam>
	/// <typeparam name="TResponse">The type of the response returned by the action handler.</typeparam>
	/// <param name="endpoints">The endpoint route builder to register the route with.</param>
	/// <param name="route">The route pattern for the endpoint.</param>
	/// <param name="responseHandler">Optional custom response handler. If null, uses default result-to-HTTP conversion.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
	[RequiresDynamicCode(
		"ASP.NET Core minimal API endpoint registration requires dynamic code generation for request/response handling and dependency injection.")]
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static RouteHandlerBuilder DispatchDeleteAction<TAction, TResponse>(
		this IEndpointRouteBuilder endpoints,
		string route,
		Func<HttpContext, IMessageResult<TResponse>, IResult>? responseHandler = null)
		where TAction : class, IDispatchAction<TResponse>
		where TResponse : class =>
		endpoints.MapDelete(route, RouteMessageHandlerFactory.CreateMessageHandler<TAction, TResponse>(
			responseHandler ?? (static (_, messageResult) => messageResult.ToHttpResult())));
}

