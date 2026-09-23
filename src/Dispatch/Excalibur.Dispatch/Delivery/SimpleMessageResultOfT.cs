// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Routing;

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
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleMessageResultOfT{T}"/> class for success cases
	/// with an explicit disposition and minimal metadata.
	/// </summary>
	/// <param name="value">The return value.</param>
	/// <param name="disposition">Describes how the result was produced.</param>
	public SimpleMessageResultOfT(T? value, MessageDisposition disposition)
	{
		Succeeded = true;
		ReturnValue = value;
		Disposition = disposition;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SimpleMessageResultOfT{T}"/> class with full parameters.
	/// </summary>
	/// <param name="value"> The return value. </param>
	/// <param name="succeeded"> Whether the operation succeeded. </param>
	/// <param name="errorMessage"> Optional error message. </param>
	/// <param name="disposition"> Describes how the result was produced. </param>
	/// <param name="routingDecision"> Optional routing decision metadata. </param>
	/// <param name="validationResult"> Optional validation result. </param>
	/// <param name="authorizationResult"> Optional authorization result. </param>
	/// <param name="problemDetails"> Optional problem details. </param>
	public SimpleMessageResultOfT(
		T? value,
		bool succeeded,
		string? errorMessage = null,
		MessageDisposition disposition = MessageDisposition.Handled,
		RoutingDecision? routingDecision = null,
		object? validationResult = null,
		object? authorizationResult = null,
		IMessageProblemDetails? problemDetails = null)
	{
		Succeeded = succeeded;
		ReturnValue = value;
		Disposition = disposition;
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
	public MessageDisposition Disposition { get; }

	// MA0041: Property must remain instance member to implement IMessageResult interface.
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

	// Explicit interface implementations for compatibility with Excalibur.Dispatch

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => _metadata?.ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => _metadata?.AuthorizationResult;
}
