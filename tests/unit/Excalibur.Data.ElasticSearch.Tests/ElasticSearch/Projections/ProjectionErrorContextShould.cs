// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionErrorContextShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var ex = new InvalidOperationException("test error");
		var sut = new ProjectionErrorContext
		{
			ProjectionType = "OrderProjection",
			OperationType = "Index",
			Exception = ex,
			IndexName = "orders",
		};

		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.OperationType.ShouldBe("Index");
		sut.Exception.ShouldBeSameAs(ex);
		sut.IndexName.ShouldBe("orders");
	}

	[Fact]
	public void HaveNullDefaultsForOptionalProperties()
	{
		var sut = new ProjectionErrorContext
		{
			ProjectionType = "Test",
			OperationType = "Index",
			Exception = new InvalidOperationException("test"),
			IndexName = "test",
		};

		sut.Document.ShouldBeNull();
		sut.DocumentId.ShouldBeNull();
		sut.Metadata.ShouldBeNull();
		sut.AttemptCount.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var doc = new { Id = "123" };
		var metadata = new Dictionary<string, object> { ["key"] = "value" };
		var sut = new ProjectionErrorContext
		{
			ProjectionType = "Test",
			OperationType = "Update",
			Exception = new InvalidOperationException("error"),
			IndexName = "test",
			Document = doc,
			DocumentId = "doc-123",
			AttemptCount = 3,
			Metadata = metadata,
		};

		sut.Document.ShouldBeSameAs(doc);
		sut.DocumentId.ShouldBe("doc-123");
		sut.AttemptCount.ShouldBe(3);
		sut.Metadata.ShouldBeSameAs(metadata);
	}
}
