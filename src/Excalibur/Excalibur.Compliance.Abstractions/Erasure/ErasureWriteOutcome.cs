// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// What a fenced write against an erasure request actually did.
/// </summary>
/// <remarks>
/// <para>
/// <b>A <see langword="bool"/> conflated a race with a defect.</b> The status write returned
/// <see langword="true"/> or <see langword="false"/>, and false meant any of: the row is gone, the
/// transition is illegal, or another executor has taken the request. The first two are caller errors
/// worth reporting; the third is the ordinary outcome of an ordinary race and the only correct response
/// is to stand down silently. A caller that cannot tell them apart either logs a defect on every normal
/// handover or reports nothing on a real one.
/// </para>
/// <para>
/// <b>Zero is deliberately not a success.</b> A <see langword="default"/> value, an unassigned field and
/// a task that completed without setting a result all read as <see cref="NotDetermined"/>. Had zero meant
/// applied, each of those would assert a durable write that never happened — and on the completion write
/// that assertion is a signed certificate. Implementations must never return it and must refuse a
/// presented default.
/// </para>
/// <para>
/// <b>Test for <see cref="Applied"/> by name</b>, never by excluding the refusals you know about, or
/// every member added later begins life meaning "it worked".
/// </para>
/// </remarks>
public enum ErasureWriteOutcome
{
	/// <summary>
	/// Nothing decided this. The write must not be treated as having been applied or refused.
	/// </summary>
	/// <remarks>
	/// Reachable only from a <see langword="default"/> value, so a caller that sees it has been handed a
	/// result nobody produced. It exists to make that case nameable rather than to be returned.
	/// </remarks>
	NotDetermined = 0,

	/// <summary>
	/// The write was applied. Proceed.
	/// </summary>
	Applied = 1,

	/// <summary>
	/// The caller's claim is no longer the one the request carries, so nothing was written.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Stand down silently.</b> Write nothing further for this request — including any terminal-failure
	/// record — and do not report a defect. Another executor holds the request and is handling it, and a
	/// terminal failure written here would mark as failed an erasure that is in progress elsewhere.
	/// </para>
	/// <para>
	/// <b>A lapse alone does not produce this.</b> Only a RE-CLAIM does. An executor whose run outlasted
	/// its own lease, but whom nobody superseded, still holds the identity the request carries and its
	/// writes still apply — otherwise an erasure that destroyed keys could never issue the certificate
	/// attesting it.
	/// </para>
	/// </remarks>
	ClaimLost = 2,

	/// <summary>
	/// The request exists and the caller holds its claim, but the transition is not legal from the current
	/// status, or an expected status was supplied and did not match. Report it.
	/// </summary>
	/// <remarks>
	/// <b>This is a caller error, not a race, and that is a change in what the status predicate means.</b>
	/// Before custody was enforced by the claim, a status refusal was how a loser discovered it had lost;
	/// now the claim answers that question, so a status refusal while holding the claim says the caller
	/// asked for a transition its own state machine should never have requested.
	/// </remarks>
	StatusRefused = 3,

	/// <summary>
	/// No such request exists in the ambient tenant.
	/// </summary>
	/// <remarks>
	/// On a write made while holding a claim this is an anomaly rather than an ordinary outcome: the row
	/// went away underneath a live claim. Report it.
	/// </remarks>
	NotFound = 4,
}
