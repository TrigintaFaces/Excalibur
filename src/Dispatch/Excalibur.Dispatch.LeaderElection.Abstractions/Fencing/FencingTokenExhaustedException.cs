// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Thrown by <see cref="IFencingTokenIssuer.IssueTokenAsync(string, System.Threading.CancellationToken)"/>
/// when its domain cannot generate another strictly positive monotonic token.
/// </summary>
/// <remarks>
/// The token domain must never wrap or reuse a value: either would admit a stale leader. Exhaustion is
/// permanent for the attempt, so a retrying mint caller propagates this exception and relinquishes
/// rather than declaring a tenure with an unsafe token. A provider's bounded domain need not fill the
/// entire Int64 range. Read-native providers do not implement the separate minting capability.
/// </remarks>
public sealed class FencingTokenExhaustedException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FencingTokenExhaustedException"/> class.
	/// </summary>
	public FencingTokenExhaustedException()
		: base("The fencing token domain is exhausted; a new monotonic token cannot be issued.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FencingTokenExhaustedException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public FencingTokenExhaustedException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FencingTokenExhaustedException"/> class with a message
	/// and an inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The inner exception.</param>
	public FencingTokenExhaustedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the protected resource whose token domain is exhausted, when known.
	/// </summary>
	public string? ResourceId { get; init; }
}
