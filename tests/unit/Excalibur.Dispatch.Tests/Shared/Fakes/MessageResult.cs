// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.TestFakes;

/// <summary>
/// Fake implementation of IMessageResult for testing purposes.
/// </summary>
public sealed class MessageResult : IMessageResult
{
	private MessageResult(bool isSuccess, string? errorMessage = null, Exception? exception = null, bool cacheHit = false, object? validationResult = null, object? authorizationResult = null, IMessageProblemDetails? problemDetails = null)
	{
		Succeeded = isSuccess;
		ErrorMessage = errorMessage;
		Exception = exception;
		CacheHit = cacheHit;
		ValidationResult = validationResult;
		AuthorizationResult = authorizationResult;
		ProblemDetails = problemDetails;
	}

	public bool Succeeded { get; private set; }

	public bool IsSuccess => Succeeded;

	public string? ErrorMessage { get; private set; }

	public bool CacheHit { get; private set; }

	public object? ValidationResult { get; private set; }

	public object? AuthorizationResult { get; private set; }

	public IMessageProblemDetails? ProblemDetails { get; private set; }

	public Exception? Exception { get; private set; }

	/// <summary>
	/// Creates a successful message result.
	/// </summary>
	/// <returns> A successful IMessageResult. </returns>
	public static IMessageResult Success() => new MessageResult(true);

	/// <summary>
	/// Creates a failed message result with an error message.
	/// </summary>
	/// <param name="errorMessage"> The error message. </param>
	/// <returns> A failed IMessageResult. </returns>
	public static IMessageResult Failure(string errorMessage) => new MessageResult(false, errorMessage);

	/// <summary>
	/// Creates a failed message result with an exception.
	/// </summary>
	/// <param name="exception"> The exception that caused the failure. </param>
	/// <returns> A failed IMessageResult. </returns>
	public static IMessageResult Failure(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new MessageResult(false, exception.Message, exception);
	}

	/// <summary>
	/// Creates a successful cached message result.
	/// </summary>
	/// <returns> A successful cached IMessageResult. </returns>
	public static IMessageResult CachedSuccess() => new MessageResult(true, cacheHit: true);

	/// <summary>
	/// Creates a failed message result with validation error.
	/// </summary>
	/// <param name="validationResult"> The validation result. </param>
	/// <returns> A failed IMessageResult with validation error. </returns>
	public static IMessageResult ValidationFailure(object validationResult) => new MessageResult(false, "Validation failed", validationResult: validationResult);

	/// <summary>
	/// Creates a failed message result with authorization error.
	/// </summary>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <returns> A failed IMessageResult with authorization error. </returns>
	public static IMessageResult AuthorizationFailure(object authorizationResult) => new MessageResult(false, "Authorization failed", authorizationResult: authorizationResult);
}
