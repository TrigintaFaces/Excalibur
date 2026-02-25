// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="QueueMetrics" /> record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class QueueMetricsShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		var operationMetrics = new Dictionary<string, ComponentMetrics>();
		var sut = new QueueMetrics
		{
			OperationMetrics = operationMetrics,
			CurrentDepth = 10,
			MaxDepthReached = 50,
			AverageDepth = 25.5,
			ThroughputOperationsPerSecond = 1000.0,
		};

		sut.OperationMetrics.ShouldBe(operationMetrics);
		sut.CurrentDepth.ShouldBe(10);
		sut.MaxDepthReached.ShouldBe(50);
		sut.AverageDepth.ShouldBe(25.5);
		sut.ThroughputOperationsPerSecond.ShouldBe(1000.0);
	}

	[Fact]
	public void SupportValueEquality()
	{
		var metrics = new Dictionary<string, ComponentMetrics>();

		var a = new QueueMetrics
		{
			OperationMetrics = metrics,
			CurrentDepth = 5,
			MaxDepthReached = 20,
			AverageDepth = 10.0,
			ThroughputOperationsPerSecond = 500.0,
		};

		var b = new QueueMetrics
		{
			OperationMetrics = metrics,
			CurrentDepth = 5,
			MaxDepthReached = 20,
			AverageDepth = 10.0,
			ThroughputOperationsPerSecond = 500.0,
		};

		a.ShouldBe(b);
	}

	[Fact]
	public void SupportWithExpression()
	{
		var original = new QueueMetrics
		{
			OperationMetrics = new Dictionary<string, ComponentMetrics>(),
			CurrentDepth = 5,
			MaxDepthReached = 20,
			AverageDepth = 10.0,
			ThroughputOperationsPerSecond = 500.0,
		};

		var modified = original with { CurrentDepth = 15, MaxDepthReached = 25 };

		modified.CurrentDepth.ShouldBe(15);
		modified.MaxDepthReached.ShouldBe(25);
		modified.AverageDepth.ShouldBe(10.0); // unchanged
	}
}
