// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

using NpgsqlTypes;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresCdcPositionShould
{
	[Fact]
	public void HaveInvalidStartPosition()
	{
		PostgresCdcPosition.Start.IsValid.ShouldBeFalse();
		PostgresCdcPosition.Start.LsnValue.ShouldBe(0UL);
	}

	[Fact]
	public void CreateFromLsn()
	{
		var lsn = new NpgsqlLogSequenceNumber(12345UL);
		var position = new PostgresCdcPosition(lsn);

		position.IsValid.ShouldBeTrue();
		position.Lsn.ShouldBe(lsn);
		position.LsnValue.ShouldBe(12345UL);
	}

	[Fact]
	public void CreateFromUlong()
	{
		var position = new PostgresCdcPosition(67890UL);

		position.IsValid.ShouldBeTrue();
		position.LsnValue.ShouldBe(67890UL);
	}

	[Fact]
	public void CreateFromValidString()
	{
		var position = new PostgresCdcPosition("0/1234ABCD");

		position.IsValid.ShouldBeTrue();
		position.LsnString.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateInvalidFromEmptyString()
	{
		var position = new PostgresCdcPosition("");

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void CreateInvalidFromNullString()
	{
		var position = new PostgresCdcPosition((string)null!);

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ParseValidLsnString()
	{
		var position = PostgresCdcPosition.Parse("0/1234ABCD");

		position.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void TryParseValidLsnString()
	{
		PostgresCdcPosition.TryParse("0/1234ABCD", out var result).ShouldBeTrue();
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void TryParseNullReturnsFalse()
	{
		PostgresCdcPosition.TryParse(null, out var result).ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void TryParseEmptyReturnsFalse()
	{
		PostgresCdcPosition.TryParse("", out var result).ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void TryParseInvalidReturnsFalse()
	{
		PostgresCdcPosition.TryParse("not-an-lsn", out var result).ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void SupportEquality()
	{
		var a = new PostgresCdcPosition(100UL);
		var b = new PostgresCdcPosition(100UL);

		(a == b).ShouldBeTrue();
		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void SupportInequality()
	{
		var a = new PostgresCdcPosition(100UL);
		var b = new PostgresCdcPosition(200UL);

		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void SupportComparison()
	{
		var a = new PostgresCdcPosition(100UL);
		var b = new PostgresCdcPosition(200UL);
		var c = new PostgresCdcPosition(100UL);

		(a < b).ShouldBeTrue();
		(b > a).ShouldBeTrue();
		(a <= b).ShouldBeTrue();
		(b >= a).ShouldBeTrue();
		(a <= c).ShouldBeTrue();
		(a >= c).ShouldBeTrue();
	}

	[Fact]
	public void HaveConsistentHashCode()
	{
		var a = new PostgresCdcPosition(100UL);
		var b = new PostgresCdcPosition(100UL);

		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void NotEqualDifferentType()
	{
		var position = new PostgresCdcPosition(100UL);

		position.Equals("not-a-position").ShouldBeFalse();
	}

	[Fact]
	public void ConvertToChangePosition()
	{
		var position = new PostgresCdcPosition(12345UL);

		var changePosition = position.ToChangePosition();

		changePosition.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ConvertStartToEmptyChangePosition()
	{
		var changePosition = PostgresCdcPosition.Start.ToChangePosition();

		changePosition.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ConvertFromNullChangePosition()
	{
		var position = PostgresCdcPosition.FromChangePosition(null);

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ReturnLsnStringFromToString()
	{
		var position = new PostgresCdcPosition(100UL);

		position.ToString().ShouldBe(position.LsnString);
	}

	[Fact]
	public void CompareToReturnsZeroForEqual()
	{
		var a = new PostgresCdcPosition(100UL);
		var b = new PostgresCdcPosition(100UL);

		a.CompareTo(b).ShouldBe(0);
	}
}
