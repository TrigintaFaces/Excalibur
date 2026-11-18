// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// The result of asking a store for custody of an erasure request: an outcome, and — only when the
/// request was granted — the identity the caller must present on every attestation write for that run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a type rather than a nullable identity.</b> A <see langword="string"/>? can say "you
/// hold it" and "you do not", and cannot say WHY not. The three refusals have different futures — a
/// request another process holds returns by itself when that claim lapses, a request in an unexpected
/// status does not, and a request that does not exist never will — so collapsing them forces every
/// caller to re-read the row to decide what to do next, which is a second linearization point exactly
/// where the first one was bought.
/// </para>
/// <para>
/// <b>The identity is OPAQUE.</b> It answers one question, by equality: is this write coming from the
/// claim currently held? It carries no order, no issue sequence and no issuer, and comparing two of them
/// for anything but equality is a defect. It is not a fencing token: a fencing token orders successive
/// tenures against a resource shared by many rows, and the resource here is one request row and the
/// attestation carried on it, where the store itself decides which of two contenders wins because the
/// claim is a single conditional update.
/// </para>
/// <para>
/// <b>It never leaves the executing method.</b> Nothing puts it on a status, a result DTO or the wire. A
/// caller that cannot obtain one cannot present one, which is what makes the opacity above a property of
/// the design rather than a rule somebody has to remember.
/// </para>
/// <para>
/// <b>A record struct rather than a plain one, and not for brevity.</b> A value type here must implement
/// equality -- the analyzers require it of every public struct -- so the choice is between a synthesised
/// equality surface and a hand-written one, never between having it and not. The synthesised one is
/// correct by construction. It does mean equality is publicly available on a type whose identity field
/// must never be compared for ORDER; that is a different operation and the remarks above are where it is
/// forbidden.
/// </para>
/// </remarks>
public readonly record struct ErasureClaimResult
{
	private ErasureClaimResult(ErasureClaimOutcome outcome, string? claimIdentity)
	{
		Outcome = outcome;
		ClaimIdentity = claimIdentity;
	}

	/// <summary>
	/// Gets what the store decided.
	/// </summary>
	/// <value>
	/// The outcome. <see cref="ErasureClaimOutcome.NotDetermined"/> on a <see langword="default"/> value,
	/// which no store returns and no caller may act on.
	/// </value>
	public ErasureClaimOutcome Outcome { get; }

	/// <summary>
	/// Gets the identity this caller must present on every attestation write for this run.
	/// </summary>
	/// <value>
	/// The granted identity, and <see langword="null"/> on every other outcome. Non-null if and only if
	/// <see cref="Outcome"/> is <see cref="ErasureClaimOutcome.Granted"/>.
	/// </value>
	public string? ClaimIdentity { get; }

	/// <summary>
	/// The caller now holds the request.
	/// </summary>
	/// <param name="claimIdentity">The minted identity. Must be non-empty.</param>
	/// <returns>A granted result carrying <paramref name="claimIdentity"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="claimIdentity"/> is null, empty or whitespace.</exception>
	/// <remarks>
	/// The argument is checked rather than trusted because a granted result with no identity is the one
	/// shape a caller cannot act on and cannot detect: it would present null on its first fenced write,
	/// which is the predicate asserting that NOBODY holds the request.
	/// </remarks>
	public static ErasureClaimResult Granted(string claimIdentity)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claimIdentity);

		return new(ErasureClaimOutcome.Granted, claimIdentity);
	}

	/// <summary>
	/// Another process holds a claim that has not lapsed.
	/// </summary>
	/// <returns>A refusal carrying no identity.</returns>
	public static ErasureClaimResult AlreadyClaimed() => new(ErasureClaimOutcome.AlreadyClaimed, null);

	/// <summary>
	/// The request is not in the status the caller expected.
	/// </summary>
	/// <returns>A refusal carrying no identity.</returns>
	public static ErasureClaimResult StatusMismatch() => new(ErasureClaimOutcome.StatusMismatch, null);

	/// <summary>
	/// No such request exists in the ambient tenant.
	/// </summary>
	/// <returns>A refusal carrying no identity.</returns>
	public static ErasureClaimResult NotFound() => new(ErasureClaimOutcome.NotFound, null);
}
