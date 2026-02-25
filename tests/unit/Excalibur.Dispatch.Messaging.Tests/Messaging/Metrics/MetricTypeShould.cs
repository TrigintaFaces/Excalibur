// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricTypeShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert - Verify expected enum values exist
		MetricType.Counter.ShouldBe(MetricType.Counter);
		MetricType.Gauge.ShouldBe(MetricType.Gauge);
		MetricType.Histogram.ShouldBe(MetricType.Histogram);
		MetricType.Summary.ShouldBe(MetricType.Summary);
	}

	[Fact]
	public void HaveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<MetricType>();

		// Assert
		values.Distinct().Count().ShouldBe(values.Length);
	}

	[Fact]
	public void HaveCounterAsZero()
	{
		// Assert
		((int)MetricType.Counter).ShouldBe(0);
	}

	[Fact]
	public void HaveCorrectNumericValues()
	{
		// Assert
		((int)MetricType.Counter).ShouldBe(0);
		((int)MetricType.Gauge).ShouldBe(1);
		((int)MetricType.Histogram).ShouldBe(2);
		((int)MetricType.Summary).ShouldBe(3);
	}

	[Fact]
	public void HaveCounterAsDefault()
	{
		// Arrange
		var defaultValue = default(MetricType);

		// Assert
		defaultValue.ShouldBe(MetricType.Counter);
	}
}
