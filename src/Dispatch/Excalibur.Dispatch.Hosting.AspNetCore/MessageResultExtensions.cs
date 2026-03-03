// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Extension methods for converting <see cref="IMessageResult" /> instances to HTTP responses.
/// </summary>
public static class MessageResultExtensions
{
	#region Sync — Minimal API (IResult)

	/// <summary>
	/// Converts a message result to an HTTP result for Minimal APIs.
	/// </summary>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <returns>
	/// An <see cref="IResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 403 Forbidden if authorization failed </description>
	/// </item>
	/// <item>
	/// <description> 400 Bad Request if validation failed </description>
	/// </item>
	/// <item>
	/// <description> Problem with ProblemDetails status if structured error details are present </description>
	/// </item>
	/// <item>
	/// <description> 500 Problem if execution failed without structured error details </description>
	/// </item>
	/// <item>
	/// <description> 202 Accepted if the message was processed successfully </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="messageResult" /> is null. </exception>
	public static IResult ToHttpResult(this IMessageResult messageResult)
	{
		ArgumentNullException.ThrowIfNull(messageResult);

		return messageResult.Succeeded
			? Results.Accepted()
			: MapFailureToResult(messageResult);
	}

	/// <summary>
	/// Converts a message result with a return value to an HTTP result for Minimal APIs.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <returns>
	/// An <see cref="IResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 403 Forbidden if authorization failed </description>
	/// </item>
	/// <item>
	/// <description> 400 Bad Request if validation failed </description>
	/// </item>
	/// <item>
	/// <description> Problem with ProblemDetails status if structured error details are present </description>
	/// </item>
	/// <item>
	/// <description> 500 Problem if execution failed without structured error details </description>
	/// </item>
	/// <item>
	/// <description> 200 OK with the return value if the message was processed successfully </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="messageResult" /> is null. </exception>
	public static IResult ToHttpResult<TResult>(this IMessageResult<TResult> messageResult)
	{
		ArgumentNullException.ThrowIfNull(messageResult);

		return messageResult.Succeeded
			? Results.Ok(messageResult.ReturnValue)
			: MapFailureToResult(messageResult);
	}

	/// <summary>
	/// Converts a non-generic message result to a 204 No Content HTTP result on success.
	/// </summary>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <returns>
	/// An <see cref="IResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 204 No Content if the message was processed successfully </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="messageResult" /> is null. </exception>
	public static IResult ToNoContentResult(this IMessageResult messageResult)
	{
		ArgumentNullException.ThrowIfNull(messageResult);

		return messageResult.Succeeded
			? Results.NoContent()
			: MapFailureToResult(messageResult);
	}

	/// <summary>
	/// Converts a generic message result to a 201 Created HTTP result on success.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <param name="location"> The URI of the newly created resource. </param>
	/// <returns>
	/// An <see cref="IResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 201 Created with the location and return value if successful </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="messageResult" /> is null. </exception>
	public static IResult ToCreatedResult<TResult>(this IMessageResult<TResult> messageResult, string location)
	{
		ArgumentNullException.ThrowIfNull(messageResult);

		return messageResult.Succeeded
			? Results.Created(new Uri(location, UriKind.RelativeOrAbsolute), messageResult.ReturnValue)
			: MapFailureToResult(messageResult);
	}

	#endregion

	#region Async — Minimal API (Task<IResult>)

	/// <summary>
	/// Converts an async non-generic message result to an HTTP result for Minimal APIs.
	/// Enables fluent chaining from <see cref="Task{T}" /> without breaking the pipeline.
	/// </summary>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <returns>
	/// A <see cref="Task{T}" /> representing an <see cref="IResult" />:
	/// <list type="bullet">
	/// <item>
	/// <description> 202 Accepted if the message was processed successfully </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="resultTask" /> is null. </exception>
	public static async Task<IResult> ToApiResult(this Task<IMessageResult> resultTask)
	{
		ArgumentNullException.ThrowIfNull(resultTask);

		var result = await resultTask.ConfigureAwait(false);
		return result.ToHttpResult();
	}

