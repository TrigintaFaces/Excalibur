// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Observability.Context;

/// <summary>
/// Interface for context flow tracking operations.
/// </summary>
public interface IContextFlowTracker
{
	/// <summary>
	/// Records the current state of a message context.
	/// </summary>
	/// <param name="context"> The context being recorded. </param>
	/// <param name="stage"> The pipeline stage identifier. </param>
	/// <param name="metadata"> Optional metadata associated with the snapshot. </param>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	void RecordContextState(IMessageContext context, string stage, IReadOnlyDictionary<string, object>? metadata = null);

	/// <summary>
	/// Detects changes between pipeline stages.
	/// </summary>
	/// <param name="context"> The context to inspect. </param>
	/// <param name="fromStage"> The originating stage. </param>
	/// <param name="toStage"> The destination stage. </param>
	/// <returns> A collection describing detected changes. </returns>
	[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
		"Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	IEnumerable<ContextChange> DetectChanges(IMessageContext context, string fromStage, string toStage);

	/// <summary>
	/// Correlates context across service boundaries.
	/// </summary>
	/// <param name="context"> The context being correlated. </param>
	/// <param name="serviceBoundary"> The boundary the context traversed. </param>
	void CorrelateAcrossBoundary(IMessageContext context, string serviceBoundary);

	/// <summary>
	/// Gets the complete lineage of a context.
	/// </summary>
	/// <param name="correlationId"> The correlation identifier to retrieve. </param>
	/// <returns> The context lineage or <see langword="null" /> if not found. </returns>
	ContextLineage? GetContextLineage(string correlationId);

	/// <summary>
	/// Gets all snapshots for a specific message.
	/// </summary>
	/// <param name="messageId"> The identifier of the message to inspect. </param>
	/// <returns> A collection of recorded snapshots. </returns>
	IEnumerable<ContextSnapshot> GetMessageSnapshots(string messageId);

	/// <summary>
	/// Validates context integrity.
	/// </summary>
	/// <param name="context"> The context to validate. </param>
	/// <returns> <see langword="true" /> when the context passes validation; otherwise <see langword="false" />. </returns>
	bool ValidateContextIntegrity(IMessageContext context);
}
