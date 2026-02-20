// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DocumentMigrationFailureShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new DocumentMigrationFailure
		{
			DocumentId = "doc-123",
			Reason = "Type mismatch: expected integer, got string",
		};

		sut.DocumentId.ShouldBe("doc-123");
		sut.Reason.ShouldBe("Type mismatch: expected integer, got string");
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new DocumentMigrationFailure
		{
			DocumentId = "doc-1",
			Reason = "error",
		};

		sut.FailedField.ShouldBeNull();
		sut.OriginalValue.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var sut = new DocumentMigrationFailure
		{
			DocumentId = "doc-456",
			Reason = "Invalid date format",
			FailedField = "created_at",
			OriginalValue = "not-a-date",
		};

		sut.FailedField.ShouldBe("created_at");
		sut.OriginalValue.ShouldBe("not-a-date");
	}
}