	/// <summary>
	/// Converts an async generic message result to an HTTP result for Minimal APIs.
	/// Enables fluent chaining from <see cref="Task{T}" /> without breaking the pipeline.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <returns>
	/// A <see cref="Task{T}" /> representing an <see cref="IResult" />:
	/// <list type="bullet">
	/// <item>
	/// <description> 200 OK with the return value if successful </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="resultTask" /> is null. </exception>
	public static async Task<IResult> ToApiResult<TResult>(this Task<IMessageResult<TResult>> resultTask)
	{
		ArgumentNullException.ThrowIfNull(resultTask);

		var result = await resultTask.ConfigureAwait(false);
		return result.ToHttpResult();
	}

	/// <summary>
	/// Converts an async non-generic message result to a 204 No Content HTTP result on success.
	/// Enables fluent chaining from <see cref="Task{T}" /> without breaking the pipeline.
	/// </summary>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <returns>
	/// A <see cref="Task{T}" /> representing an <see cref="IResult" />:
	/// <list type="bullet">
	/// <item>
	/// <description> 204 No Content if successful </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="resultTask" /> is null. </exception>
	public static async Task<IResult> ToNoContentResult(this Task<IMessageResult> resultTask)
	{
		ArgumentNullException.ThrowIfNull(resultTask);

		var result = await resultTask.ConfigureAwait(false);
		return result.ToNoContentResult();
	}

	/// <summary>
	/// Converts an async generic message result to a 201 Created HTTP result on success.
	/// Enables fluent chaining from <see cref="Task{T}" /> without breaking the pipeline.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <param name="location"> The URI of the newly created resource. </param>
	/// <returns>
	/// A <see cref="Task{T}" /> representing an <see cref="IResult" />:
	/// <list type="bullet">
	/// <item>
	/// <description> 201 Created with the location and return value if successful </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="resultTask" /> is null. </exception>
	public static async Task<IResult> ToCreatedResult<TResult>(
		this Task<IMessageResult<TResult>> resultTask,
		string location)
	{
		ArgumentNullException.ThrowIfNull(resultTask);

		var result = await resultTask.ConfigureAwait(false);
		return result.ToCreatedResult(location);
	}

	/// <summary>
	/// Converts an async generic message result to a 201 Created HTTP result on success,
	/// using a factory function to derive the location URI from the result value.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="resultTask"> The task representing the message result. </param>
	/// <param name="locationFactory"> A function that creates the location URI from the result value. </param>
	/// <returns>
	/// A <see cref="Task{T}" /> representing an <see cref="IResult" />:
	/// <list type="bullet">
	/// <item>
	/// <description> 201 Created with the dynamic location and return value if successful </description>
	/// </item>
	/// <item>
	/// <description> Appropriate error response if the operation failed </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="resultTask" /> or <paramref name="locationFactory" /> is null.
	/// </exception>
	public static async Task<IResult> ToCreatedResult<TResult>(
		this Task<IMessageResult<TResult>> resultTask,
		Func<TResult, string> locationFactory)
	{
		ArgumentNullException.ThrowIfNull(resultTask);
		ArgumentNullException.ThrowIfNull(locationFactory);

		var result = await resultTask.ConfigureAwait(false);

		if (!result.Succeeded || result.ReturnValue is null)
		{
			return MapFailureToResult(result);
		}

		var location = locationFactory(result.ReturnValue);
		return Results.Created(new Uri(location, UriKind.RelativeOrAbsolute), result.ReturnValue);
	}

	#endregion

	#region Sync — MVC (IActionResult)

