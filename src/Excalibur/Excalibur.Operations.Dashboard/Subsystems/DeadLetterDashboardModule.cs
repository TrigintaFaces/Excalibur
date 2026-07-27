// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.ErrorHandling;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>A read-only summary of the dead-letter queue: whether it is configured and its entry count.</summary>
public sealed class DeadLetterView
{
	/// <summary>Gets a value indicating whether a dead-letter queue is configured in the host.</summary>
	/// <value><see langword="true"/> when the dead-letter queue is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the number of entries currently in the dead-letter queue.</summary>
	/// <value>The dead-letter entry count.</value>
	public long Count { get; init; }

	/// <summary>Gets the UTC instant this summary was captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>A read-only projection of a single dead-letter entry (payload excluded).</summary>
public sealed class DeadLetterEntryView
{
	/// <summary>Gets the unique identifier of the dead-letter entry.</summary>
	/// <value>The entry id.</value>
	public Guid Id { get; init; }

	/// <summary>Gets the type name of the original message.</summary>
	/// <value>The message type name.</value>
	public string MessageType { get; init; } = string.Empty;

	/// <summary>Gets the reason the message was dead-lettered.</summary>
	/// <value>The dead-letter reason name.</value>
	public string Reason { get; init; } = string.Empty;

	/// <summary>Gets the exception message, if any.</summary>
	/// <value>The exception message, or <see langword="null"/>.</value>
	public string? ExceptionMessage { get; init; }

	/// <summary>Gets the instant the entry was enqueued.</summary>
	/// <value>The enqueue timestamp.</value>
	public DateTimeOffset EnqueuedAt { get; init; }

	/// <summary>Gets the number of processing attempts before dead-lettering.</summary>
	/// <value>The original attempt count.</value>
	public int OriginalAttempts { get; init; }

	/// <summary>Gets the correlation id, if any.</summary>
	/// <value>The correlation id, or <see langword="null"/>.</value>
	public string? CorrelationId { get; init; }

	/// <summary>Gets the source transport or queue, if known.</summary>
	/// <value>The source queue, or <see langword="null"/>.</value>
	public string? SourceQueue { get; init; }

	/// <summary>Gets a value indicating whether the entry has been replayed.</summary>
	/// <value><see langword="true"/> when replayed; otherwise <see langword="false"/>.</value>
	public bool IsReplayed { get; init; }

	/// <summary>Gets the instant the entry was replayed, if applicable.</summary>
	/// <value>The replay timestamp, or <see langword="null"/>.</value>
	public DateTimeOffset? ReplayedAt { get; init; }
}

/// <summary>
/// Maps the dead-letter read endpoints. Fails open: when no dead-letter queue is configured the
/// endpoints return not-configured/empty payloads rather than a 500.
/// </summary>
internal sealed class DeadLetterDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "dlq";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(options);

		var defaultPageSize = options.DefaultPageSize;
		var maxPageSize = options.MaxPageSize;
		var exposeSensitive = options.ExposeSensitiveData;

		group.MapGet("/dlq", static async (IDeadLetterQueue? dlq, CancellationToken ct) =>
		{
			if (dlq is null)
			{
				return Results.Json(
					new DeadLetterView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					DeadLetterJsonContext.Default.DeadLetterView);
			}

			var count = await dlq.GetCountAsync(ct).ConfigureAwait(false);
			return Results.Json(
				new DeadLetterView { Configured = true, Count = count, CapturedAt = DateTimeOffset.UtcNow },
				DeadLetterJsonContext.Default.DeadLetterView);
		});

		group.MapGet("/dlq/entries", async (IDeadLetterQueue? dlq, int? skip, int? limit, CancellationToken ct) =>
		{
			if (dlq is null)
			{
				return Results.Json(
					Array.Empty<DeadLetterEntryView>(),
					DeadLetterJsonContext.Default.DeadLetterEntryViewArray);
			}

			var pageSize = Math.Clamp(limit ?? defaultPageSize, 1, maxPageSize);
			var filter = new DeadLetterQueryFilter { Skip = Math.Max(0, skip ?? 0) };
			var entries = await dlq.GetEntriesAsync(ct, filter, pageSize).ConfigureAwait(false);

			// Redact potentially sensitive fields (exception message may quote failed-message content;
			// correlation id can be a personal identifier) unless the host has opted in via
			// DashboardOptions.ExposeSensitiveData behind an authorized boundary. Redacted fields serialize
			// to null and are omitted (DefaultIgnoreCondition = WhenWritingNull).
			var views = entries.Select(e => new DeadLetterEntryView
			{
				Id = e.Id,
				MessageType = e.MessageType,
				Reason = e.Reason.ToString(),
				ExceptionMessage = exposeSensitive ? e.ExceptionMessage : null,
				EnqueuedAt = e.EnqueuedAt,
				OriginalAttempts = e.OriginalAttempts,
				CorrelationId = exposeSensitive ? e.CorrelationId : null,
				SourceQueue = e.SourceQueue,
				IsReplayed = e.IsReplayed,
				ReplayedAt = e.ReplayedAt,
			}).ToArray();

			return Results.Json(views, DeadLetterJsonContext.Default.DeadLetterEntryViewArray);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeadLetterView))]
[JsonSerializable(typeof(DeadLetterEntryView[]))]
internal sealed partial class DeadLetterJsonContext : JsonSerializerContext;
