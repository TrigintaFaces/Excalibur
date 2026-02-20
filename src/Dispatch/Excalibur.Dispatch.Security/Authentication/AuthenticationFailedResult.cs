// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Result returned when authentication fails.
/// </summary>
public sealed class AuthenticationFailedResult : IMessageResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the message processing succeeded.
	/// </summary>
	/// <value>
	/// <see langword="false"/> for authentication failures.
	/// </value>
	public bool Succeeded { get; set; }

	/// <summary>
	/// Gets or sets the problem details for the authentication failure.
	/// </summary>
	/// <value>
	/// Problem details describing the authentication failure, if available.
	/// </value>
	public IMessageProblemDetails? ProblemDetails { get; set; }

	/// <summary>
	/// Gets or sets the routing decision.
	/// </summary>
	/// <value>
	/// The routing decision, or <see langword="null"/> if routing was not performed.
	/// </value>
	public RoutingDecision? RoutingDecision { get; set; }

	/// <summary>
	/// Gets or sets the validation result.
	/// </summary>
	/// <value>
	/// The validation result, or <see langword="null"/> if validation was not performed.
	/// </value>
	public object? ValidationResult { get; set; }

	/// <summary>
	/// Gets or sets the authorization result.
	/// </summary>
	/// <value>
	/// The authorization result, or <see langword="null"/> if authorization was not performed.
	/// </value>
	public object? AuthorizationResult { get; set; }

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>
	/// A human-readable error message describing the authentication failure.
	/// </value>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the result was retrieved from cache.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if the result was retrieved from cache; otherwise, <see langword="false"/>.
	/// </value>
	public bool CacheHit { get; set; }

	/// <summary>
	/// Gets or sets the reason for authentication failure.
	/// </summary>
	/// <value>
	/// The specific reason why authentication failed.
	/// </value>
	public AuthenticationFailureReason Reason { get; set; }
}
