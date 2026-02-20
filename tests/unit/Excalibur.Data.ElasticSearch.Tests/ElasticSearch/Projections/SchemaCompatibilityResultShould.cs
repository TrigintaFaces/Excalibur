// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaCompatibilityResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new SchemaCompatibilityResult
		{
			IsCompatible = true,
			Level = CompatibilityLevel.Full,
		};

		sut.IsCompatible.ShouldBeTrue();
		sut.Level.ShouldBe(CompatibilityLevel.Full);
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new SchemaCompatibilityResult
		{
			IsCompatible = true,
			Level = CompatibilityLevel.Full,
		};

		sut.Incompatibilities.ShouldBeNull();
		sut.SuggestedFixes.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var incompatibilities = new List<string> { "Field 'price' changed from float to long" };
		var fixes = new List<string> { "Add script to convert price values" };

		var sut = new SchemaCompatibilityResult
		{
			IsCompatible = false,
			Level = CompatibilityLevel.None,
			Incompatibilities = incompatibilities,
			SuggestedFixes = fixes,
		};

		sut.Incompatibilities.ShouldBeSameAs(incompatibilities);
		sut.SuggestedFixes.ShouldBeSameAs(fixes);
	}
}
