// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class BulkOperationErrorContextShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var failures = new List<BulkOperationFailure>
		{
			new() { DocumentId = "doc-1", ErrorMessage = "Mapping conflict" },
		};

		var sut = new BulkOperationErrorContext
		{
			ProjectionType = "OrderProjection",
			OperationType = "BulkIndex",
			IndexName = "orders",
			TotalDocuments = 100,
			SuccessfulDocuments = 99,
			Failures = failures,
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.OperationType.ShouldBe("BulkIndex");
		sut.IndexName.ShouldBe("orders");
		sut.TotalDocuments.ShouldBe(100);
		sut.SuccessfulDocuments.ShouldBe(99);
		sut.Failures.ShouldBeSameAs(failures);
	}

	[Fact]
	public void HaveNullDefaultForMetadata()
	{
		var sut = new BulkOperationErrorContext
		{
			ProjectionType = "Test",
			OperationType = "BulkIndex",
			IndexName = "test",
			TotalDocuments = 10,
			SuccessfulDocuments = 10,
			Failures = [],
		};

		sut.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMetadata()
	{
		var metadata = new Dictionary<string, object> { ["batchId"] = "batch-1" };
		var sut = new BulkOperationErrorContext
		{
			ProjectionType = "Test",
			OperationType = "BulkUpdate",
			IndexName = "test",
			TotalDocuments = 50,
			SuccessfulDocuments = 45,
			Failures = [],
			Metadata = metadata,
		};

		sut.Metadata.ShouldBeSameAs(metadata);
	}
}
