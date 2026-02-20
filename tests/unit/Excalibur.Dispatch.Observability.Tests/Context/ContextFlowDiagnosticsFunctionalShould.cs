// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Functional tests for <see cref="ContextFlowDiagnostics"/> verifying anomaly detection, health analysis, and history tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "ContextFlow")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code only")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code only")]
public sealed class ContextFlowDiagnosticsFunctionalShould : IDisposable
{
	private readonly ContextFlowDiagnostics _diagnostics;
	private readonly IContextFlowTracker _fakeTracker;
	private readonly IContextFlowMetrics _fakeMetrics;

	public ContextFlowDiagnosticsFunctionalShould()
	{
		_fakeTracker = A.Fake<IContextFlowTracker>();
		_fakeMetrics = A.Fake<IContextFlowMetrics>();
		A.CallTo(() => _fakeMetrics.GetMetricsSummary()).Returns(new ContextMetricsSummary());

		_diagnostics = new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_fakeTracker,
			_fakeMetrics,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()));
	}

	public void Dispose() => _diagnostics.Dispose();

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowDiagnostics(
			null!,
			A.Fake<IContextFlowTracker>(),
			A.Fake<IContextFlowMetrics>(),
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullTracker()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			null!,
			A.Fake<IContextFlowMetrics>(),
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			A.Fake<IContextFlowTracker>(),
			null!,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions())));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			A.Fake<IContextFlowTracker>(),
			A.Fake<IContextFlowMetrics>(),
			null!));
	}

	[Fact]
	public void DetectMissingCorrelation_OnRedeliveredMessage()
	{
		var context = CreateFakeContext(messageId: "msg-1", deliveryCount: 3, correlationId: null);

		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		anomalies.ShouldContain(a => a.Type == AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void NotDetectMissingCorrelation_OnFirstDelivery()
	{
		var context = CreateFakeContext(messageId: "msg-2", deliveryCount: 1, correlationId: null);

		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		anomalies.ShouldNotContain(a => a.Type == AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void DetectInsufficientContext()
	{
		// Very few fields
		var context = CreateFakeContext(messageId: "msg-3");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		anomalies.ShouldContain(a => a.Type == AnomalyType.InsufficientContext);
	}

	[Fact]
	public void DetectCircularCausation()
	{
		var context = CreateFakeContext(messageId: "msg-4", causationId: "msg-4");

		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		anomalies.ShouldContain(a => a.Type == AnomalyType.CircularCausation);
	}

	[Fact]
	public void DetectPotentialPII()
	{
		var context = CreateFakeContext(messageId: "msg-5");
		var items = new Dictionary<string, object>
		{
			["SSN_Number"] = "123-45-6789",
			["CreditCardNumber"] = "4111-1111-1111-1111",
		};
		A.CallTo(() => context.Items).Returns(items);

		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		anomalies.ShouldContain(a => a.Type == AnomalyType.PotentialPII);
	}

	[Fact]
	public void ThrowOnNullContext_ForDetectAnomalies()
	{
		Should.Throw<ArgumentNullException>(() => _diagnostics.DetectAnomalies(null!).ToList());
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsMissingRequiredFields()
	{
		var options = new ContextObservabilityOptions();
		options.Fields.RequiredContextFields = ["MessageId", "CorrelationId"];

		var diagnostics = new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_fakeTracker,
			_fakeMetrics,
			Microsoft.Extensions.Options.Options.Create(options));

		var context = CreateFakeContext(messageId: null, correlationId: "corr");

		var issues = diagnostics.AnalyzeContextHealth(context).ToList();

		issues.ShouldContain(i => i.Category == "MissingField");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsHighDeliveryCount()
	{
		var context = CreateFakeContext(messageId: "msg-hd", deliveryCount: 10);

		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		issues.ShouldContain(i => i.Category == "HighDeliveryCount");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsStaleTimestamp()
	{
		var context = CreateFakeContext(messageId: "msg-stale");
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow.AddHours(-2));

		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		issues.ShouldContain(i => i.Category == "StaleMessage");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsValidationFailure()
	{
		var context = CreateFakeContext(messageId: "msg-vf");
		var properties = new Dictionary<string, object?>();
		A.CallTo(() => context.Properties).Returns(properties);
		// Set the validation result via the Properties dictionary (extension method reads from "__ValidationResult")
		var validationResult = new TestValidationResult { IsValid = false };
		properties["__ValidationResult"] = validationResult;

		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		issues.ShouldContain(i => i.Category == "ValidationFailure");
	}

	[Fact]
	public void AnalyzeContextHealth_ThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() => _diagnostics.AnalyzeContextHealth(null!).ToList());
	}

	[Fact]
	public void TrackContextHistory()
	{
		var context = CreateFakeContext(messageId: "msg-hist");

		_diagnostics.TrackContextHistory(context, "Created", "Initial creation");

		var history = _diagnostics.GetContextHistory("msg-hist");
		history.ShouldNotBeNull();
		history.Events.ShouldNotBeEmpty();
		history.Events[0].EventType.ShouldBe("Created");
		history.Events[0].Details.ShouldBe("Initial creation");
	}

	[Fact]
	public void TrackContextHistory_ThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() => _diagnostics.TrackContextHistory(null!, "Created"));
	}

	[Fact]
	public void ReturnNullForUnknownHistory()
	{
		_diagnostics.GetContextHistory("nonexistent").ShouldBeNull();
	}

	[Fact]
	public void GetRecentAnomalies()
	{
		var context1 = CreateFakeContext(messageId: "msg-a1", deliveryCount: 3, correlationId: null);
		var context2 = CreateFakeContext(messageId: "msg-a2", causationId: "msg-a2");

		var anomalies1 = _diagnostics.DetectAnomalies(context1).ToList();
		var anomalies2 = _diagnostics.DetectAnomalies(context2).ToList();

		var recent = _diagnostics.GetRecentAnomalies().ToList();
		recent.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void GenerateCorrelationReport_ForUnknownCorrelation()
	{
		A.CallTo(() => _fakeTracker.GetContextLineage("unknown")).Returns(null);

		var report = _diagnostics.GenerateCorrelationReport("unknown");

		report.ShouldContain("No lineage data available");
	}

	[Fact]
	public void GenerateCorrelationReport_WithLineage()
	{
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-report",
			OriginMessageId = "msg-origin",
			StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
			Snapshots =
			[
				new ContextSnapshot
				{
					MessageId = "msg-origin",
					Stage = "Start",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
					FieldCount = 10,
					SizeBytes = 500,
					Fields = new Dictionary<string, object?>(),
					Metadata = new Dictionary<string, object>(),
				},
			],
			ServiceBoundaries =
			[
				new ServiceBoundaryTransition
				{
					ServiceName = "OrderService",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3),
					ContextPreserved = true,
				},
			],
		};

		A.CallTo(() => _fakeTracker.GetContextLineage("corr-report")).Returns(lineage);

		var report = _diagnostics.GenerateCorrelationReport("corr-report");

		report.ShouldContain("corr-report");
		report.ShouldContain("msg-origin");
		report.ShouldContain("OrderService");
		report.ShouldContain("Preserved");
	}

	[Fact]
	public void ExportDiagnosticData()
	{
		var context = CreateFakeContext(messageId: "msg-export");
		_diagnostics.TrackContextHistory(context, "Test");

		var json = _diagnostics.ExportDiagnosticData("msg-export");

		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("msg-export");
	}

	[Fact]
	public void VisualizeContextFlow_ReturnsNoDataMessage_WhenNoSnapshots()
	{
		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-viz")).Returns([]);

		var result = _diagnostics.VisualizeContextFlow("msg-viz");

		result.ShouldContain("No context flow data available");
	}

	[Fact]
	public void VisualizeContextFlow_WithSnapshots()
	{
		var snapshots = new List<ContextSnapshot>
		{
			new()
			{
				MessageId = "msg-viz2",
				Stage = "Start",
				Timestamp = DateTimeOffset.UtcNow.AddSeconds(-2),
				FieldCount = 10,
				SizeBytes = 500,
				Fields = new Dictionary<string, object?> { ["MessageId"] = "msg-viz2", ["CorrelationId"] = "corr-viz" },
				Metadata = new Dictionary<string, object>(),
			},
			new()
			{
				MessageId = "msg-viz2",
				Stage = "Handler",
				Timestamp = DateTimeOffset.UtcNow,
				FieldCount = 12,
				SizeBytes = 600,
				Fields = new Dictionary<string, object?> { ["MessageId"] = "msg-viz2", ["CorrelationId"] = "corr-viz", ["NewField"] = "added" },
				Metadata = new Dictionary<string, object>(),
			},
		};

		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-viz2")).Returns(snapshots);

		var result = _diagnostics.VisualizeContextFlow("msg-viz2");

		result.ShouldContain("msg-viz2");
		result.ShouldContain("Start");
		result.ShouldContain("Handler");
		result.ShouldContain("Summary");
	}

	private static IMessageContext CreateFakeContext(
		string? messageId = "msg-default",
		string? correlationId = "corr-default",
		string? causationId = null,
		int deliveryCount = 1)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.CausationId).Returns(causationId);
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.DeliveryCount).Returns(deliveryCount);
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.ExternalId).Returns(null);
		A.CallTo(() => context.UserId).Returns(null);
		A.CallTo(() => context.TraceParent).Returns(null);
		A.CallTo(() => context.TenantId).Returns(null);
		A.CallTo(() => context.Source).Returns(null);
		A.CallTo(() => context.ContentType).Returns(null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	/// <summary>
	/// Test implementation of <see cref="IValidationResult"/> to avoid faking an interface with static abstract members.
	/// </summary>
	private sealed class TestValidationResult : IValidationResult
	{
		public bool IsValid { get; set; }
		public IReadOnlyCollection<object> Errors { get; set; } = [];
		public static IValidationResult Failed(params object[] errors) => new TestValidationResult { IsValid = false, Errors = errors };
		public static IValidationResult Success() => new TestValidationResult { IsValid = true };
	}
}
