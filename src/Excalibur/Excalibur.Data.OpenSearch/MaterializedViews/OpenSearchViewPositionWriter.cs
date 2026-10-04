// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

using OpenSearch.Client;
using OpenSearch.Net;

namespace Excalibur.Data.OpenSearch.MaterializedViews;

/// <summary>
/// Writes a materialized view's position checkpoint, enforcing the monotonic advance server-side.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a separate type because the store was over-coupled, and the analyzer was right about it.</b>
/// <c>OpenSearchMaterializedViewStore</c> handles view documents AND position checkpoints, and adding the
/// scripted advance pushed its type coupling past the project's ceiling. Shaving a type or two off the
/// call site would have cleared the number without addressing what the number was reporting; writing the
/// checkpoint is its own concern and now lives in its own type.
/// </para>
/// <para>
/// <b>Why a script rather than a document version.</b> The position used to BE the external document
/// version, which refuses a lower value — elegant, and wrong twice over. A projection checkpoint must be
/// resettable, so refusing a lower value makes a legitimate reset impossible; and a delete does not
/// forget a version, it increments it and retains a tombstone, so a low write stayed refused even after
/// the document was removed. Measured on real OpenSearch 2.16 and real Elasticsearch: <c>"current version
/// [4000002] is higher than the one provided [1]"</c>. External versioning exists to mirror a monotonic
/// version owned by an external system; a resettable checkpoint is not that, and the mismatch was the root
/// cause of both defects.
/// </para>
/// </remarks>
internal static class OpenSearchViewPositionWriter
{
	/// <summary>
	/// The monotonic position advance, evaluated server-side so concurrent writers need no coordination.
	/// </summary>
	/// <remarks>
	/// <b>These are the SERIALIZED field names, not the C# property names.</b> The client infers camelCase
	/// member names by default, so the document members are <c>position</c>, <c>viewName</c> and so on. A
	/// script written against the PascalCase property names compiles, runs, and silently compares
	/// <see langword="null"/> — the mistake a first draft of this made, which then passed a probe that had
	/// written its own documents with the wrong casing.
	/// </remarks>
	private const string AdvanceScript =
		"if (ctx.op == 'create') {"
		+ " ctx._source.position = params.pos;"
		+ " ctx._source.viewName = params.view;"
		+ " ctx._source.tenantId = params.tenant;"
		+ " ctx._source.createdAt = params.now;"
		+ " ctx._source.updatedAt = params.now;"
		+ " } else if (params.pos > ctx._source.position) {"
		+ " ctx._source.position = params.pos;"
		+ " ctx._source.updatedAt = params.now;"
		+ " } else { ctx.op = 'noop'; }";

	/// <summary>
	/// Advances the stored checkpoint to <paramref name="position" />, or reports that the advance was
	/// refused because the stored value is already at or beyond it.
	/// </summary>
	/// <param name="client">The OpenSearch client.</param>
	/// <param name="indexName">The positions index.</param>
	/// <param name="documentId">The position document identifier.</param>
	/// <param name="viewName">The view whose checkpoint is advancing.</param>
	/// <param name="tenantId">The owning tenant, recorded on the document for operator visibility.</param>
	/// <param name="position">The position to advance to.</param>
	/// <param name="refresh">The refresh policy to apply.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// <see cref="ViewPositionSaveOutcome.Advanced" /> when the checkpoint moved, or
	/// <see cref="ViewPositionSaveOutcome.RefusedAsStale" /> when the script declined to move it.
	/// </returns>
	/// <exception cref="InvalidOperationException">The write failed for any reason other than staleness.</exception>
	internal static async ValueTask<ViewPositionSaveOutcome> AdvanceAsync(
		IOpenSearchClient client,
		string indexName,
		string documentId,
		string viewName,
		string tenantId,
		long position,
		Refresh refresh,
		CancellationToken cancellationToken)
	{
		var request = new UpdateRequest<OpenSearchMaterializedViewStore.MaterializedViewPositionDocument, OpenSearchMaterializedViewStore.MaterializedViewPositionDocument>(
			indexName,
			documentId)
		{
			ScriptedUpsert = true,
			Upsert = new OpenSearchMaterializedViewStore.MaterializedViewPositionDocument(),
			Refresh = refresh,
			Script = new InlineScript(AdvanceScript)
			{
				Params = new Dictionary<string, object>(StringComparer.Ordinal)
				{
					["pos"] = position,
					["view"] = viewName,
					["tenant"] = tenantId,
					["now"] = DateTimeOffset.UtcNow,
				},
			},
		};

		var response = await client.UpdateAsync(request, cancellationToken).ConfigureAwait(false);

		if (!response.IsValid)
		{
			throw new InvalidOperationException(
				$"Failed to save position for {viewName}: {response.DebugInformation}");
		}

		// Painless reports `noop` when the script declines to change the document, which is exactly a
		// refused advance rather than a failure. The response therefore carries the outcome and no second
		// round trip is needed to discover it.
		return response.Result == Result.Noop
			? ViewPositionSaveOutcome.RefusedAsStale
			: ViewPositionSaveOutcome.Advanced;
	}
}
