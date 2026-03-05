// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Simple implementation of <see cref="IMessageResult{T}"/>.
/// </summary>
/// <typeparam name="T"> The type of the return value. </typeparam>
internal sealed class SimpleMessageResultOfT<T> : IMessageResult<T>
{
	private sealed class ResultMetadata(
		RoutingDecision? routingDecision,
		object? validationResult,
		object? authorizationResult,
		IMessageProblemDetails? problemDetails)
	{
		public RoutingDecision? RoutingDecision { get; } = routingDecision;

		public object? ValidationResult { get; } = validationResult;

		public object? AuthorizationResult { get; } = authorizationResult;

		public IMessageProblemDetails? ProblemDetails { get; } = problemDetails;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleMessageResultOfT{T}"/> class for success cases.
	/// </summary>
	/// <param name="value"> The return value. </param>
	public SimpleMessageResultOfT(T? value)
	{
		Succeeded = true;
		ReturnValue = value;
		CacheHit = false;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleMessageResultOfT{T}"/> class for success cases
	/// with explicit cache-hit state and minimal metadata.
	/// </summary>
	/// <param name="value">The return value.</param>
	/// <param name="cacheHit">Whether this was a cache-hit result.</param>
	public SimpleMessageResultOfT(T? value, bool cacheHit)
	{
		Succeeded = true;
		ReturnValue = value;
		CacheHit = cacheHit;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleMessageResultOfT{T}"/> class with full parameters.
	/// </summary>
	/// <param name="value"> The return value. </param>
	/// <param name="succeeded"> Whether the operation succeeded. </param>
	/// <param name="errorMessage"> Optional error message. </param>
	/// <param name="cacheHit"> Whether this was a cache hit. </param>
	/// <param name="routingDecision"> Optional routing decision metadata. </param>
	/// <param name="validationResult"> Optional validation result. </param>
	/// <param name="authorizationResult"> Optional authorization result. </param>
	/// <param name="problemDetails"> Optional problem details. </param>
	public SimpleMessageResultOfT(
		T? value,
		bool succeeded,
		string? errorMessage = null,
		bool cacheHit = false,
		RoutingDecision? routingDecision = null,
		object? validationResult = null,
		object? authorizationResult = null,
		IMessageProblemDetails? problemDetails = null)
	{
		Succeeded = succeeded;
		ReturnValue = value;
		CacheHit = cacheHit;
		_errorMessage = errorMessage;

		if (routingDecision is not null ||
		    validationResult is not null ||
		    authorizationResult is not null ||
		    problemDetails is not null)
		{
			_metadata = new ResultMetadata(routingDecision, validationResult, authorizationResult, problemDetails);
		}
	}

	private readonly string? _errorMessage;
	private readonly ResultMetadata? _metadata;

	/// <inheritdoc/>
	public bool Succeeded { get; }

	/// <inheritdoc/>
	public T? ReturnValue { get; }

	/// <inheritdoc/>
	public IMessageProblemDetails? ProblemDetails => _metadata?.ProblemDetails;

	/// <inheritdoc/>
	public RoutingDecision? RoutingDecision => _metadata?.RoutingDecision;

	/// <inheritdoc/>
	public bool CacheHit { get; }

	// R0.8: Make property static - property must be instance member to implement IMessageResult interface
#pragma warning disable MA0041

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage => ProblemDetails?.Detail ?? _errorMessage;

#pragma warning restore MA0041

	/// <summary>
	/// Kept for backward compatibility.
	/// </summary>
	public bool Success => Succeeded;

	public T? Value => ReturnValue;

	public string? Error => ProblemDetails?.Detail;

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => _metadata?.ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => _metadata?.AuthorizationResult;
}
