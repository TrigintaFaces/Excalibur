// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability;

/// <summary>
/// JSON serializer context for Excalibur.Dispatch.Observability types, enabling AOT-compatible serialization.
/// </summary>
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ContextSnapshot))]
[JsonSerializable(typeof(ContextMetricsSummary))]
[JsonSerializable(typeof(ContextObservabilityOptions))]
[JsonSerializable(typeof(ContextTracingOptions))]
[JsonSerializable(typeof(ContextLimitsOptions))]
[JsonSerializable(typeof(ContextFieldOptions))]
[JsonSerializable(typeof(ContextExportOptions))]
[JsonSerializable(typeof(ContextAnomaly))]
[JsonSerializable(typeof(ContextChange))]
[JsonSerializable(typeof(ContextDiagnosticIssue))]
[JsonSerializable(typeof(ContextHistory))]
[JsonSerializable(typeof(ContextHistoryEvent))]
[JsonSerializable(typeof(ContextLineage))]
[JsonSerializable(typeof(ServiceBoundaryTransition))]
[JsonSerializable(typeof(ObservabilityOptions))]
[JsonSerializable(typeof(AnomalyType))]
[JsonSerializable(typeof(AnomalySeverity))]
[JsonSerializable(typeof(ContextChangeType))]
[JsonSerializable(typeof(DiagnosticSeverity))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, object>))]
public partial class ObservabilityJsonSerializerContext : JsonSerializerContext
{
}
