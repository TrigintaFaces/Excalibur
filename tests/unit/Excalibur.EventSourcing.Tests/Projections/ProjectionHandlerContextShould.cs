// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Coverage gap-fill: Unit tests for <see cref="ProjectionHandlerContext"/>
/// constructor, property access, and mutable OverrideProjectionId.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionHandlerContextShould
{
	[Fact]
	public void InitializeAllPropertiesFromConstructor()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var context = new ProjectionHandlerContext("agg-42", "OrderAggregate", 7, timestamp);

		// Assert
		context.AggregateId.ShouldBe("agg-42");
		context.AggregateType.ShouldBe("OrderAggregate");
		context.CommittedVersion.ShouldBe(7);
		context.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void DefaultOverrideProjectionIdToNull()
	{
		// Act
		var context = new ProjectionHandlerContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

		// Assert -- OverrideProjectionId defaults to null
		context.OverrideProjectionId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOverrideProjectionId()
	{
		// Arrange
		var context = new ProjectionHandlerContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);

		// Act
		context.OverrideProjectionId = "custom-id-99";

		// Assert
		context.OverrideProjectionId.ShouldBe("custom-id-99");
	}

	[Fact]
	public void AllowResettingOverrideProjectionIdToNull()
	{
		// Arrange
		var context = new ProjectionHandlerContext("agg-1", "Order", 1, DateTimeOffset.UtcNow);
		context.OverrideProjectionId = "some-id";

		// Act
		context.OverrideProjectionId = null;

		// Assert
		context.OverrideProjectionId.ShouldBeNull();
	}

	[Fact]
	public void PreserveReadOnlyPropertiesAfterOverrideChange()
	{
		// Arrange
		var timestamp = new DateTimeOffset(2026, 3, 30, 12, 0, 0, TimeSpan.Zero);
		var context = new ProjectionHandlerContext("agg-5", "Invoice", 42, timestamp);

		// Act -- mutating OverrideProjectionId should not affect other properties
		context.OverrideProjectionId = "override-1";

		// Assert -- all read-only properties unchanged
		context.AggregateId.ShouldBe("agg-5");
		context.AggregateType.ShouldBe("Invoice");
		context.CommittedVersion.ShouldBe(42);
		context.Timestamp.ShouldBe(timestamp);
	}
}
