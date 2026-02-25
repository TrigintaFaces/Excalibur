// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricMetadata"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricMetadataShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void CreateWithAllParameters()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(
			metricId: 1,
			name: "http_requests_total",
			description: "Total HTTP requests",
			unit: "requests",
			type: MetricType.Counter,
			labelNames: ["method", "path", "status"]);

		// Assert
		metadata.MetricId.ShouldBe(1);
		metadata.Name.ShouldBe("http_requests_total");
		metadata.Description.ShouldBe("Total HTTP requests");
		metadata.Unit.ShouldBe("requests");
		metadata.Type.ShouldBe(MetricType.Counter);
		metadata.LabelNames.ShouldBe(["method", "path", "status"]);
	}

	[Fact]
	public void ThrowOnNullName()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MetricMetadata(1, null!, null, null, MetricType.Counter));
	}

	[Fact]
	public void UseEmptyStringForNullDescription()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter);

		// Assert
		metadata.Description.ShouldBe(string.Empty);
	}

	[Fact]
	public void UseEmptyStringForNullUnit()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter);

		// Assert
		metadata.Unit.ShouldBe(string.Empty);
	}

	[Fact]
	public void UseEmptyArrayForNullLabelNames()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter, null);

		// Assert
		metadata.LabelNames.ShouldBeEmpty();
	}

	#endregion

	#region MetricType Tests

	[Fact]
	public void SupportCounterType()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter);

		// Assert
		metadata.Type.ShouldBe(MetricType.Counter);
	}

	[Fact]
	public void SupportGaugeType()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Gauge);

		// Assert
		metadata.Type.ShouldBe(MetricType.Gauge);
	}

	[Fact]
	public void SupportHistogramType()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Histogram);

		// Assert
		metadata.Type.ShouldBe(MetricType.Histogram);
	}

	[Fact]
	public void SupportSummaryType()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Summary);

		// Assert
		metadata.Type.ShouldBe(MetricType.Summary);
	}

	#endregion

	#region LabelNames Tests

	[Fact]
	public void StoreSingleLabel()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter, "label1");

		// Assert
		metadata.LabelNames.Length.ShouldBe(1);
		metadata.LabelNames[0].ShouldBe("label1");
	}

	[Fact]
	public void StoreMultipleLabels()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(
			1, "test", null, null, MetricType.Counter,
			"method", "path", "status_code", "handler");

		// Assert
		metadata.LabelNames.Length.ShouldBe(4);
		metadata.LabelNames.ShouldContain("method");
		metadata.LabelNames.ShouldContain("path");
		metadata.LabelNames.ShouldContain("status_code");
		metadata.LabelNames.ShouldContain("handler");
	}

	[Fact]
	public void HandleEmptyLabelArray()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(1, "test", null, null, MetricType.Counter, []);

		// Assert
		metadata.LabelNames.ShouldBeEmpty();
	}

	#endregion

	#region Real-World Examples

	[Fact]
	public void CreateHttpRequestDurationMetric()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(
			metricId: 100,
			name: "http_request_duration_seconds",
			description: "HTTP request latency in seconds",
			unit: "seconds",
			type: MetricType.Histogram,
			labelNames: ["method", "path", "status"]);

		// Assert
		metadata.MetricId.ShouldBe(100);
		metadata.Name.ShouldBe("http_request_duration_seconds");
		metadata.Type.ShouldBe(MetricType.Histogram);
		metadata.LabelNames.Length.ShouldBe(3);
	}

	[Fact]
	public void CreateActiveConnectionsMetric()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(
			metricId: 200,
			name: "active_connections",
			description: "Current number of active connections",
			unit: "connections",
			type: MetricType.Gauge,
			labelNames: ["pool_name"]);

		// Assert
		metadata.Type.ShouldBe(MetricType.Gauge);
		metadata.Unit.ShouldBe("connections");
	}

	[Fact]
	public void CreateMessageQueueDepthMetric()
	{
		// Arrange & Act
		var metadata = new MetricMetadata(
			metricId: 300,
			name: "message_queue_depth",
			description: "Current number of messages in queue",
			unit: "messages",
			type: MetricType.Gauge,
			labelNames: ["queue_name", "priority"]);

		// Assert
		metadata.Name.ShouldBe("message_queue_depth");
		metadata.LabelNames.ShouldContain("queue_name");
		metadata.LabelNames.ShouldContain("priority");
	}

	#endregion
}
