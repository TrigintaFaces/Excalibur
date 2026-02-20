// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Deep coverage tests for <see cref="ContextFlowDiagnostics"/> covering all analysis code paths,
/// anomaly detection, history tracking, correlation reports, and diagnostics export.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextFlowDiagnosticsDepthCoverageShould : IDisposable
{
	private readonly IContextFlowTracker _tracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _metrics = A.Fake<IContextFlowMetrics>();
	private readonly ContextObservabilityOptions _options;
	private readonly ContextFlowDiagnostics _sut;

	public ContextFlowDiagnosticsDepthCoverageShould()
	{
		_options = new ContextObservabilityOptions();
		_options.Fields.RequiredContextFields = ["MessageId", "CorrelationId"];
		_options.Limits.MaxContextSizeBytes = 1000;
		_options.Limits.MaxHistoryEventsPerContext = 5;
		_options.Limits.MaxAnomalyQueueSize = 10;
		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
		_sut = new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_tracker,
			_metrics,
			optionsWrapper);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public void VisualizeContextFlow_ReturnNoDataMessage_WhenNoSnapshots()
	{
		// Arrange
		A.CallTo(() => _tracker.GetMessageSnapshots("msg-1"))
			.Returns(Enumerable.Empty<ContextSnapshot>());

		// Act
		var result = _sut.VisualizeContextFlow("msg-1");

		// Assert
		result.ShouldContain("No context flow data available for message msg-1");
	}

	[Fact]
	public void VisualizeContextFlow_IncludeStagesAndKeyFields()
	{
		// Arrange
		var snapshot1 = new ContextSnapshot
		{
			MessageId = "msg-1",
			Stage = "PreProcessing",
			Timestamp = DateTimeOffset.UtcNow.AddSeconds(-5),
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["MessageId"] = "msg-1",
				["CorrelationId"] = "corr-1",
			},
			FieldCount = 2,
			SizeBytes = 100,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
		};
		var snapshot2 = new ContextSnapshot
		{
			MessageId = "msg-1",
			Stage = "PostProcessing",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["MessageId"] = "msg-1",
				["CorrelationId"] = "corr-1",
				["NewField"] = "added",
			},
			FieldCount = 3,
			SizeBytes = 200,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
		};

		A.CallTo(() => _tracker.GetMessageSnapshots("msg-1"))
			.Returns(new[] { snapshot1, snapshot2 });

		// Act
		var result = _sut.VisualizeContextFlow("msg-1");

		// Assert
		result.ShouldContain("Context Flow Visualization for Message: msg-1");
		result.ShouldContain("Stage: PreProcessing");
		result.ShouldContain("Stage: PostProcessing");
		result.ShouldContain("Key Fields:");
		result.ShouldContain("MessageId: msg-1");
		result.ShouldContain("CorrelationId: corr-1");
		result.ShouldContain("Summary:");
		result.ShouldContain("Total Stages: 2");
		result.ShouldContain("Added: NewField");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectMissingRequiredFields()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(null);
		A.CallTo(() => context.CorrelationId).Returns(null);
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(0);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "MissingField" && i.Field == "MessageId");
		issues.ShouldContain(i => i.Category == "MissingField" && i.Field == "CorrelationId");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectStaleTimestamps()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.SentTimestampUtc).Returns(DateTimeOffset.UtcNow.AddHours(-2));
		A.CallTo(() => context.DeliveryCount).Returns(0);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "StaleMessage");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectHighDeliveryCount()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(10);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "HighDeliveryCount");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectValidationFailures()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(0);

		// Set up validation result via context Items (extension method uses GetProperty with __ValidationResult key)
		var failedResult = new TestValidationResult(false);
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["__ValidationResult"] = failedResult,
		};
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "ValidationFailure");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectAuthorizationFailures()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(0);

		// Set up authorization result via context Items (extension method uses GetProperty with __AuthorizationResult key)
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["__AuthorizationResult"] = authResult,
		};
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var issues = _sut.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "AuthorizationFailure");
	}

	[Fact]
	public void AnalyzeContextHealth_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() => _sut.AnalyzeContextHealth(null!));
	}

	[Fact]
	public void DetectAnomalies_DetectMissingCorrelationOnRedelivery()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns(null);
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(3);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.MissingCorrelation);
	}

	[Fact]
	public void DetectAnomalies_DetectCircularCausation()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CausationId).Returns("msg-1"); // circular
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.DeliveryCount).Returns(0);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.CircularCausation);
	}

	[Fact]
	public void DetectAnomalies_DetectPotentialPII()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.DeliveryCount).Returns(0);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["UserEmail"] = "user@example.com",
			["SSN_Data"] = "123-45-6789",
			["CreditCardNumber"] = "4111-1111",
			["safe_field_1"] = "1",
			["safe_field_2"] = "2",
			["safe_field_3"] = "3",
			["safe_field_4"] = "4",
			["safe_field_5"] = "5",
		});

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.PotentialPII);
	}

	[Fact]
	public void DetectAnomalies_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() => _sut.DetectAnomalies(null!));
	}

	[Fact]
	public void TrackContextHistory_RecordAndRetrieveHistory()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-track-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		_sut.TrackContextHistory(context, "Created", "Message created");
		_sut.TrackContextHistory(context, "Processed", "Processing complete");

		// Assert
		var history = _sut.GetContextHistory("msg-track-1");
		history.ShouldNotBeNull();
		history.MessageId.ShouldBe("msg-track-1");
		history.Events.Count.ShouldBe(2);
		history.Events[0].EventType.ShouldBe("Created");
		history.Events[1].EventType.ShouldBe("Processed");
	}

	[Fact]
	public void TrackContextHistory_TrimHistoryWhenExceedsLimit()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-limit");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act - add more than MaxHistoryEventsPerContext (5)
		for (var i = 0; i < 8; i++)
		{
			_sut.TrackContextHistory(context, $"Event-{i}", $"Details {i}");
		}

		// Assert
		var history = _sut.GetContextHistory("msg-limit");
		history.ShouldNotBeNull();
		history.Events.Count.ShouldBeLessThanOrEqualTo(5);
	}

	[Fact]
	public void TrackContextHistory_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() => _sut.TrackContextHistory(null!, "event"));
	}

	[Fact]
	public void GetContextHistory_ReturnNull_WhenNotFound()
	{
		var result = _sut.GetContextHistory("nonexistent");
		result.ShouldBeNull();
	}

	[Fact]
	public void GetRecentAnomalies_ReturnLimitedResults()
	{
		// Arrange - push some anomalies via DetectAnomalies
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-a");
		A.CallTo(() => context.CausationId).Returns("msg-a"); // circular
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.DeliveryCount).Returns(0);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		_sut.DetectAnomalies(context);

		// Act
		var anomalies = _sut.GetRecentAnomalies(5).ToList();

		// Assert
		anomalies.Count.ShouldBeGreaterThan(0);
		anomalies.Count.ShouldBeLessThanOrEqualTo(5);
	}

	[Fact]
	public void GenerateCorrelationReport_ReturnNoDataMessage_WhenLineageNotFound()
	{
		// Arrange
		A.CallTo(() => _tracker.GetContextLineage("unknown-corr"))
			.Returns(null);

		// Act
		var result = _sut.GenerateCorrelationReport("unknown-corr");

		// Assert
		result.ShouldContain("No lineage data available for correlation unknown-corr");
	}

	[Fact]
	public void GenerateCorrelationReport_IncludeFullReport()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-100",
			OriginMessageId = "msg-origin",
			StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
			Snapshots =
			[
				new ContextSnapshot
				{
					MessageId = "msg-origin",
					Stage = "PreProcessing",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-4),
					Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
					FieldCount = 5,
					SizeBytes = 200,
					Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
				},
				new ContextSnapshot
				{
					MessageId = "msg-origin",
					Stage = "PostProcessing",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-3),
					Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
					FieldCount = 7,
					SizeBytes = 350,
					Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
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
				new ServiceBoundaryTransition
				{
					ServiceName = "PaymentService",
					Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2),
					ContextPreserved = false,
				},
			],
		};

		A.CallTo(() => _tracker.GetContextLineage("corr-100"))
			.Returns(lineage);

		// Act
		var result = _sut.GenerateCorrelationReport("corr-100");

		// Assert
		result.ShouldContain("Correlation Chain Report: corr-100");
		result.ShouldContain("Origin Message: msg-origin");
		result.ShouldContain("Total Snapshots: 2");
		result.ShouldContain("Service Boundaries Crossed: 2");
		result.ShouldContain("Stage Progression:");
		result.ShouldContain("PreProcessing");
		result.ShouldContain("PostProcessing");
		result.ShouldContain("Service Boundaries:");
		result.ShouldContain("OrderService - Preserved");
		result.ShouldContain("PaymentService - Lost");
		result.ShouldContain("Statistics:");
		result.ShouldContain("Preservation Rate:");
	}

	[Fact]
	public void ExportDiagnosticData_ReturnJsonWithAllSections()
	{
		// Arrange - add history and anomalies
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-export");
		A.CallTo(() => context.CorrelationId).Returns("corr-export");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		_sut.TrackContextHistory(context, "Created", "For export");

		A.CallTo(() => _metrics.GetMetricsSummary())
			.Returns(new ContextMetricsSummary());

		// Act
		var json = _sut.ExportDiagnosticData("msg-export");

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("msg-export");
		json.ShouldContain("Histories");
		json.ShouldContain("RecentAnomalies");
	}

	[Fact]
	public void ExportDiagnosticData_WithoutMessageId_ReturnAllHistories()
	{
		// Arrange
		A.CallTo(() => _metrics.GetMetricsSummary())
			.Returns(new ContextMetricsSummary());

		// Act
		var json = _sut.ExportDiagnosticData();

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("Histories");
	}

	[Fact]
	public void DetectAnomalies_DetectInsufficientContext()
	{
		// Arrange - context with very few fields (less than 5 items)
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-few");
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.DeliveryCount).Returns(0);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Arrange - mock minimal fields so CountContextFields < 5
		A.CallTo(() => context.ExternalId).Returns(null);
		A.CallTo(() => context.UserId).Returns(null);
		A.CallTo(() => context.TraceParent).Returns(null);
		A.CallTo(() => context.TenantId).Returns(null);
		A.CallTo(() => context.Source).Returns(null);
		A.CallTo(() => context.MessageType).Returns(null);
		A.CallTo(() => context.ContentType).Returns(null);
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(default(DateTimeOffset));

		// Act
		var anomalies = _sut.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.InsufficientContext);
	}

	/// <summary>
	/// Concrete test implementation of IValidationResult since the interface has static abstract members
	/// that FakeItEasy cannot proxy.
	/// </summary>
	private sealed class TestValidationResult(bool isValid) : IValidationResult
	{
		public IReadOnlyCollection<object> Errors { get; } = isValid ? [] : ["Test validation error"];

		public bool IsValid { get; } = isValid;

		public static IValidationResult Failed(params object[] errors) =>
			new TestValidationResult(false);

		public static IValidationResult Success() =>
			new TestValidationResult(true);
	}
}