	/// <summary>
	/// Converts a message result to an HTTP action result for MVC controllers.
	/// </summary>
	/// <param name="controller"> The controller instance. </param>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <returns>
	/// An <see cref="IActionResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 403 Forbidden if authorization failed </description>
	/// </item>
	/// <item>
	/// <description> 400 Bad Request if validation failed </description>
	/// </item>
	/// <item>
	/// <description> 500 Problem if execution failed without validation errors </description>
	/// </item>
	/// <item>
	/// <description> 202 Accepted if the message was processed successfully </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="controller" /> or <paramref name="messageResult" /> is null.
	/// </exception>
	public static IActionResult ToHttpActionResult(this ControllerBase controller, IMessageResult messageResult)
	{
		ArgumentNullException.ThrowIfNull(messageResult);
		ArgumentNullException.ThrowIfNull(controller);

		return messageResult.Succeeded switch
		{
			false when messageResult.AuthorizationResult is IAuthorizationResult { IsAuthorized: false } => controller.Forbid(),
			false when messageResult.ValidationResult is IValidationResult { IsValid: false } validationResult => controller.BadRequest(
				validationResult.Errors),
			false => controller.Problem(detail: "Failed to process the request", statusCode: StatusCodes.Status500InternalServerError),
			_ => controller.Accepted(),
		};
	}

	/// <summary>
	/// Converts a message result with a return value to an HTTP action result for MVC controllers.
	/// </summary>
	/// <typeparam name="TResult"> The type of the result value. </typeparam>
	/// <param name="controller"> The controller instance. </param>
	/// <param name="messageResult"> The message result to convert. </param>
	/// <returns>
	/// An <see cref="IActionResult" /> representing the HTTP response:
	/// <list type="bullet">
	/// <item>
	/// <description> 403 Forbidden if authorization failed </description>
	/// </item>
	/// <item>
	/// <description> 400 Bad Request if validation failed </description>
	/// </item>
	/// <item>
	/// <description> 500 Problem if execution failed without validation errors </description>
	/// </item>
	/// <item>
	/// <description> 200 OK with the return value if the message was processed successfully </description>
	/// </item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="controller" /> or <paramref name="messageResult" /> is null.
	/// </exception>
	public static IActionResult ToHttpActionResult<TResult>(this ControllerBase controller, IMessageResult<TResult> messageResult)
	{
		ArgumentNullException.ThrowIfNull(messageResult);
		ArgumentNullException.ThrowIfNull(controller);

		return messageResult.Succeeded switch
		{
			false when messageResult.AuthorizationResult is IAuthorizationResult { IsAuthorized: false } => controller.Forbid(),
			false when messageResult.ValidationResult is IValidationResult { IsValid: false } validationResult => controller.BadRequest(
				validationResult.Errors),
			false => controller.Problem(detail: "Failed to process the request", statusCode: StatusCodes.Status500InternalServerError),
			_ => controller.Ok(messageResult.ReturnValue),
		};
	}

	#endregion

	#region Private Helpers

	/// <summary>
	/// Maps a failed message result to an appropriate HTTP error result.
	/// </summary>
	/// <remarks>
	/// The mapping precedence is:
	/// <list type="number">
	/// <item><description>Authorization failure (403 Forbidden)</description></item>
	/// <item><description>Validation failure (400 Bad Request)</description></item>
	/// <item><description>ProblemDetails with Status (RFC 7807 Problem response with the specified status)</description></item>
	/// <item><description>Fallback (500 Problem with error message or generic detail)</description></item>
	/// </list>
	/// </remarks>
	private static IResult MapFailureToResult(IMessageResult messageResult)
	{
		// 1. Authorization failure → 403 Forbid
		if (messageResult.AuthorizationResult is IAuthorizationResult { IsAuthorized: false })
		{
			return Results.Forbid();
		}

		// 2. Validation failure → 400 BadRequest
		if (messageResult.ValidationResult is IValidationResult { IsValid: false })
		{
			return Results.BadRequest(messageResult.ValidationResult);
		}

		// 3. ProblemDetails with Status → RFC 7807 Problem response
		if (messageResult.ProblemDetails is { Status: int status })
		{
			return Results.Problem(
				detail: messageResult.ProblemDetails.Detail,
				title: messageResult.ProblemDetails.Title,
				statusCode: status,
				type: messageResult.ProblemDetails.Type,
				instance: messageResult.ProblemDetails.Instance);
		}

		// 4. Fallback → generic 500
		return Results.Problem(
			detail: messageResult.ErrorMessage ?? "Failed to process the request");
	}

	#endregion
}
