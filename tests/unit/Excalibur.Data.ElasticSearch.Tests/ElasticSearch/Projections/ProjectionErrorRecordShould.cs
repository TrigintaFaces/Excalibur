// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ProjectionErrorRecordShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var timestamp = DateTimeOffset.UtcNow;
		var sut = new ProjectionErrorRecord
		{
			Id = "err-1",
			Timestamp = timestamp,
			ProjectionType = "OrderProjection",
			OperationType = "Index",
			IndexName = "orders",
			ErrorMessage = "Mapping exception",
		};

		sut.Id.ShouldBe("err-1");
		sut.Timestamp.ShouldBe(timestamp);
		sut.ProjectionType.ShouldBe("OrderProjection");
		sut.OperationType.ShouldBe("Index");
		sut.IndexName.ShouldBe("orders");
		sut.ErrorMessage.ShouldBe("Mapping exception");
	}

	[Fact]
	public void HaveDefaultsForOptionalProperties()
	{
		var sut = new ProjectionErrorRecord
		{
			Id = "err-1",
			Timestamp = DateTimeOffset.UtcNow,
			ProjectionType = "Test",
			OperationType = "Index",
			IndexName = "test",
			ErrorMessage = "error",
		};

		sut.DocumentId.ShouldBeNull();
		sut.ExceptionDetails.ShouldBeNull();
		sut.AttemptCount.ShouldBe(0);
		sut.IsResolved.ShouldBeFalse();
		sut.ResolvedAt.ShouldBeNull();
		sut.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var resolvedAt = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
		var metadata = new Dictionary<string, object> { ["retryable"] = true };

		var sut = new ProjectionErrorRecord
		{
			Id = "err-2",
			Timestamp = DateTimeOffset.UtcNow,
			ProjectionType = "OrderProjection",
			OperationType = "Update",
			IndexName = "orders",
			ErrorMessage = "Version conflict",
			DocumentId = "doc-456",
			ExceptionDetails = "Full stack trace...",
			AttemptCount = 3,
			IsResolved = true,
			ResolvedAt = resolvedAt,
			Metadata = metadata,
		};

		sut.DocumentId.ShouldBe("doc-456");
		sut.ExceptionDetails.ShouldBe("Full stack trace...");
		sut.AttemptCount.ShouldBe(3);
		sut.IsResolved.ShouldBeTrue();
		sut.ResolvedAt.ShouldBe(resolvedAt);
		sut.Metadata.ShouldBeSameAs(metadata);
	}
}
