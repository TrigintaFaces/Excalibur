// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of message processing operations, including success status and detailed failure information.
/// </summary>
public interface IMessageResult
{
	/// <summary>
	/// Gets a value indicating whether the message processing operation completed successfully.
	/// </summary>
	/// <value> <see langword="true" /> when processing completed successfully; otherwise, <see langword="false" />. </value>
	bool Succeeded { get; }

	/// <summary>
	/// Gets a value indicating whether the message processing operation completed successfully.
	/// </summary>
	/// <remarks> Alias for <see cref="Succeeded" /> to maintain compatibility with different naming conventions. </remarks>
	/// <value> <see langword="true" /> when processing completed successfully; otherwise, <see langword="false" />. </value>
	bool IsSuccess => Succeeded;

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value> The error message when the operation fails; otherwise, <see langword="null" />. </value>
	string? ErrorMessage { get; }

	/// <summary>
	/// Gets a value indicating whether the result was served from cache rather than processed anew.
	/// </summary>
	/// <value> <see langword="true" /> when the result was served from cache; otherwise, <see langword="false" />. </value>
	bool CacheHit { get; }

	/// <summary>
	/// Gets the validation result associated with this message result.
	/// </summary>
	/// <value> The validation result or <see langword="null" />. </value>
	object? ValidationResult { get; }

	/// <summary>
	/// Gets the authorization result associated with this message result.
	/// </summary>
	/// <value> The authorization result or <see langword="null" />. </value>
	object? AuthorizationResult { get; }

	/// <summary>
	/// Gets the problem details when the operation fails, providing structured error information.
	/// </summary>
	/// <value> The problem details when the operation fails; otherwise, <see langword="null" />. </value>
	IMessageProblemDetails? ProblemDetails { get; }
}

/// <summary>
/// Represents the result of message processing operations that return a value, extending the base result with typed return data.
/// </summary>
/// <typeparam name="T"> The type of the return value. </typeparam>
public interface IMessageResult<out T> : IMessageResult
{
	/// <summary>
	/// Gets the return value from the message processing operation, or null if the operation failed or produced no result.
	/// </summary>
	/// <value> The return value or <see langword="null" />. </value>
	T? ReturnValue { get; }
}
