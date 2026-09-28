// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

namespace Excalibur.EventSourcing;

/// <summary>
/// Which of the three things a projection's stored position can be saying.
/// </summary>
/// <remarks>
/// <para>
/// A projection's position is not a number with a missing case. It is an assertion about which prefix of
/// the stream is folded into the state, and there are three assertions a row can be making — not two.
/// Carrying them in a <see cref="long"/> with a null forces every producer that has no number to pick
/// one of the two available answers, and the fabrication is then indistinguishable from a measurement.
/// </para>
/// <para>
/// The distinction that matters is between the second and third members: both have no number, and a
/// writer must ADOPT one and REFUSE the other. Conflating them is how a projection comes to assert a
/// prefix it does not hold.
/// </para>
/// </remarks>
public enum ProjectionPositionKind
{
	/// <summary>
	/// The state is NOT a fold over any prefix, so no position could describe it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is deliberately the ZERO value, so that <c>default(ProjectionPosition)</c> is the
	/// fail-safe state.</b> A struct can be default-constructed anywhere -- an array element, an
	/// uninitialised field, an explicit <c>default</c> -- and nothing in the language prevents it, so
	/// the question is not whether that happens but what it MEANS when it does. With
	/// <c>Positioned</c> at zero the default asserted "this state is a fold over everything up to
	/// position 0": a definite, plausible, wrong claim, and indistinguishable from a measured one.
	/// Unplaceable is the honest reading of a value nobody set -- we do not know that it is a fold over
	/// any prefix -- and it is also the conservative one, because it refuses adoption.
	/// </para>
	/// <para>
	/// Produced by the unconditional <see cref="IProjectionStore{TProjection}.UpsertAsync"/>, which
	/// replaces the state with a value the store cannot relate to the stream. A positioned writer must
	/// NEVER adopt such a row: folding the next batch onto it and stamping that batch's position would
	/// assert a prefix the state does not contain, and every event below that position would be missing
	/// from the read model while the row claimed otherwise.
	/// </para>
	/// </remarks>
	Unplaceable,

	/// <summary>
	/// The state is a fold over exactly the events at or below <see cref="ProjectionPosition.Value"/>.
	/// </summary>
	Positioned,

	/// <summary>
	/// The state IS a complete fold over some prefix, but that prefix has no global position number.
	/// </summary>
	/// <remarks>
	/// Produced by a writer that folded events which carry no global position yet, and by rows written
	/// before positions were recorded at all. A positioned writer MAY adopt such a row: the state is
	/// trustworthy, only its coordinate is unknown, so folding the next batch onto it and stamping that
	/// batch's position makes a true assertion.
	/// </remarks>
	Unnumbered,
}

