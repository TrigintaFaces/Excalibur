// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch;

/// <summary>
/// Static factory methods for creating message results.
/// </summary>
public static class MessageResult
{
	private static readonly IMessageResult CachedSuccess = new BasicMessageResult(succeeded: true);
	private static readonly IMessageResult CachedSuccessFromCache =
		new BasicMessageResult(succeeded: true, disposition: MessageDisposition.ServedFromCache);
	private static readonly IMessageResult CachedCancelled = new BasicMessageResult(succeeded: false);

	/// <summary>
	/// Creates a successful message result.
	/// </summary>
	/// <returns> A successful message result. </returns>
	public static IMessageResult Success() => CachedSuccess;

	/// <summary>
	/// Creates a successful message result with cache hit.
	/// </summary>
	/// <returns> A successful message result from cache. </returns>
	public static IMessageResult SuccessFromCache() => CachedSuccessFromCache;

	/// <summary>
	/// Creates a successful message result with a value from cache.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="value"> The cached value to return. </param>
	/// <returns> A successful message result with value indicating cache hit. </returns>
	public static IMessageResult<T> SuccessFromCache<T>(T value) =>
		new BasicMessageResult<T>(succeeded: true, value: value, disposition: MessageDisposition.ServedFromCache);

	/// <summary>
	/// Creates a successful message result with additional parameters.
	/// </summary>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <param name="disposition"> Describes how the result was produced. </param>
	/// <returns> A successful message result. </returns>
	public static IMessageResult Success(
			RoutingDecision? routingDecision,
			object? validationResult,
			object? authorizationResult,
			MessageDisposition disposition = MessageDisposition.Handled)
	{
		_ = routingDecision;

		if (validationResult is null && authorizationResult is null)
		{
			// Only the two dispositions with a cached singleton are served from one; anything else
			// allocates rather than being flattened onto the nearest singleton, which would discard it.
			switch (disposition)
			{
				case MessageDisposition.Handled:
					return CachedSuccess;
				case MessageDisposition.ServedFromCache:
					return CachedSuccessFromCache;
				default:
					break;
			}
		}

		return new BasicMessageResult(succeeded: true, disposition: disposition, validationResult: validationResult,
			authorizationResult: authorizationResult);
	}

	/// <summary>
	/// Creates a successful message result with a value.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="value"> The value to return. </param>
	/// <returns> A successful message result with value. </returns>
	public static IMessageResult<T> Success<T>(T value) => new BasicMessageResult<T>(succeeded: true, value: value);

	/// <summary>
	/// Creates a successful message result with a value and full context.
	/// </summary>
	/// <typeparam name="T"> The type of the value. </typeparam>
	/// <param name="value"> The value to return. </param>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <param name="disposition"> Describes how the result was produced. </param>
	/// <returns> A successful message result with value. </returns>
	public static IMessageResult<T> Success<T>(
		T value,
				RoutingDecision? routingDecision = null,
				object? validationResult = null,
				object? authorizationResult = null,
				MessageDisposition disposition = MessageDisposition.Handled)
	{
		_ = routingDecision;

		return new BasicMessageResult<T>(
			succeeded: true,
			value: value,
			disposition: disposition,
			validationResult: validationResult,
			authorizationResult: authorizationResult);
	}

	/// <summary>
	/// Creates a failed message result.
	/// </summary>
	/// <param name="error"> The error message. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult Failed(string error) => new BasicMessageResult(succeeded: false, errorMessage: error);

	/// <summary>
	/// Creates a failed message result with problem details.
	/// </summary>
	/// <param name="problemDetails"> The problem details. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult Failed(IMessageProblemDetails problemDetails) =>
		new BasicMessageResult(succeeded: false, errorMessage: problemDetails?.Detail, problemDetails: problemDetails);

	/// <summary>
	/// Creates a failed message result with a generic type parameter.
	/// </summary>
	/// <typeparam name="T"> The type of the expected return value. </typeparam>
	/// <param name="errorMessage"> The error message. </param>
	/// <param name="problemDetails"> The problem details. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult<T> Failed<T>(string? errorMessage, IMessageProblemDetails? problemDetails = null) =>
		new BasicMessageResult<T>(succeeded: false, value: default, errorMessage: errorMessage, problemDetails: problemDetails);

	/// <summary>
	/// Creates a failed message result from an exception, preserving the full exception detail.
	/// </summary>
	/// <param name="exception"> The exception that caused the failure. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult Failed(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new BasicMessageResult(succeeded: false, errorMessage: exception.ToString());
	}

	/// <summary>
	/// Creates a failed message result with a generic type parameter from an exception, preserving the full exception detail.
	/// </summary>
	/// <typeparam name="T"> The type of the expected return value. </typeparam>
	/// <param name="exception"> The exception that caused the failure. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult<T> Failed<T>(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new BasicMessageResult<T>(succeeded: false, value: default, errorMessage: exception.ToString());
	}

	/// <summary>
	/// Creates a failed message result with problem details and additional context.
	/// </summary>
	/// <param name="problemDetails"> The problem details. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <returns> A failed message result. </returns>
	public static IMessageResult Failed(
		IMessageProblemDetails? problemDetails,
		object? validationResult,
		object? authorizationResult) =>
		new BasicMessageResult(
			succeeded: false,
			errorMessage: problemDetails?.Detail,
			problemDetails: problemDetails,
			validationResult: validationResult,
			authorizationResult: authorizationResult);

	/// <summary>
	/// Creates a cancelled message result.
	/// </summary>
	/// <returns> A cancelled message result indicating the operation was cancelled. </returns>
	public static IMessageResult Cancelled() => CachedCancelled;

	/// <summary>
	/// Creates a cancelled message result with a generic type parameter.
	/// Returns a cached instance per type parameter to avoid unnecessary allocations.
	/// </summary>
	/// <typeparam name="T"> The type of the expected return value. </typeparam>
	/// <returns> A cancelled message result. </returns>
	public static IMessageResult<T> Cancelled<T>() => CachedCancelledResult<T>.Instance;

	private static class CachedCancelledResult<T>
	{
		public static readonly IMessageResult<T> Instance = new BasicMessageResult<T>(succeeded: false, value: default);
	}
}
