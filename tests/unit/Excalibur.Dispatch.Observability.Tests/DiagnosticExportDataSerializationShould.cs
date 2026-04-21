// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Round-trip serialization tests for <see cref="DiagnosticExportData"/> via
/// <see cref="ObservabilityJsonSerializerContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Serialization")]
public sealed class DiagnosticExportDataSerializationShould
{
    private static readonly JsonSerializerOptions ContextOptions =
        ObservabilityJsonSerializerContext.Default.Options;

    [Fact]
    public void HaveDiagnosticExportDataTypeInfo()
    {
        ObservabilityJsonSerializerContext.Default
            .GetTypeInfo(typeof(DiagnosticExportData))
            .ShouldNotBeNull();
    }

    [Fact]
    public void RoundTripFullyPopulatedInstance()
    {
        var original = CreateFullInstance();

        var json = JsonSerializer.Serialize(original, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        deserialized.Timestamp.ShouldBe(original.Timestamp);
        deserialized.MessageId.ShouldBe(original.MessageId);
        deserialized.Histories.Length.ShouldBe(original.Histories.Length);
        deserialized.Histories[0].MessageId.ShouldBe("hist-msg-1");
        deserialized.RecentAnomalies.Length.ShouldBe(original.RecentAnomalies.Length);
        deserialized.RecentAnomalies[0].Type.ShouldBe(AnomalyType.MissingCorrelation);
        deserialized.MetricsSummary.ShouldNotBeNull();
        deserialized.MetricsSummary!.TotalContextsProcessed.ShouldBe(100L);
    }

    [Fact]
    public void ProduceCamelCasePropertyNames()
    {
        var data = new DiagnosticExportData
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessageId = "test-1",
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        json.ShouldContain("\"timestamp\"");
        json.ShouldContain("\"messageId\"");
        json.ShouldContain("\"histories\"");
        json.ShouldContain("\"recentAnomalies\"");
    }

    [Fact]
    public void OmitNullMetricsSummaryWhenWritingNull()
    {
        var data = new DiagnosticExportData
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessageId = "test-2",
            MetricsSummary = null,
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        json.ShouldNotContain("\"metricsSummary\"");
    }

    [Fact]
    public void OmitNullMessageId()
    {
        var data = new DiagnosticExportData
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessageId = null,
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        json.ShouldNotContain("\"messageId\"");
    }

    [Fact]
    public void RoundTripWithEmptyArrays()
    {
        var data = new DiagnosticExportData
        {
            Timestamp = DateTimeOffset.UtcNow,
            MessageId = "empty-test",
            Histories = [],
            RecentAnomalies = [],
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        deserialized.Histories.ShouldBeEmpty();
        deserialized.RecentAnomalies.ShouldBeEmpty();
    }

    [Fact]
    public void RoundTripWithDefaultValues()
    {
        var data = new DiagnosticExportData();

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        deserialized.Timestamp.ShouldBe(default);
        deserialized.MessageId.ShouldBeNull();
        deserialized.Histories.ShouldBeEmpty();
        deserialized.RecentAnomalies.ShouldBeEmpty();
        deserialized.MetricsSummary.ShouldBeNull();
    }

    [Fact]
    public void PreserveNestedContextHistoryProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var data = new DiagnosticExportData
        {
            Timestamp = now,
            Histories =
            [
                new ContextHistory
                {
                    MessageId = "msg-1",
                    CorrelationId = "corr-1",
                    StartTime = now.AddMinutes(-5),
                    Events = new List<ContextHistoryEvent>
                    {
                        new()
                        {
                            EventType = "Created",
                            Timestamp = now.AddMinutes(-5),
                            Details = "Initial creation",
                            Stage = "Dispatch",
                            ThreadId = 42,
                            FieldCount = 3,
                            SizeBytes = 256,
                        },
                    },
                },
            ],
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        var history = deserialized.Histories[0];
        history.MessageId.ShouldBe("msg-1");
        history.CorrelationId.ShouldBe("corr-1");
        history.Events.Count.ShouldBe(1);
        history.Events[0].EventType.ShouldBe("Created");
        history.Events[0].ThreadId.ShouldBe(42);
        history.Events[0].SizeBytes.ShouldBe(256);
    }

    [Fact]
    public void PreserveNestedContextAnomalyProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var data = new DiagnosticExportData
        {
            Timestamp = now,
            RecentAnomalies =
            [
                new ContextAnomaly
                {
                    Type = AnomalyType.PotentialPII,
                    Severity = AnomalySeverity.High,
                    Description = "PII detected in context",
                    MessageId = "pii-msg",
                    DetectedAt = now,
                    SuggestedAction = "Remove PII fields",
                },
            ],
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        var anomaly = deserialized.RecentAnomalies[0];
        anomaly.Type.ShouldBe(AnomalyType.PotentialPII);
        anomaly.Severity.ShouldBe(AnomalySeverity.High);
        anomaly.Description.ShouldBe("PII detected in context");
        anomaly.SuggestedAction.ShouldBe("Remove PII fields");
    }

    [Fact]
    public void PreserveMetricsSummaryValues()
    {
        var now = DateTimeOffset.UtcNow;
        var data = new DiagnosticExportData
        {
            Timestamp = now,
            MetricsSummary = new ContextMetricsSummary
            {
                TotalContextsProcessed = 500,
                ContextsPreservedSuccessfully = 495,
                PreservationRate = 0.99,
                ActiveContexts = 10,
                MaxLineageDepth = 7,
                Timestamp = now,
            },
        };

        var json = JsonSerializer.Serialize(data, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);
        var deserialized = JsonSerializer.Deserialize(json, ObservabilityJsonSerializerContext.Default.DiagnosticExportData);

        deserialized.ShouldNotBeNull();
        deserialized.MetricsSummary.ShouldNotBeNull();
        deserialized.MetricsSummary!.TotalContextsProcessed.ShouldBe(500L);
        deserialized.MetricsSummary.PreservationRate.ShouldBe(0.99);
        deserialized.MetricsSummary.ActiveContexts.ShouldBe(10L);
        deserialized.MetricsSummary.MaxLineageDepth.ShouldBe(7L);
    }

    private static DiagnosticExportData CreateFullInstance()
    {
        var now = DateTimeOffset.UtcNow;
        return new DiagnosticExportData
        {
            Timestamp = now,
            MessageId = "test-msg-001",
            Histories =
            [
                new ContextHistory
                {
                    MessageId = "hist-msg-1",
                    CorrelationId = "corr-1",
                    StartTime = now.AddMinutes(-10),
                    Events = new List<ContextHistoryEvent>
                    {
                        new()
                        {
                            EventType = "Created",
                            Timestamp = now.AddMinutes(-10),
                            Details = "Context created",
                            Stage = "Dispatch",
                            ThreadId = 1,
                            FieldCount = 5,
                            SizeBytes = 128,
                        },
                    },
                },
            ],
            RecentAnomalies =
            [
                new ContextAnomaly
                {
                    Type = AnomalyType.MissingCorrelation,
                    Severity = AnomalySeverity.Medium,
                    Description = "No correlation ID",
                    MessageId = "anom-msg-1",
                    DetectedAt = now.AddMinutes(-5),
                },
            ],
            MetricsSummary = new ContextMetricsSummary
            {
                TotalContextsProcessed = 100,
                ContextsPreservedSuccessfully = 98,
                PreservationRate = 0.98,
                ActiveContexts = 5,
                MaxLineageDepth = 3,
                Timestamp = now,
            },
        };
    }
}
