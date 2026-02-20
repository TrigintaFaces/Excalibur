// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class BulkOperationFailureShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var sut = new BulkOperationFailure
		{
			DocumentId = "doc-123",
			ErrorMessage = "Mapping exception on field 'price'",
		};

		sut.DocumentId.ShouldBe("doc-123");
		sut.ErrorMessage.ShouldBe("Mapping exception on field 'price'");
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new BulkOperationFailure
		{
			DocumentId = "doc-1",
			ErrorMessage = "error",
		};

		sut.Document.ShouldBeNull();
		sut.ErrorType.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var doc = new { Id = "doc-456", Name = "Test" };
		var sut = new BulkOperationFailure
		{
			DocumentId = "doc-456",
			ErrorMessage = "Version conflict",
			Document = doc,
			ErrorType = "version_conflict_engine_exception",
		};

		sut.Document.ShouldBeSameAs(doc);
		sut.ErrorType.ShouldBe("version_conflict_engine_exception");
	}
}
