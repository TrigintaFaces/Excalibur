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
			+ "reading is the one that refuses adoption");

		uninitialised.IsAdoptable.ShouldBeFalse(
			"adopting a value nobody set would fold the next batch onto unknown state and then stamp a "
			+ "position the state does not justify");

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
		ProjectionPosition.FromStored(ProjectionPosition.UnnumberedSentinel)
			.IsAdoptable.ShouldBeTrue("an unnumbered row holds a complete fold and may be adopted");

		ProjectionPosition.FromStored(ProjectionPosition.UnplaceableSentinel)
			.IsAdoptable.ShouldBeFalse("an unplaceable row is not a fold over any prefix");

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
		round.IsAdoptable.ShouldBeFalse("a positioned row is advanced from, never adopted");
	}

	// An absent stored value is a row written before positions existed. It holds a complete fold, so it
	// is adoptable -- reading it as unplaceable would refuse adoption on exactly the rows where adoption
	// is correct, which is the opposite defect.
	[Fact]
	public void TreatAnAbsentStoredValueAsACompleteFoldRatherThanAsUnplaceable()
	{
		ProjectionPosition.FromStored(null).Kind.ShouldBe(ProjectionPositionKind.Unnumbered);
		ProjectionPosition.FromStored(null).IsAdoptable.ShouldBeTrue();
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
