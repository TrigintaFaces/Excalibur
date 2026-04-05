// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class ContextFlowDiagnosticsShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker;
	private readonly IContextFlowMetrics _fakeMetrics;
	private readonly ContextObservabilityOptions _options;
	private readonly ContextFlowDiagnostics _sut;

	public ContextFlowDiagnosticsShould()
	{
		_fakeTracker = A.Fake<IContextFlowTracker>();
		_fakeMetrics = A.Fake<IContextFlowMetrics>();

		_options = new ContextObservabilityOptions();

		_sut = new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_fakeTracker,
			_fakeMetrics,
			Microsoft.Extensions.Options.Options.Create(_options));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(null!, _fakeTracker, _fakeMetrics,
				Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowOnNullTracker()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(
				NullLogger<ContextFlowDiagnostics>.Instance, null!, _fakeMetrics,
				Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(
				NullLogger<ContextFlowDiagnostics>.Instance, _fakeTracker, null!,
				Microsoft.Extensions.Options.Options.Create(_options)));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(
				NullLogger<ContextFlowDiagnostics>.Instance, _fakeTracker, _fakeMetrics,
				null!));
	}

#pragma warning disable IL2026, IL3050
	[Fact]
	public void VisualizeContextFlowReturnNoDataForUnknownMessage()
	{
		// Arrange
		A.CallTo(() => _fakeTracker.GetMessageSnapshots("unknown"))
			.Returns(Enumerable.Empty<ContextSnapshot>());

		// Act
		var result = _sut.VisualizeContextFlow("unknown");

		// Assert
		result.ShouldContain("No context flow data available");
	}

	[Fact]
	public void VisualizeContextFlowWithSnapshots()
	{
		// Arrange
		var snapshots = new[]
		{
			new ContextSnapshot
			{
				MessageId = "msg-001",
				Stage = "Validation",
				Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(-100),
				Fields = new Dictionary<string, object?> { ["MessageId"] = "msg-001" },
				FieldCount = 1,
				SizeBytes = 50,
				Metadata = new Dictionary<string, object>(),
			},
			new ContextSnapshot
			{
				MessageId = "msg-001",
				Stage = "Handler",
				Timestamp = DateTimeOffset.UtcNow,
				Fields = new Dictionary<string, object?> { ["MessageId"] = "msg-001", ["Result"] = "ok" },
				FieldCount = 2,
				SizeBytes = 80,
				Metadata = new Dictionary<string, object>(),
			},
		};

		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-001"))
			.Returns(snapshots);

		// Act
		var result = _sut.VisualizeContextFlow("msg-001");

		// Assert
		result.ShouldContain("Context Flow Visualization");
		result.ShouldContain("Validation");
		result.ShouldContain("Handler");
		result.ShouldContain("Summary");
		result.ShouldContain("Total Stages: 2");
	}

	[Fact]
	public void AnalyzeContextHealthThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.AnalyzeContextHealth(null!).ToList());
	}

	[Fact]
	public void AnalyzeContextHealthDetectsStaleTimestamps()
	{
		// Arrange
		var items = new Dictionary<string, object>
		{
			["__SentTimestampUtc"] = DateTimeOffset.UtcNow.AddHours(-2),
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "StaleMessage");
	}

	[Fact]
	public void AnalyzeContextHealthDetectsHighDeliveryCount()
	{
		// Arrange
		var processingFeature = A.Fake<IMessageProcessingFeature>();
		A.CallTo(() => processingFeature.DeliveryCount).Returns(10);

		var features = new Dictionary<Type, object>
		{
			[typeof(IMessageProcessingFeature)] = processingFeature,
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.Features).Returns(features);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "HighDeliveryCount");
	}

	[Fact]
	public void DetectAnomaliesThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectAnomalies(null!).ToList());
	}

	[Fact]
	public void DetectAnomaliesFindsCircularCausation()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CausationId).Returns("msg-001"); // circular!
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.CircularCausation);
	}

	[Fact]
	public void DetectAnomaliesFindsInsufficientContext()
	{
		// Arrange - context with very few fields (all null)
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.InsufficientContext);
	}

	[Fact]
	public void DetectAnomaliesFindsPotentialPII()
	{
		// Arrange
		var items = new Dictionary<string, object>
		{
			["CustomerEmail"] = "test@test.com",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.PotentialPII);
	}

	[Fact]
	public void DetectAnomaliesFindsMissingCorrelationOnRedeliver()
	{
		// Arrange
		var processingFeature = A.Fake<IMessageProcessingFeature>();
		A.CallTo(() => processingFeature.DeliveryCount).Returns(3);

		var features = new Dictionary<Type, object>
		{
			[typeof(IMessageProcessingFeature)] = processingFeature,
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CorrelationId).Returns((string?)null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.Features).Returns(features);

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void TrackContextHistoryThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.TrackContextHistory(null!, "TestEvent"));
	}

	[Fact]
	public void TrackContextHistoryRecordsEvent()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		_sut.TrackContextHistory(context, "Received", "From OrderService");

		// Assert
		var history = _sut.GetContextHistory("msg-001");
		history.ShouldNotBeNull();
		history.Events.Count.ShouldBe(1);
		history.Events[0].EventType.ShouldBe("Received");
		history.Events[0].Details.ShouldBe("From OrderService");
	}

	[Fact]
	public void GetContextHistoryReturnsNullForUnknownMessage()
	{
		var history = _sut.GetContextHistory("unknown");
		history.ShouldBeNull();
	}

	[Fact]
	public void GetRecentAnomaliesReturnsEmpty()
	{
		var anomalies = _sut.GetRecentAnomalies().ToList();
		anomalies.ShouldBeEmpty();
	}

	[Fact]
	public void GetRecentAnomaliesReturnsDetectedAnomalies()
	{
		// Arrange - trigger an anomaly
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CausationId).Returns("msg-001"); // circular
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		_ = _sut.DetectAnomalies(context).ToList();

		// Act
		var anomalies = _sut.GetRecentAnomalies().ToList();

		// Assert
		anomalies.ShouldNotBeEmpty();
	}

	[Fact]
	public void GenerateCorrelationReportForUnknownCorrelation()
	{
		// Arrange
		A.CallTo(() => _fakeTracker.GetContextLineage("unknown"))
			.Returns(null);

		// Act
		var report = _sut.GenerateCorrelationReport("unknown");

		// Assert
		report.ShouldContain("No lineage data available");
	}

	[Fact]
	public void GenerateCorrelationReportWithLineage()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-001",
			OriginMessageId = "msg-001",
			StartTime = DateTimeOffset.UtcNow.AddSeconds(-5),
			Snapshots =
			[
				new ContextSnapshot
				{
					MessageId = "msg-001",
					Stage = "Validation",
					Timestamp = DateTimeOffset.UtcNow.AddSeconds(-4),
					Fields = new Dictionary<string, object?>(),
					FieldCount = 5,
					SizeBytes = 200,
					Metadata = new Dictionary<string, object>(),
				},
			],
			ServiceBoundaries =
			[
				new ServiceBoundaryTransition
				{
					ServiceName = "OrderService",
					Timestamp = DateTimeOffset.UtcNow.AddSeconds(-3),
					ContextPreserved = true,
				},
			],
		};

		A.CallTo(() => _fakeTracker.GetContextLineage("corr-001"))
			.Returns(lineage);

		// Act
		var report = _sut.GenerateCorrelationReport("corr-001");

		// Assert
		report.ShouldContain("Correlation Chain Report");
		report.ShouldContain("OrderService");
		report.ShouldContain("Statistics");
		report.ShouldContain("Preservation Rate");
	}

	[Fact]
	public void ExportDiagnosticDataReturnsValidJson()
	{
		// Act
		var json = _sut.ExportDiagnosticData();

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldStartWith("{");
	}

	[Fact]
	public void ExportDiagnosticDataForSpecificMessage()
	{
		// Arrange - track some history first
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-export");
		A.CallTo(() => context.CorrelationId).Returns("corr-export");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		_sut.TrackContextHistory(context, "Received");

		// Act
		var json = _sut.ExportDiagnosticData("msg-export");

		// Assert
		json.ShouldContain("msg-export");
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public void DisposeDoesNotThrow()
	{
		_sut.Dispose();
		// Dispose is a no-op (dependencies are DI-managed)
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
