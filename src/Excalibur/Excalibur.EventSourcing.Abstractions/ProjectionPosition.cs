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
/// The distinction between the second and third members is about what the row is ASSERTING, not about
/// what the next write does: a positioned write refuses both, because neither carries a number it can
/// advance from. One says "a complete fold, coordinate unknown"; the other says "a fold over no known
/// prefix at all", and only the first can be READ as an answer while the row waits to be rebuilt.
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
	/// any prefix -- and it is also the conservative one, because no positioned write can advance from it.
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
	/// <para>
	/// Produced by a writer that folded events which carry no global position yet, and by rows written
	/// before positions were recorded at all. The state is trustworthy; only its coordinate is unknown.
	/// </para>
	/// <para>
	/// <b>A positioned writer does NOT adopt such a row.</b> There is no number to advance from, so a
	/// writer stamping its own batch's position onto it would be guessing which prefix the stored state
	/// covers -- and the guess is wrong whenever anything folded into that state lies above the batch. The
	/// write is refused with <see cref="ProjectionAdvanceOutcome.Unplaceable"/> -- the same outcome an
	/// unplaceable row gets, because that outcome reports only that there was no number to advance from --
	/// and the row is repaired by
	/// <see cref="IPositionedProjectionStore{TProjection}.RebuildAtPositionAsync"/>.
	/// </para>
	/// <para>
	/// <b>THIS is where the difference from <see cref="Unplaceable"/> lives, and it belongs here rather
	/// than on the write's outcome.</b> A row of this kind holds a state that can be READ as a complete
	/// answer while it waits for its rebuild; an unplaceable one cannot. That is a fact about the ROW,
	/// measured in the same observation as the value, so it is honest here -- and would not be honest on a
	/// write result, which carries no state and is classified against a read taken at a different instant
	/// from the refusal.
	/// </para>
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
/// <para>
/// <b>THE INVARIANT THAT MAKES THIS DESIGN DEFENSIBLE, and the one to read before changing any read
/// path: <see cref="ProjectionPositionKind.Unnumbered"/> is reachable ONLY when a caller explicitly
/// asserted it</b>, by writing the sentinel through
/// <see cref="IPositionedProjectionStore{TProjection}.UpsertUnnumberedAsync"/>. Not by a row that does
/// not exist, not by an absent field, not by a stray negative, not by a value that failed to decode --
/// every one of those reads as <see cref="ProjectionPositionKind.Unplaceable"/>. The trustworthy state
/// requires an assertion from someone who knew; everything unknown reads fail-safe. Three kinds of site
/// hold that up -- this decoder, every provider's absent-row return, and every provider's decode-failure
/// arm -- and undoing any one of them loses the invariant for all of them.
/// </para>
/// </remarks>
public readonly record struct ProjectionPosition
{
	/// <summary>
	/// The stored value meaning <see cref="ProjectionPositionKind.Unnumbered"/>.
	/// </summary>
	/// <remarks>
	/// This is the value the relational column already defaulted to, so a row written before this type
	/// existed reads back as <see cref="ProjectionPositionKind.Unnumbered"/> -- the correct reading of a
	/// row that predates positions, because its state IS a complete fold. A positioned write still refuses
	/// it: complete is not the same as placeable, and the row has to be rebuilt before it can be advanced.
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
				+ "neither of the two no-number states has a coordinate to read, and they differ in what the "
				+ "row is asserting rather than in how a positioned write treats them.",
				Kind));

	/// <summary>
	/// A state that is a complete fold over a prefix which has no global position number.
	/// </summary>
	public static ProjectionPosition Unnumbered { get; } =
		new(ProjectionPositionKind.Unnumbered, UnnumberedSentinel);

	/// <summary>
	/// A state that is not a fold over any prefix, so no positioned write can advance from it.
	/// </summary>
	/// <remarks>
	/// A positioned write refuses <see cref="Unnumbered"/> too -- neither carries a number it can match --
	/// so the refusal is not what separates them, and neither is the repair, which is the same rebuild for
	/// both. What separates them is what the row is ASSERTING: here, that the stored state is not a fold
	/// over any prefix and therefore cannot be read as an answer to anything.
	/// </remarks>
	public static ProjectionPosition Unplaceable { get; } =
		new(ProjectionPositionKind.Unplaceable, UnplaceableSentinel);

	/// <summary>
	/// Gets the value to pass as a positioned write's <c>expectedPosition</c>, or <see langword="null"/>
	/// when this position carries no number.
	/// </summary>
	/// <remarks>
	/// <b>Both no-number states map to <see langword="null"/> here, and that is not the conflation this
	/// type exists to remove.</b> Neither carries a number, and <see langword="null"/> is exactly what a
	/// caller must pass to say "I read no position" -- a true statement in both. The caller could not act
	/// on the difference at this point anyway: between its read and its write another writer may change the
	/// row, so a caller-side decision would rest on a stale observation. The store decides admissibility at
	/// the instant it writes, and refuses both alike with
	/// <see cref="ProjectionAdvanceOutcome.Unplaceable"/>.
	/// <para>
	/// <b>The distinction does NOT travel on the write's outcome, and that is deliberate.</b> A write
	/// result carries no state, and a store that must read the row to classify its refusal reads it at a
	/// different instant from the refusal -- so an outcome naming which no-number state the row was in
	/// could already be false when the caller acted on it. The distinction lives on the READ, where
	/// <see cref="Kind"/> is measured together with the value in one observation.
	/// </para>
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
	/// The value a provider read back, or <see langword="null"/> where the provider has no row, the field is
	/// absent from the document, or the stored value could not be decoded as a number.
	/// </param>
	/// <returns>The position that value denotes.</returns>
	/// <remarks>
	/// <para>
	/// <b>Anything that is not a legible non-negative number, and not the explicit unnumbered sentinel,
	/// reads as <see cref="Unplaceable"/>.</b> Absence, a stray negative, and a value that failed to decode
	/// are three ways of saying one thing -- nobody knows which prefix the stored state covers -- and
	/// not-knowing reads conservatively, because a positioned write refuses an unplaceable row rather than
	/// stamping a position over it.
	/// </para>
	/// <para>
	/// <b>The one exception is an ASSERTION rather than an absence.</b>
	/// <see cref="UnnumberedSentinel"/> is what
	/// <see cref="IPositionedProjectionStore{TProjection}.UpsertUnnumberedAsync"/> writes, so a caller that
	/// stated "this state IS a complete fold whose prefix has no number" gets that statement back. Reading
	/// it as <see cref="Unplaceable"/> would not be conservative, it would be a DIFFERENT false claim --
	/// and it would make the <see cref="ProjectionPositionKind.Unnumbered"/> kind unreachable through
	/// storage, destroying on read the only assertion that can produce it.
	/// </para>
	/// <para>
	/// <b>This is the reading the type already gives its own default, and for the same reason.</b> A field
	/// nobody wrote is the same epistemic situation as a field nobody set. Reading one fail-safe and the
	/// other trustworthy would be an inconsistency inside a single type, and the trustworthy direction is
	/// the fabrication: an absent field is absence of evidence, not evidence of a complete fold.
	/// </para>
	/// <para>
	/// <b>Superseded reading, quoted so an inheritor recognises it.</b> Absence used to read as
	/// <see cref="Unnumbered"/>, on the grounds that "absence is what a row written before this type
	/// existed looks like, and such a row IS a complete fold". That justification rested entirely on data
	/// written by earlier versions, and it is withdrawn -- which leaves the mapping nothing to stand on.
	/// </para>
	/// </remarks>
	public static ProjectionPosition FromStored(long? stored) => stored switch
	{
		// AN EXPLICIT ASSERTION, AND THE ONLY WAY THE TRUSTWORTHY STATE IS REACHABLE AT ALL. This is the
		// value UpsertUnnumberedAsync writes, so a caller that stated "this state IS a complete fold whose
		// prefix has no number" gets that statement back. Collapse this arm into the one below and the
		// Unnumbered kind becomes unreachable through storage -- the assertion would be destroyed by the
		// read that is supposed to report it. MUST stay above the negative arm; the sentinel is negative.
		UnnumberedSentinel => Unnumbered,

		// EVERYTHING ELSE THAT IS NOT A LEGIBLE NON-NEGATIVE NUMBER. Absence, the unplaceable sentinel,
		// and any other negative alike: nobody asserted anything, so nobody knows, so it reads fail-safe.
		null or < 0 => Unplaceable,

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
