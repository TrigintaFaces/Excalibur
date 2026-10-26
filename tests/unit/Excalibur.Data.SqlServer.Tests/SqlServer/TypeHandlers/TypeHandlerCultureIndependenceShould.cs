// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;

using Excalibur.Data.SqlServer.TypeHandlers;

namespace Excalibur.Data.Tests.SqlServer.TypeHandlers;

/// <summary>
/// A value read from a database has no culture, so the same stored string must produce the same value
/// on every host that reads it.
/// </summary>
/// <remarks>
/// <para>
/// These handlers defaulted to <see cref="CultureInfo.CurrentCulture"/>, which makes the host's locale
/// part of the conversion: <c>"03/04/2026"</c> is 4 March under <c>en-US</c> and 3 April under
/// <c>en-GB</c>. Same row, same binary, different date, nothing thrown — the reading is simply wrong,
/// and wrong differently depending on which server read it. <c>Money.Parse</c> in this solution already
/// defaults to <see cref="CultureInfo.InvariantCulture"/>; these four were the outliers.
/// </para>
/// <para>
/// The arms drive the DEFAULT constructor, because the default is the defect. A handler constructed
/// with an explicit provider was always under the caller's control and is not what shipped.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Data")]
public sealed class TypeHandlerCultureIndependenceShould
{
	/// <summary>Runs an action with the thread culture pinned, then restores it.</summary>
	private static void UnderCulture(string name, Action body)
	{
		var prior = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo(name);
			body();
		}
		finally
		{
			CultureInfo.CurrentCulture = prior;
		}
	}

	/// <summary>
	/// An ambiguous day/month string resolves identically everywhere. This is the discriminating case:
	/// an ISO string parses under most cultures anyway and would prove nothing.
	/// </summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("en-GB")]
	[InlineData("de-DE")]
	[InlineData("ja-JP")]
	public void ReadAnAmbiguousDateTheSameUnderAnyHostCulture(string culture) =>
		UnderCulture(culture, () =>
			new DateOnlyTypeHandler().Parse("03/04/2026").ShouldBe(new DateOnly(2026, 3, 4)));

	/// <summary>The nullable handler owes the same guarantee.</summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("en-GB")]
	[InlineData("de-DE")]
	[InlineData("ja-JP")]
	public void ReadAnAmbiguousNullableDateTheSameUnderAnyHostCulture(string culture) =>
		UnderCulture(culture, () =>
			new NullableDateOnlyTypeHandler().Parse("03/04/2026").ShouldBe(new DateOnly(2026, 3, 4)));

	/// <summary>
	/// A 12-hour time with an English designator is unparseable under a culture that has none, so a
	/// culture-sensitive default turns a readable row into a thrown exception rather than a wrong value.
	/// </summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("de-DE")]
	[InlineData("ja-JP")]
	public void ReadATwelveHourTimeTheSameUnderAnyHostCulture(string culture) =>
		UnderCulture(culture, () =>
			new TimeOnlyTypeHandler().Parse("1:45 PM").ShouldBe(new TimeOnly(13, 45)));

	/// <summary>The nullable handler owes the same guarantee.</summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("de-DE")]
	[InlineData("ja-JP")]
	public void ReadATwelveHourNullableTimeTheSameUnderAnyHostCulture(string culture) =>
		UnderCulture(culture, () =>
			new NullableTimeOnlyTypeHandler().Parse("1:45 PM").ShouldBe(new TimeOnly(13, 45)));

	/// <summary>
	/// LIVENESS. The ISO form every provider actually emits must keep working, under every culture.
	/// </summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("de-DE")]
	[InlineData("ja-JP")]
	public void StillReadAnIsoDate(string culture) =>
		UnderCulture(culture, () =>
			new DateOnlyTypeHandler().Parse("2026-02-11").ShouldBe(new DateOnly(2026, 2, 11)));

	/// <summary>
	/// LIVENESS. A stored string carrying a time component is still accepted — some columns are a
	/// varchar holding a full timestamp, and tightening to a date-only grammar would break them.
	/// </summary>
	[Theory]
	[InlineData("en-US")]
	[InlineData("de-DE")]
	public void StillReadADateThatCarriesATimeComponent(string culture) =>
		UnderCulture(culture, () =>
			new DateOnlyTypeHandler().Parse("2026-02-11 13:45:00").ShouldBe(new DateOnly(2026, 2, 11)));

	/// <summary>
	/// An explicit provider still wins, so a consumer with a genuinely local legacy format keeps a way
	/// out. Without this arm the fix could have hard-coded invariant and removed the escape hatch.
	/// </summary>
	[Fact]
	public void HonourAnExplicitlySuppliedFormatProvider() =>
		new DateOnlyTypeHandler(new CultureInfo("en-GB")).Parse("03/04/2026")
			.ShouldBe(new DateOnly(2026, 4, 3));

	/// <summary>A value in no recognisable form is refused rather than guessed at.</summary>
	[Fact]
	public void RefuseAnUnparseableDate() =>
		Should.Throw<InvalidOperationException>(() => new DateOnlyTypeHandler().Parse("not-a-date"));
}
