// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Message result indicating that processing timed out.
/// </summary>
internal sealed class TimeoutMessageResult : IMessageResult
{
	private readonly MessageTimeoutException _timeoutException;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeoutMessageResult" /> class.
	/// </summary>
	/// <param name="timeoutException"> The timeout exception that occurred. </param>
	public TimeoutMessageResult(MessageTimeoutException timeoutException)
	{
		_timeoutException = timeoutException ?? throw new ArgumentNullException(nameof(timeoutException));
		ProblemDetails = new TimeoutProblemDetails(_timeoutException);
	}

	/// <inheritdoc />
	public static RoutingDecision? RoutingDecision => null;

	/// <inheritdoc />
	public static IValidationResult? ValidationResult => null;

	/// <inheritdoc />
	public static IAuthorizationResult? AuthorizationResult => null;

	/// <inheritdoc />
	public bool Succeeded => false;

	/// <inheritdoc />
	public string? ErrorMessage => _timeoutException.Message;

	/// <inheritdoc />
	public IMessageProblemDetails? ProblemDetails { get; }

	/// <inheritdoc />
	public bool CacheHit => false;

	// Explicit interface implementations for compatibility with Excalibur.Dispatch.Abstractions

	/// <inheritdoc/>
	object? IMessageResult.ValidationResult => ValidationResult;

	/// <inheritdoc/>
	object? IMessageResult.AuthorizationResult => AuthorizationResult;
}
