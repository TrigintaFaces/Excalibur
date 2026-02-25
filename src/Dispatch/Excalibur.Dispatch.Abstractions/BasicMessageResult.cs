// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents the result of a message dispatch operation without a return value.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="BasicMessageResult" /> class. </remarks>
/// <param name="succeeded"> Indicates whether the operation succeeded. </param>
/// <param name="errorMessage"> Optional error message if the operation failed. </param>
/// <param name="cacheHit"> Indicates whether the result was retrieved from cache. </param>
/// <param name="validationResult"> Optional validation result. </param>
/// <param name="authorizationResult"> Optional authorization result. </param>
/// <param name="problemDetails"> Optional problem details if the operation failed. </param>
internal class BasicMessageResult(
	bool succeeded,
	string? errorMessage = null,
	bool cacheHit = false,
	object? validationResult = null,
	object? authorizationResult = null,
	IMessageProblemDetails? problemDetails = null)
	: IMessageResult
{
	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	/// <value> <see langword="true" /> when the operation succeeded; otherwise, <see langword="false" />. </value>
	public bool Succeeded { get; } = succeeded;

	/// <summary>
	/// Gets the error message, if the operation failed.
	/// </summary>
	/// <value> The error message or <see langword="null" />. </value>
	public string? ErrorMessage { get; } = errorMessage;

	/// <summary>
	/// Gets a value indicating whether this result was retrieved from cache.
	/// </summary>
	/// <value> <see langword="true" /> when the result originated from cache; otherwise, <see langword="false" />. </value>
	public bool CacheHit { get; } = cacheHit;

	/// <summary>
	/// Gets the validation result, if validation was performed.
	/// </summary>
	/// <value> The validation result or <see langword="null" />. </value>
	public object? ValidationResult { get; } = validationResult;

	/// <summary>
	/// Gets the authorization result, if authorization was performed.
	/// </summary>
	/// <value> The authorization result or <see langword="null" />. </value>
	public object? AuthorizationResult { get; } = authorizationResult;

	/// <summary>
	/// Gets the problem details, if the operation failed.
	/// </summary>
	/// <value> The problem details or <see langword="null" />. </value>
	public IMessageProblemDetails? ProblemDetails { get; } = problemDetails;
}

/// <summary>
/// Represents the result of a message dispatch operation with a typed return value.
/// </summary>
/// <typeparam name="T"> The type of the return value. </typeparam>
/// <remarks> Initializes a new instance of the <see cref="BasicMessageResult{T}" /> class. </remarks>
/// <param name="succeeded"> Indicates whether the operation succeeded. </param>
/// <param name="value"> The return value of the operation. </param>
/// <param name="errorMessage"> Optional error message if the operation failed. </param>
/// <param name="cacheHit"> Indicates whether the result was retrieved from cache. </param>
/// <param name="validationResult"> Optional validation result. </param>
/// <param name="authorizationResult"> Optional authorization result. </param>
/// <param name="problemDetails"> Optional problem details if the operation failed. </param>
// R0.8: File may only contain a single type
#pragma warning disable SA1402

internal sealed class BasicMessageResult<T>(
#pragma warning restore SA1402 // File may only contain a single type
	bool succeeded,
	T? value = default,
	string? errorMessage = null,
	bool cacheHit = false,
	object? validationResult = null,
	object? authorizationResult = null,
	IMessageProblemDetails? problemDetails = null)
	: BasicMessageResult(succeeded, errorMessage, cacheHit, validationResult, authorizationResult, problemDetails), IMessageResult<T>
{
	/// <summary>
	/// Gets the return value of the operation.
	/// </summary>
	/// <value> The operation return value or <see langword="null" />. </value>
	public T? ReturnValue { get; } = value;
}
