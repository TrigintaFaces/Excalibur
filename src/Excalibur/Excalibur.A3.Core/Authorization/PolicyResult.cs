// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Represents the result of a policy evaluation.
/// </summary>
/// <remarks>
/// <para>
/// <b>A value type, and the reason is measured.</b> This is returned by every <c>HasGrant</c> call, and an
/// authorization check runs on the dispatch path for every authorized message. As a reference type it cost
/// one 24-byte heap allocation per check — for three booleans that the bool-returning overloads discard
/// immediately.
/// </para>
/// <para>
/// <b>The runtime sometimes removed that allocation, which is worse than it sounds.</b> Because the result
/// never escapes <c>HasGrant</c>, escape analysis can stack-allocate it — but only once the method reaches
/// tier-1. Measured: the same authorization check allocated 24 bytes per call early in a process and zero
/// later, with nothing in the code changing between the two. An allocation that disappears when the
/// optimiser gets around to it is not a guarantee a consumer can rely on, and it cannot be asserted by a
/// test without the test becoming a measurement of the JIT. As a struct there is no allocation to
/// eliminate, so zero is structural rather than opportunistic.
/// </para>
/// <para>
/// <b><see cref="IsAuthorized"/> is DERIVED, not stored, and that is a correctness property rather than a
/// saving.</b> With three independently settable flags, <c>IsAuthorized = false</c> alongside
/// <c>HasActivityGrant = true</c> was expressible — a result asserting both that a grant exists and that
/// the caller is unauthorized. Nothing rejected it and nothing could, because the inconsistency was in the
/// data rather than in any code path. Computing it from the two grant flags makes that state
/// unrepresentable.
/// </para>
/// <para>
/// Equality is written out rather than synthesized by a record, so the published surface carries exactly
/// these members and no compiler-generated extras.
/// </para>
/// </remarks>
public readonly struct PolicyResult : IEquatable<PolicyResult>
{
	/// <summary>
	/// Gets a value indicating whether the activity is authorized.
	/// </summary>
	/// <value><see langword="true"/> if the activity is authorized; otherwise, <see langword="false"/>.</value>
	/// <remarks>
	/// Derived from <see cref="HasActivityGrant"/> and <see cref="HasResourceGrant"/>. Authorization is the
	/// existence of either grant, so this cannot disagree with them.
	/// </remarks>
	public bool IsAuthorized => HasActivityGrant || HasResourceGrant;

	/// <summary>
	/// Gets a value indicating whether the activity grant exists.
	/// </summary>
	/// <value><see langword="true"/> if the activity grant exists; otherwise, <see langword="false"/>.</value>
	public bool HasActivityGrant { get; init; }

	/// <summary>
	/// Gets a value indicating whether the resource grant exists.
	/// </summary>
	/// <value><see langword="true"/> if the resource grant exists; otherwise, <see langword="false"/>.</value>
	public bool HasResourceGrant { get; init; }

	/// <summary>Determines whether two results carry the same grants.</summary>
	/// <param name="left">The first result.</param>
	/// <param name="right">The second result.</param>
	/// <returns><see langword="true"/> if both grant flags match.</returns>
	public static bool operator ==(PolicyResult left, PolicyResult right) => left.Equals(right);

	/// <summary>Determines whether two results carry different grants.</summary>
	/// <param name="left">The first result.</param>
	/// <param name="right">The second result.</param>
	/// <returns><see langword="true"/> if either grant flag differs.</returns>
	public static bool operator !=(PolicyResult left, PolicyResult right) => !left.Equals(right);

	/// <inheritdoc/>
	public bool Equals(PolicyResult other) =>
		HasActivityGrant == other.HasActivityGrant && HasResourceGrant == other.HasResourceGrant;

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is PolicyResult other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => HashCode.Combine(HasActivityGrant, HasResourceGrant);
}
