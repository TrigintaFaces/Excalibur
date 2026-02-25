// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class TimeoutOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		var sut = new TimeoutOptions();

		sut.SearchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		sut.IndexTimeout.ShouldBe(TimeSpan.FromSeconds(60));
		sut.BulkTimeout.ShouldBe(TimeSpan.FromSeconds(120));
		sut.DeleteTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new TimeoutOptions
		{
			SearchTimeout = TimeSpan.FromMinutes(1),
			IndexTimeout = TimeSpan.FromMinutes(2),
			BulkTimeout = TimeSpan.FromMinutes(5),
			DeleteTimeout = TimeSpan.FromMinutes(1),
		};

		sut.SearchTimeout.ShouldBe(TimeSpan.FromMinutes(1));
		sut.IndexTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		sut.BulkTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		sut.DeleteTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
