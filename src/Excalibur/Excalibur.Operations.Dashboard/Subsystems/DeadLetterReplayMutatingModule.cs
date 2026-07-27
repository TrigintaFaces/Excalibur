// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.ErrorHandling;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>The result of a dead-letter replay request.</summary>
public sealed class DeadLetterReplayResult
{
	/// <summary>Gets a value indicating whether the replay was performed.</summary>
	/// <value><see langword="true"/> when the entry was replayed; otherwise <see langword="false"/>.</value>
	public bool Replayed { get; init; }

	/// <summary>Gets the number of entries replayed (for batch replay).</summary>
	/// <value>The replayed entry count.</value>
	public int Count { get; init; }

	/// <summary>Gets the number of entries the filter selected for this batch.</summary>
	/// <value>The enumerated entry count.</value>
	/// <remarks>
	/// A value above <see cref="Count"/> means some selected entries were not replayed, which a single
	/// count cannot express.
	/// </remarks>
	public int Enumerated { get; init; }

	/// <summary>Gets a value indicating whether the batch limit cut the selection short.</summary>
	/// <value>
	/// <see langword="true"/> when further entries may match the filter and the batch is therefore
	/// incomplete; otherwise <see langword="false"/>.
	/// </value>
	/// <remarks>
	/// When <see langword="true"/>, re-run the request until it reports <see langword="false"/>. Without
	/// this flag a partial batch is indistinguishable from a drained queue.
	/// </remarks>
	public bool Truncated { get; init; }
}

/// <summary>
/// Maps the dead-letter replay mutating endpoints over the existing <see cref="IDeadLetterQueue"/> and
/// <see cref="IDeadLetterQueueAdmin"/> replay operations. Only mapped when mutating actions are enabled,
/// and the group is auth-gated by the host. Fails open (404-style empty result) when no dead-letter
/// queue is configured.
/// </summary>
internal sealed class DeadLetterReplayMutatingModule : IDashboardMutatingModule
{
	/// <summary>
	/// The batch size used when a request does not state one. Applied here, at the endpoint, rather than
	/// inside the store: the store takes the limit as a parameter so no caller inherits a hidden cap.
	/// </summary>
	private const int DefaultReplayBatchLimit = 1000;

	/// <inheritdoc />
	public string Subsystem => "dlq";

	/// <inheritdoc />
	public void MapMutatingEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);

		group.MapPost("/dlq/{id:guid}/replay", static async (Guid id, IDeadLetterQueue? dlq, CancellationToken ct) =>
		{
			if (dlq is null)
			{
				return Results.NotFound();
			}

			var replayed = await dlq.ReplayAsync(id, ct).ConfigureAwait(false);
			return replayed
				? Results.Json(new DeadLetterReplayResult { Replayed = true, Count = 1 }, DeadLetterReplayJsonContext.Default.DeadLetterReplayResult)
				: Results.NotFound();
		});

		group.MapPost("/dlq/replay-batch", static async (
			IDeadLetterQueueAdmin? admin,
			DeadLetterReplayBatchRequest? request,
			CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.NotFound();
			}

			var filter = new DeadLetterQueryFilter
			{
				MessageType = request?.MessageType,
				SourceQueue = request?.SourceQueue,
				IsReplayed = false,
			};

			var limit = request?.Limit ?? DefaultReplayBatchLimit;
			var result = await admin.ReplayBatchAsync(filter, limit, ct).ConfigureAwait(false);

			// Truncated is surfaced to the operator, not swallowed here. Closing the silent-truncation gap
			// in the store and then dropping the signal at the HTTP boundary would leave the operator
			// exactly where they started: reading a plausible count off an incomplete job.
			return Results.Json(
				new DeadLetterReplayResult
				{
					Replayed = result.Replayed > 0,
					Count = result.Replayed,
					Enumerated = result.Enumerated,
					Truncated = result.Truncated,
				},
				DeadLetterReplayJsonContext.Default.DeadLetterReplayResult);
		});
	}
}

/// <summary>Request body for batch dead-letter replay, narrowing which entries are replayed.</summary>
public sealed class DeadLetterReplayBatchRequest
{
	/// <summary>Gets the message type to replay, or <see langword="null"/> for any.</summary>
	/// <value>The message type filter, or <see langword="null"/>.</value>
	public string? MessageType { get; init; }

	/// <summary>Gets the source queue to replay, or <see langword="null"/> for any.</summary>
	/// <value>The source queue filter, or <see langword="null"/>.</value>
	public string? SourceQueue { get; init; }

	/// <summary>Gets the maximum number of entries this call may replay.</summary>
	/// <value>The batch limit, or <see langword="null"/> to use the endpoint default.</value>
	/// <remarks>
	/// When the limit is reached the response reports <c>truncated</c> and the caller re-runs to drain the
	/// remainder; a batch is never silently partial.
	/// </remarks>
	public int? Limit { get; init; }
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeadLetterReplayResult))]
[JsonSerializable(typeof(DeadLetterReplayBatchRequest))]
internal sealed partial class DeadLetterReplayJsonContext : JsonSerializerContext;
