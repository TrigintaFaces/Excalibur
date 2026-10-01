// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// The three-state position, and specifically the states it must never silently become.
/// </summary>
/// <remarks>
/// The whole point of this type is that "no position" and "a position" cannot be confused, and that the
/// two no-position states cannot be confused with each other. Each arm below names an input that would
/// make it red.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionPositionShould
{
	// SAFETY, and it is the arm that caught a real defect in the first cut of this type. A struct can be
	// default-constructed anywhere -- an array element, an uninitialised field, an explicit `default` --
	// so the question is not whether it happens but what it MEANS. With Positioned as the zero enum
	// member the default asserted "folded over everything up to position 0": definite, plausible, wrong,
	// and indistinguishable from a measured value. RED if ProjectionPositionKind is renumbered so that
	// Positioned sits at zero again.
	[Fact]
	public void NeverReportADefaultConstructedValueAsAMeasuredPosition()
	{
		var uninitialised = default(ProjectionPosition);

		uninitialised.Kind.ShouldBe(
			ProjectionPositionKind.Unplaceable,
			"a value nobody set must not claim to be a fold over any prefix, and the conservative "
			+ "reading is the one no positioned write can advance from");

		uninitialised.ExpectedPositionOrNull.ShouldBeNull(
			"a value nobody set must not hand a coordinate to a conditional write; doing so would fold "
			+ "the next batch onto unknown state and then stamp a position the state does not justify");

		_ = Should.Throw<InvalidOperationException>(
			() => uninitialised.Value,
			"reading a coordinate off a value that has none is the fabrication this type exists to "
			+ "make unrepresentable");
	}

	// SAFETY. ToStored reads the KIND, not the backing field. A default-constructed value has a backing
	// field of zero, and returning that directly would write the perfectly valid position 0 -- which
	// reads back as a measured coordinate. RED if ToStored is changed back to `=> _value`.
	[Fact]
	public void RoundTripADefaultConstructedValueAsUnplaceableRatherThanAsPositionZero()
	{
		var stored = default(ProjectionPosition).ToStored();

		stored.ShouldBe(
			ProjectionPosition.UnplaceableSentinel,
			"the default must persist as something a later read refuses, never as position 0");

		ProjectionPosition.FromStored(stored).Kind.ShouldBe(ProjectionPositionKind.Unplaceable);
	}

	// SAFETY. The two no-number states need OPPOSITE treatment, so a decoder that collapsed them would
	// reintroduce the whole defect. RED if FromStored maps the unplaceable sentinel to Unnumbered.
	[Fact]
	public void KeepTheTwoNoNumberStatesDistinctThroughStorage()
	{
		// Asserted on Kind, which is the three-way discriminator itself. A two-valued predicate over it
		// could be satisfied by a decoder that returned the wrong one of the two no-number members.
		ProjectionPosition.FromStored(ProjectionPosition.UnnumberedSentinel)
			.Kind.ShouldBe(
				ProjectionPositionKind.Unnumbered,
				"the unnumbered sentinel means the state IS a complete fold, only its prefix has no number");

		ProjectionPosition.FromStored(ProjectionPosition.UnplaceableSentinel)
			.Kind.ShouldBe(
				ProjectionPositionKind.Unplaceable,
				"the unplaceable sentinel means the state is not a fold over any prefix, which is the "
				+ "strictly stronger and opposite claim");

		ProjectionPosition.UnnumberedSentinel.ShouldNotBe(ProjectionPosition.UnplaceableSentinel);
	}

	// LIVENESS, and it is what stops every arm above being satisfied by refusing everything. A real
	// position must survive storage as itself, including zero, which is a legitimate position.
	[Theory]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(9_007_199_254_740_993L)]
	public void CarryARealPositionThroughStorageUnchanged(long position)
	{
		var round = ProjectionPosition.FromStored(ProjectionPosition.At(position).ToStored());

		round.Kind.ShouldBe(ProjectionPositionKind.Positioned);
		round.Value.ShouldBe(position, "position zero is a legitimate coordinate, not an absence");
		round.ExpectedPositionOrNull.ShouldBe(position);

		// A real position must never encode into the negative sentinel space. If it did, the row would
		// read back as one of the two no-number states and the coordinate would be lost silently.
		ProjectionPosition.At(position).ToStored().ShouldBeGreaterThanOrEqualTo(
			0L,
			"the negatives are reserved for the states that carry no number, so a measured position that "
			+ "encoded into them would read back as an absence");
	}

	// INVERTED, deliberately. This arm used to be named
	// TreatAnAbsentStoredValueAsACompleteFoldRatherThanAsUnplaceable and asserted the opposite: that an
	// absent value reads as UNNUMBERED, on the grounds that absence is what a row written before this type
	// existed looks like, and such a row IS a complete fold.
	//
	// That justification rested entirely on data written by earlier versions, and it is withdrawn -- which
	// leaves nothing holding the mapping up. Absence is absence of EVIDENCE, not evidence of a fold, and
	// the conservative reading is the one the type already gives its own default. Reading a field nobody
	// wrote as trustworthy while reading a field nobody SET as fail-safe was an inconsistency inside one
	// type, and the trustworthy direction was the fabrication.
	//
	// What this buys: UNNUMBERED becomes reachable only when a caller explicitly asserted it through
	// UpsertUnnumberedAsync. A pre-existing document with no position field can no longer be mistaken for
	// a complete fold, which is the exposure that needed no consumer action at all.
	[Fact]
	public void TreatAnAbsentStoredValueAsUnplaceableRatherThanAsACompleteFold()
	{
		ProjectionPosition.FromStored(null).Kind.ShouldBe(ProjectionPositionKind.Unplaceable);

		// Value equality with the canonical instance, which is stronger than the Kind check alone: it
		// pins the backing encoding too, so a decoder that produced the right kind carrying a stray
		// number is still red.
		ProjectionPosition.FromStored(null).ShouldBe(ProjectionPosition.Unplaceable);

		// A STRAY NEGATIVE -- one that is NEITHER sentinel -- reads the same way, and this is the arm that
		// pins it: nobody can place such a value, so it is not evidence of a fold either. It used to read
		// as Unnumbered.
		ProjectionPosition.FromStored(-97).ShouldBe(ProjectionPosition.Unplaceable);
		ProjectionPosition.FromStored(long.MinValue).ShouldBe(ProjectionPosition.Unplaceable);

		// AND THE ONE EXCEPTION, pinned here so a future "simplification" to `null or < 0` cannot pass.
		// The unnumbered sentinel is an ASSERTION -- it is what UpsertUnnumberedAsync writes -- so reading
		// it conservatively would not be caution, it would destroy on read the only statement that can
		// produce the Unnumbered kind, making that kind unreachable through storage.
		ProjectionPosition.FromStored(ProjectionPosition.UnnumberedSentinel).ShouldBe(
			ProjectionPosition.Unnumbered,
			"an explicit assertion must survive the round trip, or the kind is dead");
	}

	// A negative position cannot be constructed, because the negatives are the sentinel space. Accepting
	// one would let a caller forge a no-number state that later reads as a measured coordinate.
	[Fact]
	public void RefuseToConstructAPositionFromTheSentinelSpace()
	{
		_ = Should.Throw<ArgumentOutOfRangeException>(() => ProjectionPosition.At(-1));
		_ = Should.Throw<ArgumentOutOfRangeException>(() => ProjectionPosition.At(-2));
	}

	// Neither no-number state carries a coordinate, so neither may hand one to a conditional write.
	[Fact]
	public void OfferNoExpectedPositionForEitherNoNumberState()
	{
		ProjectionPosition.Unnumbered.ExpectedPositionOrNull.ShouldBeNull();
		ProjectionPosition.Unplaceable.ExpectedPositionOrNull.ShouldBeNull();
	}
}
