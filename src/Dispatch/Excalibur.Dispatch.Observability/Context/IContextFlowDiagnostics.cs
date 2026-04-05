// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
	string VisualizeContextFlow(string messageId);

	/// <summary>
	/// Analyzes context health.
	/// </summary>
	/// <param name="context"> The message context to analyze. </param>
	/// <returns> A collection describing detected issues. </returns>
	IEnumerable<ContextDiagnosticIssue> AnalyzeContextHealth(IMessageContext context);

	/// <summary>
	/// Tracks context history.
	/// </summary>
	/// <param name="context"> The message context being tracked. </param>
	/// <param name="eventType"> The type of event recorded in the history. </param>
	/// <param name="details"> Optional details about the event. </param>
	void TrackContextHistory(IMessageContext context, string eventType, string? details = null);

	/// <summary>
	/// Detects anomalies in context flow.
	/// </summary>
	/// <param name="context"> The message context to inspect for anomalies. </param>
	/// <returns> A collection of detected anomalies, if any. </returns>
	IEnumerable<ContextAnomaly> DetectAnomalies(IMessageContext context);

	/// <summary>
	/// Gets context history for a message.
	/// </summary>
	/// <param name="messageId"> The identifier of the message whose history is requested. </param>
	/// <returns> The recorded history for the given message, if present. </returns>
	ContextHistory? GetContextHistory(string messageId);

}
