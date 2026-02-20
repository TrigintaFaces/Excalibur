// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Static factory methods for creating message results.
/// </summary>
public static class MessageResult
{
	private static readonly IMessageResult CachedSuccess = new BasicMessageResult(succeeded: true);
	private static readonly IMessageResult CachedSuccessFromCache = new BasicMessageResult(succeeded: true, cacheHit: true);

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
		new BasicMessageResult<T>(succeeded: true, value: value, cacheHit: true);

	/// <summary>
	/// Creates a successful message result with additional parameters.
	/// </summary>
	/// <param name="routingDecision"> The routing decision. </param>
	/// <param name="validationResult"> The validation result. </param>
	/// <param name="authorizationResult"> The authorization result. </param>
	/// <param name="cacheHit"> Whether this was a cache hit. </param>
	/// <returns> A successful message result. </returns>
	public static IMessageResult Success(
			RoutingDecision? routingDecision,
			object? validationResult,
			object? authorizationResult,
			bool cacheHit = false)
	{
		_ = routingDecision;

		if (validationResult is null && authorizationResult is null)
		{
			return cacheHit ? CachedSuccessFromCache : CachedSuccess;
		}

		return new BasicMessageResult(succeeded: true, cacheHit: cacheHit, validationResult: validationResult,
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
	/// <param name="cacheHit"> Whether this result came from cache. </param>
	/// <returns> A successful message result with value. </returns>
	public static IMessageResult<T> Success<T>(
		T value,
				RoutingDecision? routingDecision = null,
				object? validationResult = null,
				object? authorizationResult = null,
				bool cacheHit = false)
	{
		_ = routingDecision;

		return new BasicMessageResult<T>(
			succeeded: true,
			value: value,
			cacheHit: cacheHit,
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
}
