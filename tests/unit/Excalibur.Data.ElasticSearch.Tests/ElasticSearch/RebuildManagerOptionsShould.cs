// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class RebuildManagerOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new RebuildManagerOptions();

		sut.Enabled.ShouldBeTrue();
		sut.DefaultBatchSize.ShouldBe(1000);
		sut.MaxDegreeOfParallelism.ShouldBe(4);
		sut.UseAliasing.ShouldBeTrue();
		sut.OperationTimeout.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new RebuildManagerOptions
		{
			Enabled = false,
			DefaultBatchSize = 500,
			MaxDegreeOfParallelism = 8,
			UseAliasing = false,
			OperationTimeout = TimeSpan.FromHours(48),
		};

		sut.Enabled.ShouldBeFalse();
		sut.DefaultBatchSize.ShouldBe(500);
		sut.MaxDegreeOfParallelism.ShouldBe(8);
		sut.UseAliasing.ShouldBeFalse();
		sut.OperationTimeout.ShouldBe(TimeSpan.FromHours(48));
	}
}
