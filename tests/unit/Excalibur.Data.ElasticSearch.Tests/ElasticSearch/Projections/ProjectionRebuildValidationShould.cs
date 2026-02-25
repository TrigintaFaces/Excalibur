// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionRebuildValidationShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var messages = new List<string> { "Index does not exist" };
		var sut = new ProjectionRebuildValidation
		{
			CanRebuild = false,
			ValidationMessages = messages,
		};

		sut.CanRebuild.ShouldBeFalse();
		sut.ValidationMessages.ShouldBeSameAs(messages);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new ProjectionRebuildValidation
		{
			CanRebuild = true,
			ValidationMessages = [],
		};

		sut.Warnings.ShouldBeNull();
		sut.EstimatedDocumentCount.ShouldBeNull();
		sut.EstimatedDuration.ShouldBeNull();
		sut.HasSufficientResources.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var warnings = new List<string> { "Large dataset, may take a while" };
		var sut = new ProjectionRebuildValidation
		{
			CanRebuild = true,
			ValidationMessages = [],
			Warnings = warnings,
			EstimatedDocumentCount = 1_000_000,
			EstimatedDuration = TimeSpan.FromHours(2),
			HasSufficientResources = false,
		};

		sut.Warnings.ShouldBeSameAs(warnings);
		sut.EstimatedDocumentCount.ShouldBe(1_000_000);
		sut.EstimatedDuration.ShouldBe(TimeSpan.FromHours(2));
		sut.HasSufficientResources.ShouldBeFalse();
	}
}
