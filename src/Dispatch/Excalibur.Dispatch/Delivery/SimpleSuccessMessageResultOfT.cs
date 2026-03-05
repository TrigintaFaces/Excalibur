// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Lean success-only implementation of <see cref="IMessageResult{T}"/> for hot-path dispatch.
/// </summary>
/// <typeparam name="T">The response type.</typeparam>
internal sealed class SimpleSuccessMessageResultOfT<T> : IMessageResult<T>
{
	public SimpleSuccessMessageResultOfT(T? value, bool cacheHit)
	{
		ReturnValue = value;
		CacheHit = cacheHit;
	}

	/// <inheritdoc/>
	public bool Succeeded => true;

	/// <inheritdoc/>
	public T? ReturnValue { get; }

	/// <inheritdoc/>
	public IMessageProblemDetails? ProblemDetails => null;

	/// <inheritdoc/>
	public RoutingDecision? RoutingDecision => null;

	/// <inheritdoc/>
	public bool CacheHit { get; }

	/// <inheritdoc/>
	public string? ErrorMessage => null;

	/// <summary>
	/// Kept for backward compatibility.
	/// </summary>
	public bool Success => true;

	public T? Value => ReturnValue;

	public string? Error => null;

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => null;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => null;
}
