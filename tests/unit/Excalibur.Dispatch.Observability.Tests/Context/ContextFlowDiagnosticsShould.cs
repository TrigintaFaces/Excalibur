// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextFlowDiagnostics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowDiagnosticsShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _fakeMetrics = A.Fake<IContextFlowMetrics>();
	private ContextFlowDiagnostics? _diagnostics;

	public void Dispose() => _diagnostics?.Dispose();

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(null!, _fakeTracker, _fakeMetrics,
				MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullTracker()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(NullLogger<ContextFlowDiagnostics>.Instance,
				null!, _fakeMetrics, MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(NullLogger<ContextFlowDiagnostics>.Instance,
				_fakeTracker, null!, MsOptions.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowDiagnostics(NullLogger<ContextFlowDiagnostics>.Instance,
				_fakeTracker, _fakeMetrics, null!));
	}

#pragma warning disable IL2026, IL3050 // Suppress for test
	[Fact]
	public void VisualizeContextFlow_ReturnsNoDataMessage_WhenNoSnapshots()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-1"))
			.Returns([]);

		// Act
		var result = _diagnostics.VisualizeContextFlow("msg-1");

		// Assert
		result.ShouldContain("No context flow data available");
		result.ShouldContain("msg-1");
	}

	[Fact]
	public void VisualizeContextFlow_ReturnsVisualization_WhenSnapshotsExist()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var snapshots = new List<ContextSnapshot>
		{
			new()
			{
				MessageId = "msg-1",
				Stage = "PreProcessing",
				Timestamp = DateTimeOffset.UtcNow,
				Fields = new Dictionary<string, object?>(StringComparer.Ordinal) { ["MessageId"] = "msg-1" },
				FieldCount = 1,
				SizeBytes = 50,
				Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
			},
		};
		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-1"))
			.Returns(snapshots);

		// Act
		var result = _diagnostics.VisualizeContextFlow("msg-1");

		// Assert
		result.ShouldContain("Context Flow Visualization");
		result.ShouldContain("PreProcessing");
		result.ShouldContain("Summary");
	}

	[Fact]
	public void AnalyzeContextHealth_ThrowsOnNull()
	{
		_diagnostics = CreateDiagnostics();
		Should.Throw<ArgumentNullException>(() =>
			_diagnostics.AnalyzeContextHealth(null!));
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsHighDeliveryCount()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.DeliveryCount).Returns(10);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "HighDeliveryCount");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsStaleTimestamps()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow.AddHours(-2));
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "StaleMessage");
	}

	[Fact]
	public void DetectAnomalies_ThrowsOnNull()
	{
		_diagnostics = CreateDiagnostics();
		Should.Throw<ArgumentNullException>(() =>
			_diagnostics.DetectAnomalies(null!));
	}

	[Fact]
	public void DetectAnomalies_FindsMissingCorrelation()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.DeliveryCount).Returns(2);
		A.CallTo(() => context.CorrelationId).Returns((string?)null);
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CausationId).Returns((string?)null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void DetectAnomalies_FindsCircularCausation()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CausationId).Returns("msg-1"); // Same as MessageId = circular
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.CircularCausation);
	}

	[Fact]
	public void DetectAnomalies_FindsPotentialPII()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["user_email_address"] = "test@example.com",
		});

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.PotentialPII);
	}

	[Fact]
	public void TrackContextHistory_ThrowsOnNull()
	{
		_diagnostics = CreateDiagnostics();
		Should.Throw<ArgumentNullException>(() =>
			_diagnostics.TrackContextHistory(null!, "test"));
	}

	[Fact]
	public void TrackContextHistory_RecordsEvent()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act
		_diagnostics.TrackContextHistory(context, "Created", "Initial creation");

		// Assert
		var history = _diagnostics.GetContextHistory("msg-1");
		history.ShouldNotBeNull();
		history.Events.Count.ShouldBe(1);
		history.Events[0].EventType.ShouldBe("Created");
	}

	[Fact]
	public void GetRecentAnomalies_ReturnsEmpty_WhenNone()
	{
		_diagnostics = CreateDiagnostics();
		var anomalies = _diagnostics.GetRecentAnomalies().ToList();
		anomalies.ShouldBeEmpty();
	}

	[Fact]
	public void GenerateCorrelationReport_ReturnsNoDataMessage_WhenNotFound()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		A.CallTo(() => _fakeTracker.GetContextLineage("corr-1"))
			.Returns(null);

		// Act
		var result = _diagnostics.GenerateCorrelationReport("corr-1");

		// Assert
		result.ShouldContain("No lineage data available");
	}

	[Fact]
	public void GenerateCorrelationReport_ReturnsReport_WhenLineageExists()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-1",
			OriginMessageId = "msg-1",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [],
			ServiceBoundaries = [],
		};
		A.CallTo(() => _fakeTracker.GetContextLineage("corr-1"))
			.Returns(lineage);

		// Act
		var result = _diagnostics.GenerateCorrelationReport("corr-1");

		// Assert
		result.ShouldContain("Correlation Chain Report");
		result.ShouldContain("corr-1");
		result.ShouldContain("Statistics");
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public void ImplementIContextFlowDiagnostics()
	{
		_diagnostics = CreateDiagnostics();
		_diagnostics.ShouldBeAssignableTo<IContextFlowDiagnostics>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_diagnostics = CreateDiagnostics();
		_diagnostics.ShouldBeAssignableTo<IDisposable>();
	}

	private ContextFlowDiagnostics CreateDiagnostics()
	{
		return new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_fakeTracker,
			_fakeMetrics,
			MsOptions.Create(new ContextObservabilityOptions()));
	}
}
