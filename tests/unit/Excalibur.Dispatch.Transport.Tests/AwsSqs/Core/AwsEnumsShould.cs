// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;


namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsEnumsShould
{
	[Fact]
	public void HaveAllMetricUnitMembers()
	{
		Enum.GetValues<MetricUnit>().Length.ShouldBe(27);
	}

	[Theory]
	[InlineData(MetricUnit.None, 0)]
	[InlineData(MetricUnit.Count, 1)]
	[InlineData(MetricUnit.Bytes, 2)]
	[InlineData(MetricUnit.Percent, 12)]
	[InlineData(MetricUnit.Seconds, 13)]
	[InlineData(MetricUnit.Milliseconds, 15)]
	[InlineData(MetricUnit.CountPerSecond, 26)]
	public void HaveCorrectMetricUnitValues(MetricUnit unit, int expected)
	{
		((int)unit).ShouldBe(expected);
	}



}