/// <summary>
/// A projection's stored position: positioned at a number, complete but unnumbered, or unplaceable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a type and not a <c>long?</c>.</b> The two no-number states need OPPOSITE treatment
/// from a positioned writer, and a nullable long cannot tell them apart. Every provider stores all three
/// in the one atomic action that writes the state, so the distinction survives a crash: there is no
/// second write to stamp a marker, and therefore no window in which a destroyed position is recorded as
/// a never-established one.
/// </para>
/// <para>
/// <b>The encoding lives here, in one place, on purpose.</b> Eight providers store this value and they
/// must agree on it exactly; a provider that invents its own sentinel is a silent cross-provider
/// divergence that no single-provider test can see. Use <see cref="FromStored"/> and
/// <see cref="ToStored"/> rather than comparing against the sentinels directly.
/// </para>
/// </remarks>
public readonly record struct ProjectionPosition
{
	/// <summary>
	/// The stored value meaning <see cref="ProjectionPositionKind.Unnumbered"/>.
	/// </summary>
	/// <remarks>
	/// This is the value the relational column already defaulted to, so a row written before this type
	/// existed reads back as <see cref="ProjectionPositionKind.Unnumbered"/> and remains adoptable —
	/// which is the correct reading of a row that predates positions, and preserves today's behaviour
	/// for it exactly.
	/// </remarks>
	public const long UnnumberedSentinel = -1L;

	/// <summary>
	/// The stored value meaning <see cref="ProjectionPositionKind.Unplaceable"/>.
	/// </summary>
	public const long UnplaceableSentinel = -2L;

	private readonly long _value;

	private ProjectionPosition(ProjectionPositionKind kind, long value)
	{
		Kind = kind;
		_value = value;
	}

	/// <summary>Gets which of the three assertions this position is making.</summary>
	public ProjectionPositionKind Kind { get; }

	/// <summary>
	/// Gets the position number. Meaningful only when <see cref="Kind"/> is
	/// <see cref="ProjectionPositionKind.Positioned"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The position carries no number. Reading one would fabricate a coordinate for a state that has
	/// none, which is the defect this type exists to make unrepresentable.
	/// </exception>
	public long Value => Kind == ProjectionPositionKind.Positioned
		? _value
		: throw new InvalidOperationException(
			string.Format(
				CultureInfo.CurrentCulture,
				"A projection position of kind '{0}' carries no number. Test Kind before reading Value: "
				+ "an unnumbered state is adoptable and an unplaceable one is not, and neither has a "
				+ "coordinate to read.",
				Kind));

	/// <summary>
	/// A state that is a complete fold over a prefix which has no global position number.
	/// </summary>
	public static ProjectionPosition Unnumbered { get; } =
		new(ProjectionPositionKind.Unnumbered, UnnumberedSentinel);

	/// <summary>
	/// A state that is not a fold over any prefix, so no writer may adopt it.
	/// </summary>
	public static ProjectionPosition Unplaceable { get; } =
		new(ProjectionPositionKind.Unplaceable, UnplaceableSentinel);

	/// <summary>Gets a value indicating whether a positioned writer may adopt a row holding this.</summary>
	/// <remarks>
	/// True for <see cref="ProjectionPositionKind.Unnumbered"/> only. A positioned row is advanced from
	/// rather than adopted, and an unplaceable one is refused.
	/// </remarks>
	public bool IsAdoptable => Kind == ProjectionPositionKind.Unnumbered;

	/// <summary>
	/// Gets the value to pass as a positioned write's <c>expectedPosition</c>, or <see langword="null"/>
	/// when this position carries no number.
	/// </summary>
	/// <remarks>
	/// <b>Both no-number states map to <see langword="null"/> here, and that is not the conflation this
	/// type exists to remove.</b> The caller cannot safely act on the difference anyway: between its read
	/// and its write another writer may change the row, so a caller-side refusal would be deciding on a
	/// stale observation. The store holds the authoritative value and discriminates at the instant it
	/// writes -- adopting an unnumbered row and refusing an unplaceable one with
	/// <see cref="ProjectionAdvanceOutcome.Unplaceable"/>. Enforcement belongs where it can be atomic.
	/// </remarks>
	public long? ExpectedPositionOrNull =>
		Kind == ProjectionPositionKind.Positioned ? _value : null;

	/// <summary>A state folded over exactly the events at or below <paramref name="position"/>.</summary>
	/// <param name="position">The global position of the last event folded in. Must not be negative.</param>
	/// <returns>The positioned value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="position"/> is negative. Negative values are reserved for the two no-number
	/// states, so accepting one here would let a caller forge them.
	/// </exception>
	public static ProjectionPosition At(long position)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(position);
		return new ProjectionPosition(ProjectionPositionKind.Positioned, position);
	}

	/// <summary>Reads a stored value back into one of the three states.</summary>
	/// <param name="stored">
	/// The value a provider read back, or <see langword="null"/> where the provider has no row or the
	/// field is absent from the document.
	/// </param>
	/// <returns>The position that value denotes.</returns>
	/// <remarks>
	/// A <see langword="null"/> or absent field reads as <see cref="Unnumbered"/> rather than
	/// <see cref="Unplaceable"/>, and the direction is deliberate: absence is what a row written before
	/// this type existed looks like, and such a row IS a complete fold. Reading it as unplaceable would
	/// refuse adoption on exactly the rows where adoption is correct.
	/// </remarks>
	public static ProjectionPosition FromStored(long? stored) => stored switch
	{
		null => Unnumbered,
		UnplaceableSentinel => Unplaceable,
		< 0 => Unnumbered,
		_ => At(stored.Value),
	};

	/// <summary>Gets the value a provider writes to storage for this position.</summary>
	/// <returns>The stored encoding.</returns>
	/// <remarks>
	/// Derived from <see cref="Kind"/> rather than returned from the backing field, and that is
	/// load-bearing for the default value: <c>default(ProjectionPosition)</c> has a backing field of
	/// zero, which returned directly would be written as the perfectly valid position 0 and read back
	/// as a measured coordinate. Going through the kind means the default round-trips as the
	/// unplaceable sentinel instead -- refused rather than believed.
	/// </remarks>
	public long ToStored() => Kind switch
	{
		ProjectionPositionKind.Positioned => _value,
		ProjectionPositionKind.Unnumbered => UnnumberedSentinel,
		_ => UnplaceableSentinel,
	};

	/// <summary>Returns a diagnostic string.</summary>
	/// <returns>The position, rendered for a log or an assertion message.</returns>
	public override string ToString() => Kind == ProjectionPositionKind.Positioned
		? _value.ToString(CultureInfo.InvariantCulture)
		: Kind.ToString();
}
