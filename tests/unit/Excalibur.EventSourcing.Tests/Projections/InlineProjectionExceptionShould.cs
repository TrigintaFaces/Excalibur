// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Exceptions;

namespace Excalibur.EventSourcing.Tests.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InlineProjectionExceptionShould
{
	[Fact]
	public void PreserveAllProperties()
	{
		// Arrange
		var inner = new InvalidOperationException("store failure");

		// Act
		var ex = new InlineProjectionException(
			committedVersion: 42,
			aggregateId: "order-1",
			aggregateType: "Order",
			failedProjectionType: typeof(OrderSummary),
			innerException: inner);

		// Assert
		ex.CommittedVersion.ShouldBe(42);
		ex.AggregateId.ShouldBe("order-1");
		ex.AggregateType.ShouldBe("Order");
		ex.FailedProjectionType.ShouldBe(typeof(OrderSummary));
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void IncludeRecoveryGuidanceInMessage()
	{
		// Arrange & Act
		var ex = new InlineProjectionException(
			1, "agg-1", "Agg", typeof(OrderSummary),
			new Exception("fail"));

		// Assert
		ex.Message.ShouldContain("do NOT retry SaveAsync");
		ex.Message.ShouldContain("IProjectionRecovery.ReapplyAsync");
		ex.Message.ShouldContain("OrderSummary");
		ex.Message.ShouldContain("Agg/agg-1");
	}

	[Fact]
	public void ThrowOnNullAggregateId()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InlineProjectionException(1, null!, "Agg", typeof(OrderSummary), new Exception()));
	}

	[Fact]
	public void ThrowOnNullAggregateType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InlineProjectionException(1, "id", null!, typeof(OrderSummary), new Exception()));
	}

	[Fact]
	public void ThrowOnNullFailedProjectionType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InlineProjectionException(1, "id", "Agg", null!, new Exception()));
	}

	[Fact]
	public void InheritFromException()
	{
		// Arrange
		var ex = new InlineProjectionException(
			1, "id", "Agg", typeof(OrderSummary), new Exception());

		// Assert -- verifies it's a proper Exception subclass
		ex.ShouldBeAssignableTo<Exception>();
	}
}
