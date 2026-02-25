// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class CdcPositionShould
{
	[Fact]
	public void ThrowWhenLsnIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new CdcPosition(null!, null));
	}

	[Fact]
	public void ExposeProperties()
	{
		byte[] lsn = [0x01, 0x02, 0x03];
		byte[] seqVal = [0x04, 0x05];
		var sut = new CdcPosition(lsn, seqVal);
		sut.Lsn.ShouldBe(lsn);
		sut.SequenceValue.ShouldBe(seqVal);
	}

	[Fact]
	public void BeValidWhenLsnIsNonEmpty()
	{
		new CdcPosition([0x01], null).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void BeInvalidWhenLsnIsEmpty()
	{
		new CdcPosition([], null).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ConvertToChangePositionWithLsnOnly()
	{
		var sut = new CdcPosition([0xAA, 0xBB], null);
		var position = sut.ToChangePosition();
		position.IsValid.ShouldBeTrue();
		position.ToToken().ShouldBe("AABB");
	}

	[Fact]
	public void ConvertToChangePositionWithSeqVal()
	{
		var sut = new CdcPosition([0xAA, 0xBB], [0xCC]);
		sut.ToChangePosition().ToToken().ShouldBe("AABB|CC");
	}

	[Fact]
	public void ReturnEmptyChangePositionWhenInvalid()
	{
		new CdcPosition([], null).ToChangePosition().IsValid.ShouldBeFalse();
	}

	[Fact]
	public void RoundTripFromChangePosition()
	{
		byte[] lsn = [0x01, 0x02, 0x03];
		byte[] seq = [0x04, 0x05];
		var original = new CdcPosition(lsn, seq);
		var restored = CdcPosition.FromChangePosition(original.ToChangePosition());
		restored.Lsn.ShouldBe(lsn);
		restored.SequenceValue.ShouldBe(seq);
	}

	[Fact]
	public void RoundTripWithoutSeqVal()
	{
		byte[] lsn = [0x01, 0x02, 0x03];
		var restored = CdcPosition.FromChangePosition(new CdcPosition(lsn, null).ToChangePosition());
		restored.Lsn.ShouldBe(lsn);
		restored.SequenceValue.ShouldBeNull();
	}

	[Fact]
	public void ReturnInvalidPositionFromNullChangePosition()
	{
		CdcPosition.FromChangePosition(null).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ImplementEquality()
	{
		var a = new CdcPosition([0x01, 0x02], [0x03]);
		var b = new CdcPosition([0x01, 0x02], [0x03]);
		a.Equals(b).ShouldBeTrue();
		a.Equals(new CdcPosition([0x01, 0x02], [0x04])).ShouldBeFalse();
	}

	[Fact]
	public void ImplementEqualityWithNullSeqVal()
	{
		var a = new CdcPosition([0x01], null);
		a.Equals(new CdcPosition([0x01], null)).ShouldBeTrue();
		a.Equals(new CdcPosition([0x01], [0x02])).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenComparingWithNull()
	{
		new CdcPosition([0x01], null).Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueForSameReference()
	{
		var sut = new CdcPosition([0x01], null);
		sut.Equals(sut).ShouldBeTrue();
	}

	[Fact]
	public void GenerateConsistentHashCodes()
	{
		var a = new CdcPosition([0x01, 0x02], [0x03]);
		var b = new CdcPosition([0x01, 0x02], [0x03]);
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void ProduceReadableToString()
	{
		var str = new CdcPosition([0x0A, 0x0B], [0x0C]).ToString();
		str.ShouldContain("0A0B");
		str.ShouldContain("SeqVal");
	}

	[Fact]
	public void ProduceEmptyLsnToString()
	{
		new CdcPosition([], null).ToString().ShouldContain("(empty)");
	}

	[Fact]
	public void SupportObjectEquals()
	{
		var a = new CdcPosition([0x01], null);
		object b = new CdcPosition([0x01], null);
		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForNonCdcPositionObject()
	{
		new CdcPosition([0x01], null).Equals("not a position").ShouldBeFalse();
	}
}
