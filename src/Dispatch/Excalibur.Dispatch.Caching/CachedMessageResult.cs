// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Represents a cached message result with a strongly-typed return value.
/// Used to wrap cached values when returning from cache hits without executing the handler.
/// </summary>
/// <typeparam name="T">The type of the return value.</typeparam>
[SuppressMessage(
		"Performance",
		"CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via reflection in caching middleware for cache hits.")]
internal sealed class CachedMessageResult<T> : IMessageResult<T>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CachedMessageResult{T}"/> class.
	/// </summary>
	/// <param name="value">The cached return value.</param>
	public CachedMessageResult(T? value)
	{
		Succeeded = true;
		ReturnValue = value;
		CacheHit = true;
		ProblemDetails = null;
	}

	/// <inheritdoc />
	public bool Succeeded { get; }

	/// <inheritdoc />
	public T? ReturnValue { get; }

	/// <inheritdoc />
	public bool CacheHit { get; }

	/// <inheritdoc />
	public IMessageProblemDetails? ProblemDetails { get; }

	/// <inheritdoc />
	public string? ErrorMessage => null;

	/// <inheritdoc />
	object? IMessageResult.ValidationResult => null;

	/// <inheritdoc />
	object? IMessageResult.AuthorizationResult => null;
}
