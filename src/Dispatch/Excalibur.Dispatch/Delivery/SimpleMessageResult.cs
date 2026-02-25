// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Simple implementation of IMessageResult (non-generic).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Framework type available for consumer instantiation scenarios")]
internal sealed class SimpleMessageResult : IMessageResult
{
	public static readonly IMessageResult SuccessResult = new SimpleMessageResult(cacheHit: false);
	public static readonly IMessageResult SuccessCacheHitResult = new SimpleMessageResult(cacheHit: true);

	private SimpleMessageResult(bool cacheHit)
	{
		CacheHit = cacheHit;
	}

	/// <inheritdoc/>
	public bool Succeeded { get; } = true;

	/// <inheritdoc/>
	public IMessageProblemDetails? ProblemDetails => null;

	public static RoutingDecision? RoutingDecision => null;

	public static IValidationResult? ValidationResult => null;

	public static IAuthorizationResult? AuthorizationResult => null;

	/// <inheritdoc/>
	public bool CacheHit { get; }

	// R0.8: Make property static - property must be instance member to implement IMessageResult interface
#pragma warning disable MA0041

	/// <summary>
	/// Gets the error message when the operation fails, or null when successful.
	/// </summary>
	/// <value>The current <see cref="ErrorMessage"/> value.</value>
	public string? ErrorMessage => ProblemDetails?.Detail;

#pragma warning restore MA0041

	/// <summary>
	/// Kept for backward compatibility.
	/// </summary>
	public bool Success => Succeeded;

	public string? Error => ProblemDetails?.Detail;

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => AuthorizationResult;
}
