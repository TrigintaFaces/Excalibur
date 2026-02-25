// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Interface for context trace enrichment operations.
/// </summary>
public interface IContextTraceEnricher
{
	/// <summary>
	/// Enriches an activity with context information.
	/// </summary>
	/// <param name="activity"> The activity to enrich. </param>
	/// <param name="context"> The message context providing enrichment data. </param>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	void EnrichActivity(Activity? activity, IMessageContext context);

	/// <summary>
	/// Creates a span for a context operation.
	/// </summary>
	/// <param name="operationName"> The name of the operation represented by the span. </param>
	/// <param name="context"> The message context associated with the span. </param>
	/// <param name="kind"> The span kind to create. </param>
	/// <returns> The created activity, if any. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	Activity? CreateContextOperationSpan(string operationName, IMessageContext context, ActivityKind kind = ActivityKind.Internal);

	/// <summary>
	/// Links related traces using correlation IDs.
	/// </summary>
	/// <param name="activity"> The activity to link. </param>
	/// <param name="correlationId"> The correlation identifier to link with. </param>
	/// <param name="linkType"> The type of link being recorded. </param>
	void LinkRelatedTrace(Activity? activity, string correlationId, string linkType = "correlation");

	/// <summary>
	/// Propagates context as baggage.
	/// </summary>
	/// <param name="context"> The message context to propagate. </param>
	/// <param name="carrier"> The carrier receiving the propagated values. </param>
	void PropagateContextAsBaggage(IMessageContext context, IDictionary<string, string> carrier);

	/// <summary>
	/// Extracts context from baggage.
	/// </summary>
	/// <param name="carrier"> The carrier containing propagated values. </param>
	/// <param name="context"> The message context to populate. </param>
	void ExtractContextFromBaggage(IDictionary<string, string> carrier, IMessageContext context);

	/// <summary>
	/// Adds a context event to the current span.
	/// </summary>
	/// <param name="eventName"> The name of the event to add. </param>
	/// <param name="context"> The message context providing event data. </param>
	/// <param name="attributes"> Optional attributes to attach to the event. </param>
	void AddContextEvent(string eventName, IMessageContext context, IReadOnlyDictionary<string, object>? attributes = null);
}
