// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="DeadLetterQueueMetricsExtensions"/> convenience overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class DeadLetterQueueMetricsExtensionsShould
{
	[Fact]
	public void RecordEnqueued_WithoutSourceQueue_DelegatesToFullMethod()
	{
		// Arrange
		var metrics = A.Fake<IDeadLetterQueueMetrics>();

		// Act
		metrics.RecordEnqueued("OrderCommand", "MaxRetriesExceeded");

		// Assert
		A.CallTo(() => metrics.RecordEnqueued("OrderCommand", "MaxRetriesExceeded", null))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UpdateDepth_WithoutQueueName_DelegatesToFullMethod()
	{
		// Arrange
		var metrics = A.Fake<IDeadLetterQueueMetrics>();

		// Act
		metrics.UpdateDepth(42);

		// Assert
		A.CallTo(() => metrics.UpdateDepth(42, null))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RecordEnqueued_WithoutSourceQueue_PassesCorrectArguments()
	{
		// Arrange
		var metrics = A.Fake<IDeadLetterQueueMetrics>();

		// Act
		metrics.RecordEnqueued("PaymentEvent", "PoisonMessage");

		// Assert
		A.CallTo(() => metrics.RecordEnqueued("PaymentEvent", "PoisonMessage", null))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UpdateDepth_WithZeroDepth_DelegatesToFullMethod()
	{
		// Arrange
		var metrics = A.Fake<IDeadLetterQueueMetrics>();

		// Act
		metrics.UpdateDepth(0);

		// Assert
		A.CallTo(() => metrics.UpdateDepth(0, null))
			.MustHaveHappenedOnceExactly();
	}
}
