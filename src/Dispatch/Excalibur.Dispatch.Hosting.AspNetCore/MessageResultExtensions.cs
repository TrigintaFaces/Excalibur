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
	/// <description> 500 Problem if execution failed without validation errors </description>
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

		return messageResult.Succeeded switch
		{
			false when messageResult.AuthorizationResult is IAuthorizationResult { IsAuthorized: false } => Results.Forbid(),
			false => messageResult.ValidationResult is IValidationResult { IsValid: false }
				? Results.BadRequest(messageResult.ValidationResult)
				: Results.Problem("Failed to process the request"),
			_ => Results.Accepted(),
		};
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
	/// <description> 500 Problem if execution failed without validation errors </description>
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

		return messageResult.Succeeded switch
		{
			false when messageResult.AuthorizationResult is IAuthorizationResult { IsAuthorized: false } => Results.Forbid(),
			false => messageResult.ValidationResult is IValidationResult { IsValid: false }
				? Results.BadRequest(messageResult.ValidationResult)
				: Results.Problem("Failed to process the request"),
			_ => Results.Ok(messageResult.ReturnValue),
		};
	}

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
}
