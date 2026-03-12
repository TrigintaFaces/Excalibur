// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// In-depth unit tests for <see cref="ContextFlowDiagnostics"/> covering uncovered code paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowDiagnosticsDepthShould : IDisposable
{
	private readonly IContextFlowTracker _fakeTracker = A.Fake<IContextFlowTracker>();
	private readonly IContextFlowMetrics _fakeMetrics = A.Fake<IContextFlowMetrics>();
	private ContextFlowDiagnostics? _diagnostics;

	public void Dispose() => _diagnostics?.Dispose();

	[Fact]
	public void VisualizeContextFlow_ShowsChanges_BetweenStages()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var now = DateTimeOffset.UtcNow;
		var snapshots = new List<ContextSnapshot>
		{
			new()
			{
				MessageId = "msg-1",
				Stage = "Stage1",
				Timestamp = now,
				Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					["MessageId"] = "msg-1",
					["CorrelationId"] = "corr-1",
				},
				FieldCount = 2,
				SizeBytes = 100,
				Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
			},
			new()
			{
				MessageId = "msg-1",
				Stage = "Stage2",
				Timestamp = now.AddMilliseconds(50),
				Fields = new Dictionary<string, object?>(StringComparer.Ordinal)
				{
					["MessageId"] = "msg-1",
					["TenantId"] = "tenant-1", // Added field
				},
				FieldCount = 2,
				SizeBytes = 120,
				Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
			},
		};
		A.CallTo(() => _fakeTracker.GetMessageSnapshots("msg-1")).Returns(snapshots);

		// Act
		var result = _diagnostics.VisualizeContextFlow("msg-1");

		// Assert
		result.ShouldContain("Stage1");
		result.ShouldContain("Stage2");
		result.ShouldContain("Changes from previous stage:");
		result.ShouldContain("Total Stages: 2");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsMissingRequiredFields()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Fields.RequiredContextFields = ["MessageId", "CorrelationId"];
		_diagnostics = CreateDiagnostics(options);

		var context = CreateFakeContext(messageId: "msg-1", correlationId: null);

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "MissingField" && i.Field == "CorrelationId");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsOversizedContext()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Limits.MaxContextSizeBytes = 10; // Very small limit
		_diagnostics = CreateDiagnostics(options);

		var context = CreateFakeContext(messageId: "msg-1", correlationId: "corr-1");
		context.Items["LargeData"] = new string('x', 100);

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "OversizedContext");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsValidationFailures()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		// ValidationResult() is an extension method that reads Items["__ValidationResult"]
		var validationResult = new SerializableValidationResult { IsValid = false, Errors = ["error1"] };
		var context = CreateFakeContext();
		context.Items["__ValidationResult"] = validationResult;

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "ValidationFailure");
	}

	[Fact]
	public void AnalyzeContextHealth_DetectsAuthorizationFailures()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		// AuthorizationResult() is an extension method that reads Items["__AuthorizationResult"]
		var authResult = AuthorizationResult.Failed("Denied");
		var context = CreateFakeContext();
		context.Items["__AuthorizationResult"] = authResult;

		// Act
		var issues = _diagnostics.AnalyzeContextHealth(context).ToList();

		// Assert
		issues.ShouldContain(i => i.Category == "AuthorizationFailure");
	}

	[Fact]
	public void DetectAnomalies_FindsInsufficientContext()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1", correlationId: null);
		// Ensure empty Items and Features so extension methods return null/0
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.InsufficientContext);
	}

	[Fact]
	public void DetectAnomalies_FindsExcessiveContext()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1", correlationId: "corr-1");

		// Create many items to push field count > 100
		var items = new Dictionary<string, object>();
		for (var i = 0; i < 120; i++)
		{
			items[$"key_{i}"] = $"value_{i}";
		}
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.ExcessiveContext);
	}

	[Fact]
	public void DetectAnomalies_FindsOversizedItems()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1");
		context.Items["big_item"] = new string('x', 20000); // > 10KB

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldContain(a => a.Type == AnomalyType.OversizedItem);
	}

	[Fact]
	public void TrackContextHistory_CreatesHistory_ForNewMessage()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-new", correlationId: "corr-new");

		// Act
		_diagnostics.TrackContextHistory(context, "Created");

		// Assert
		var history = _diagnostics.GetContextHistory("msg-new");
		history.ShouldNotBeNull();
		history.MessageId.ShouldBe("msg-new");
		history.CorrelationId.ShouldBe("corr-new");
	}

	[Fact]
	public void TrackContextHistory_LimitsHistorySize()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Limits.MaxHistoryEventsPerContext = 3;
		_diagnostics = CreateDiagnostics(options);
		var context = CreateFakeContext(messageId: "msg-1");

		// Act — add more events than the limit
		for (var i = 0; i < 5; i++)
		{
			_diagnostics.TrackContextHistory(context, $"Event{i}", $"Details{i}");
		}

		// Assert
		var history = _diagnostics.GetContextHistory("msg-1");
		history.ShouldNotBeNull();
		history.Events.Count.ShouldBeLessThanOrEqualTo(3);
	}

	[Fact]
	public void GetRecentAnomalies_ReturnsLimited()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1");
		context.Items["password_hash"] = "secret";
		context.Items["credit_card"] = "4111111111111111";
		context.Items["email_address"] = "test@test.com";

		// Act — trigger PII anomalies
		_ = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		var recent = _diagnostics.GetRecentAnomalies(2).ToList();
		recent.Count.ShouldBeLessThanOrEqualTo(2);
	}

	[Fact]
	public void GenerateCorrelationReport_IncludesServiceBoundaries()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-1",
			OriginMessageId = "msg-1",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [
				new ContextSnapshot
				{
					MessageId = "msg-1",
					Stage = "Stage1",
					Timestamp = DateTimeOffset.UtcNow,
					Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
					FieldCount = 5,
					SizeBytes = 200,
					Metadata = new Dictionary<string, object>(StringComparer.Ordinal),
				}
			],
			ServiceBoundaries = [
				new ServiceBoundaryTransition
				{
					ServiceName = "order-service",
					Timestamp = DateTimeOffset.UtcNow,
					ContextPreserved = true,
				},
				new ServiceBoundaryTransition
				{
					ServiceName = "payment-service",
					Timestamp = DateTimeOffset.UtcNow,
					ContextPreserved = false,
				},
			],
		};
		A.CallTo(() => _fakeTracker.GetContextLineage("corr-1")).Returns(lineage);

		// Act
		var result = _diagnostics.GenerateCorrelationReport("corr-1");

		// Assert
		result.ShouldContain("Service Boundaries:");
		result.ShouldContain("order-service");
		result.ShouldContain("Preserved");
		result.ShouldContain("payment-service");
		result.ShouldContain("Lost");
		result.ShouldContain("Preservation Rate:");
	}

	[Fact]
	public void ExportDiagnosticData_ReturnsJson()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1");
		_diagnostics.TrackContextHistory(context, "Created");
		A.CallTo(() => _fakeMetrics.GetMetricsSummary()).Returns(new ContextMetricsSummary());

		// Act
		var json = _diagnostics.ExportDiagnosticData("msg-1");

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("msg-1");
	}

	[Fact]
	public void ExportDiagnosticData_WithoutMessageId_ReturnsAllHistories()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		A.CallTo(() => _fakeMetrics.GetMetricsSummary()).Returns(new ContextMetricsSummary());

		// Act
		var json = _diagnostics.ExportDiagnosticData();

		// Assert
		json.ShouldNotBeNullOrWhiteSpace();
		json.ShouldContain("Timestamp");
	}

	[Fact]
	public void GetContextHistory_ReturnsNull_WhenNotTracked()
	{
		_diagnostics = CreateDiagnostics();
		var result = _diagnostics.GetContextHistory("nonexistent");
		result.ShouldBeNull();
	}

	[Fact]
	public void DetectAnomalies_NoPII_DoesNotFlagClean()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1", correlationId: "corr-1", causationId: "cause-1");
		// Set enough fields via features to avoid InsufficientContext
		context.GetOrCreateIdentityFeature().TenantId = "tenant-1";
		context.GetOrCreateIdentityFeature().UserId = "user-1";
		context.SetMessageType("OrderCreated");
		context.Items["safe_key1"] = "value1";
		context.Items["safe_key2"] = "value2";

		// Act
		var anomalies = _diagnostics.DetectAnomalies(context).ToList();

		// Assert
		anomalies.ShouldNotContain(a => a.Type == AnomalyType.PotentialPII);
	}

	[Fact]
	public void TrackContextHistory_IncludesThreadId()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: "msg-1");

		// Act
		_diagnostics.TrackContextHistory(context, "Processing", "stage details");

		// Assert
		var history = _diagnostics.GetContextHistory("msg-1");
		history.ShouldNotBeNull();
		history.Events[0].ThreadId.ShouldBeGreaterThan(0);
		history.Events[0].Details.ShouldBe("stage details");
	}

	[Fact]
	public void TrackContextHistory_HandlesNullMessageId()
	{
		// Arrange
		_diagnostics = CreateDiagnostics();
		var context = CreateFakeContext(messageId: null);

		// Act — should not throw, uses generated ID
		_diagnostics.TrackContextHistory(context, "Event");

		// No assert on specific ID since it's a GUID
	}

	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> with real Items and Features dictionaries
	/// so that extension methods work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext(
		string? messageId = "msg-default",
		string? correlationId = "corr-default",
		string? causationId = null)
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		var features = new Dictionary<Type, object>();

		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.CausationId).Returns(causationId);
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.Features).Returns(features);

		return context;
	}

	private ContextFlowDiagnostics CreateDiagnostics(ContextObservabilityOptions? options = null)
	{
		return new ContextFlowDiagnostics(
			NullLogger<ContextFlowDiagnostics>.Instance,
			_fakeTracker,
			_fakeMetrics,
			MsOptions.Create(options ?? new ContextObservabilityOptions()));
	}
}

#pragma warning restore IL2026, IL3050
