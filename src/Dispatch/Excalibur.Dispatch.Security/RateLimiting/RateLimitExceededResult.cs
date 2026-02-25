// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Result returned when rate limit is exceeded.
/// </summary>
public sealed class RateLimitExceededResult : IMessageResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the message processing succeeded.
	/// </summary>
	/// <value>
	/// <see langword="false"/> for rate limit exceeded.
	/// </value>
	public bool Succeeded { get; set; }

	/// <summary>
	/// Gets or sets the problem details for the rate limit exceeded.
	/// </summary>
	/// <value>
	/// Problem details describing the rate limit exceeded, if available.
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
	/// A human-readable error message describing the rate limit exceeded.
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
	/// Gets or sets the number of milliseconds to wait before retrying.
	/// </summary>
	/// <value>
	/// The number of milliseconds to wait before retrying.
	/// </value>
	public int RetryAfterMilliseconds { get; set; }

	/// <summary>
	/// Gets or sets the rate limit key that was exceeded.
	/// </summary>
	/// <value>
	/// The rate limit key that was exceeded, or <see langword="null"/> if not applicable.
	/// </value>
	public string? RateLimitKey { get; set; }
}
