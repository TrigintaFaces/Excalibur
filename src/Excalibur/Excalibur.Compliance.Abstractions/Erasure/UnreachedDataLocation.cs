// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// A place the erasure did NOT reach, recorded on the certificate so a partly-successful erasure still
/// produces evidence of what was and was not done.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not an exception, and the distinction is the whole point of the type.</b> An
/// <see cref="ErasureException"/> records data lawfully RETAINED and carries the basis for retaining
/// it. This records data that was simply not reached, for which <i>no lawful basis is claimed</i>. The
/// two must never be confused: recording an unmet obligation as an exception would present a failure as
/// a legal exemption.
/// </para>
/// <para>
/// A location appears in one list or the other. Never both.
/// </para>
/// </remarks>
public sealed record UnreachedDataLocation
{
	/// <summary>The kind of store the data remains in.</summary>
	/// <remarks>
	/// The store kind's own value, so a kind an application defined for itself names itself here rather
	/// than being flattened to "other".
	/// </remarks>
	public required string StoreKind { get; init; }

	/// <summary>
	/// Why it was not reached, in one sentence describing the MECHANISM.
	/// </summary>
	/// <remarks>
	/// Not an error code and not an exception message. Someone auditing this asks how the data survived,
	/// not which call failed — for example, that erasure rewrites stored events in place and does not
	/// notify derived read models, so a read model that already folded the subject's events keeps them
	/// until it is replayed.
	/// </remarks>
	public required string Mechanism { get; init; }

	/// <summary>
	/// Always <see langword="false"/>. No lawful basis is claimed for data in this list.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Stated as a field rather than left to the absence of a basis, and deliberately not settable.</b>
	/// An absent basis is ambiguous — someone reading this adversarially could argue the silence implied
	/// one. An explicit <c>false</c> cannot be read that way, and because the property has no setter the
	/// document cannot be made to claim otherwise.
	/// </para>
	/// </remarks>
	public bool LawfulBasisClaimed => false;

	/// <summary>
	/// What the controller must now do, described as an outcome rather than as an API call.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Never name a method here.</b> This document is signed and retained for years; the procedure is
	/// already carried on surfaces that can be corrected afterwards — the operation's own error text, the
	/// logs, and the published documentation — and those are where it belongs. An instruction frozen into
	/// a signed artifact cannot be amended when the operation is renamed, and worse, cannot be amended if
	/// a later change makes the instruction impossible to carry out. What survives both is the obligation
	/// and its cost; the procedure does not.
	/// </para>
	/// </remarks>
	public required string ControllerObligation { get; init; }

	/// <summary>What discharging the obligation costs the controller operationally.</summary>
	public required RemediationCost RemediationCost { get; init; }
}

/// <summary>
/// The operational cost of discharging an outstanding erasure obligation.
/// </summary>
/// <remarks>
/// Stated on the certificate because a controller who does not know the cost will schedule the remedy as
/// routine background work. Where the remedy requires exclusive access, running it against a live writer
/// can leave the subject cleared from some records and not others, with no record of which — strictly
/// worse than not attempting it.
/// </remarks>
public enum RemediationCost
{
	/// <summary>Not known.</summary>
	Unknown = 0,

	/// <summary>Can be discharged while the system continues serving traffic.</summary>
	Online = 1,

	/// <summary>
	/// Requires the affected read model to be taken out of service for the duration.
	/// </summary>
	RequiresReadModelOffline = 2,
}
