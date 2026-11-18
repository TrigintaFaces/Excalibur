// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// Why a request was, or was not, granted to a caller for execution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Custody and outcome are different questions, and this enumeration exists because they were being
/// asked on one axis.</b> An erasure request's status says what HAPPENED to it. Whether some process is
/// currently responsible for it is a separate fact, and it used to be inferred from the status: a request
/// in the in-progress status was taken to be in somebody's hands. Nothing recorded WHOSE hands or for how
/// long, so a request whose executor died was in-progress forever, every query that looked for work
/// correctly excluded it, and nothing anywhere reported that a subject's data had not been erased.
/// </para>
/// <para>
/// <b>Zero is deliberately not a grant.</b> A <see langword="default"/> value, an unassigned field and a
/// task that completed without setting a result all read as <see cref="NotDetermined"/>. Had zero meant
/// granted, each of those would have handed a caller permission to destroy a data subject's keys that
/// nothing had granted. Implementations must never return it.
/// </para>
/// <para>
/// <b>Test for <see cref="Granted"/> by name.</b> A caller that proceeds whenever the outcome is not one
/// of the refusals it knows about gives every member added later the meaning "go ahead".
/// </para>
/// </remarks>
public enum ErasureClaimOutcome
{
	/// <summary>
	/// Nothing decided this. The caller holds no claim and must not execute.
	/// </summary>
	/// <remarks>
	/// Reachable only from a <see langword="default"/> value, so a caller that sees it has been handed a
	/// result nobody produced. It exists to make that case nameable rather than to be returned.
	/// </remarks>
	NotDetermined = 0,

	/// <summary>
	/// The caller now holds the request, and the result carries the identity it must present on every
	/// attestation write it makes for this run.
	/// </summary>
	Granted = 1,

	/// <summary>
	/// Another process holds a claim that has not lapsed. Skip quietly and release any retry budget held
	/// against this request.
	/// </summary>
	/// <remarks>
	/// <b>Releasing the retry budget is the part that is easy to miss.</b> The request comes back on its
	/// own when the holder's claim lapses, so spending a retry attempt on another instance's work drives
	/// this request toward a terminal failure it never earned.
	/// </remarks>
	AlreadyClaimed = 2,

	/// <summary>
	/// The request exists and is not in the status the caller expected, so this path will never be able to
	/// execute it. Stop offering it here.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="AlreadyClaimed"/> because the two have different futures: a claimed request
	/// returns by itself, and a request in the wrong status does not.
	/// </remarks>
	StatusMismatch = 3,

	/// <summary>
	/// No such request exists in the ambient tenant. Permanent: drop it and report it.
	/// </summary>
	NotFound = 4,
}
