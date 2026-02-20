// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class SchemaComparisonResultShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var added = new List<FieldChange>
		{
			new() { FieldName = "Email", FieldPath = "customer.email", ChangeType = FieldChangeType.Added },
		};
		var removed = new List<FieldChange>();
		var modified = new List<FieldChange>();

		var sut = new SchemaComparisonResult
		{
			AreIdentical = false,
			IsBackwardsCompatible = true,
			AddedFields = added,
			RemovedFields = removed,
			ModifiedFields = modified,
		};

		sut.AreIdentical.ShouldBeFalse();
		sut.IsBackwardsCompatible.ShouldBeTrue();
		sut.AddedFields.ShouldBeSameAs(added);
		sut.RemovedFields.ShouldBeSameAs(removed);
		sut.ModifiedFields.ShouldBeSameAs(modified);
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new SchemaComparisonResult
		{
			AreIdentical = true,
			IsBackwardsCompatible = true,
			AddedFields = [],
			RemovedFields = [],
			ModifiedFields = [],
		};

		sut.BreakingChanges.ShouldBeNull();
		sut.Warnings.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var breaking = new List<string> { "Field 'Status' type changed from keyword to integer" };
		var warnings = new List<string> { "Field 'Name' analyzer changed" };

		var sut = new SchemaComparisonResult
		{
			AreIdentical = false,
			IsBackwardsCompatible = false,
			AddedFields = [],
			RemovedFields = [],
			ModifiedFields = [],
			BreakingChanges = breaking,
			Warnings = warnings,
		};

		sut.BreakingChanges.ShouldBeSameAs(breaking);
		sut.Warnings.ShouldBeSameAs(warnings);
	}
}
