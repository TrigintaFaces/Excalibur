// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.LeaderElection.Fencing;

/// <summary>
/// Thrown by <see cref="IFencingTokenProvider.IssueTokenAsync(string, System.Threading.CancellationToken)"/>
/// when the next monotonic token would exceed the provider's token domain (the counter is exhausted).
/// </summary>
/// <remarks>
/// <para>
/// Fencing tokens MUST be strictly monotonic: a wrapped or reused value would let a stale leader validate
/// as current, producing split-brain. When the domain is exhausted the only safe behavior is to
/// <b>fail closed</b> — refuse to mint, and therefore refuse to grant or renew leadership. A leader that
/// hits exhaustion mid-tenure must relinquish rather than continue with an unsafe token.
/// </para>
/// <para>
/// Exhaustion is practically unreachable for an <see cref="long"/> domain (Mongo/Consul self-minting) but
/// is reachable for narrower native counters (e.g. a Kubernetes <c>Lease.spec.leaseTransitions</c>, which is
/// a 32-bit value).
/// </para>
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
