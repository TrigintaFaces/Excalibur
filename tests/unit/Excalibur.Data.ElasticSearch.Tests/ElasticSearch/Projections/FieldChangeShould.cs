// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class FieldChangeShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new FieldChange
		{
			FieldName = "Email",
			FieldPath = "customer.email",
			ChangeType = FieldChangeType.Added,
		};

		sut.FieldName.ShouldBe("Email");
		sut.FieldPath.ShouldBe("customer.email");
		sut.ChangeType.ShouldBe(FieldChangeType.Added);
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new FieldChange
		{
			FieldName = "Test",
			FieldPath = "test",
			ChangeType = FieldChangeType.Added,
		};

		sut.OldType.ShouldBeNull();
		sut.NewType.ShouldBeNull();
		sut.IsBreaking.ShouldBeFalse();
		sut.Impact.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var sut = new FieldChange
		{
			FieldName = "Status",
			FieldPath = "order.status",
			ChangeType = FieldChangeType.TypeChanged,
			OldType = "keyword",
			NewType = "integer",
			IsBreaking = true,
			Impact = "Existing queries filtering by Status text will fail",
		};

		sut.OldType.ShouldBe("keyword");
		sut.NewType.ShouldBe("integer");
		sut.IsBreaking.ShouldBeTrue();
		sut.Impact.ShouldBe("Existing queries filtering by Status text will fail");
	}
}
