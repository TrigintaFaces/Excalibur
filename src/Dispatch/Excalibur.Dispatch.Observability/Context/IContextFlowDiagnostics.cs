// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Interface for context flow diagnostics.
/// </summary>
public interface IContextFlowDiagnostics
{
	/// <summary>
	/// Visualizes context flow for a message.
	/// </summary>
	/// <param name="messageId"> The identifier of the message to visualize. </param>
	/// <returns> A textual representation of the context flow. </returns>
	[RequiresDynamicCode("Calls CompareSnapshots which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls CompareSnapshots which uses JSON serialization that cannot be statically analyzed")]
	string VisualizeContextFlow(string messageId);

	/// <summary>
	/// Analyzes context health.
	/// </summary>
	/// <param name="context"> The message context to analyze. </param>
	/// <returns> A collection describing detected issues. </returns>
	[RequiresDynamicCode("Calls EstimateContextSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateContextSize which uses JSON serialization that cannot be statically analyzed")]
	IEnumerable<ContextDiagnosticIssue> AnalyzeContextHealth(IMessageContext context);

	/// <summary>
	/// Tracks context history.
	/// </summary>
	/// <param name="context"> The message context being tracked. </param>
	/// <param name="eventType"> The type of event recorded in the history. </param>
	/// <param name="details"> Optional details about the event. </param>
	[RequiresDynamicCode("Calls EstimateContextSize which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls EstimateContextSize which uses JSON serialization that cannot be statically analyzed")]
	void TrackContextHistory(IMessageContext context, string eventType, string? details = null);

	/// <summary>
	/// Detects anomalies in context flow.
	/// </summary>
	/// <param name="context"> The message context to inspect for anomalies. </param>
	/// <returns> A collection of detected anomalies, if any. </returns>
	[RequiresDynamicCode("Calls CheckOversizedItem which uses JSON serialization requiring dynamic code")]
	[RequiresUnreferencedCode("Calls CheckOversizedItem which uses JSON serialization that cannot be statically analyzed")]
	IEnumerable<ContextAnomaly> DetectAnomalies(IMessageContext context);

	/// <summary>
	/// Gets context history for a message.
	/// </summary>
	/// <param name="messageId"> The identifier of the message whose history is requested. </param>
	/// <returns> The recorded history for the given message, if present. </returns>
	ContextHistory? GetContextHistory(string messageId);

	/// <summary>
	/// Gets recent anomalies.
	/// </summary>
	/// <param name="limit"> The maximum number of anomalies to return. </param>
	/// <returns> A collection containing the most recent anomalies. </returns>
	IEnumerable<ContextAnomaly> GetRecentAnomalies(int limit = 100);

	/// <summary>
	/// Generates a correlation report.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier to report on. </param>
	/// <returns> The correlation report text. </returns>
	string GenerateCorrelationReport(string correlationId);

	/// <summary>
	/// Exports diagnostic data.
	/// </summary>
	/// <param name="messageId"> Optional message identifier to scope the export. </param>
	/// <returns> The exported diagnostic payload. </returns>
	[RequiresDynamicCode(
		"JSON serialization for diagnostic data requires dynamic code generation for property reflection and value conversion.")]
	[RequiresUnreferencedCode(
		"JSON serialization may reference types not preserved during trimming. Ensure options types are annotated with DynamicallyAccessedMembers.")]
	string ExportDiagnosticData(string? messageId = null);
}
