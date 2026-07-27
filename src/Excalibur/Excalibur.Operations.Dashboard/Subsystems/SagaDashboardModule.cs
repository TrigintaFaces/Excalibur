// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>A read-only view of the saga store: running/completed/total counts and whether it is queryable.</summary>
public sealed class SagaView
{
	/// <summary>Gets a value indicating whether a queryable saga admin store is configured in the host.</summary>
	/// <value><see langword="true"/> when the saga admin store is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the number of running saga instances.</summary>
	/// <value>The running saga count.</value>
	public int Running { get; init; }

	/// <summary>Gets the number of completed saga instances retained in the store.</summary>
	/// <value>The completed saga count.</value>
	public int Completed { get; init; }

	/// <summary>Gets the total number of saga instances.</summary>
	/// <value>The total saga count.</value>
	public int Total { get; init; }

	/// <summary>Gets the UTC instant these statistics were captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>A read-only projection of a saga instance for dashboard listing.</summary>
public sealed class SagaInstanceView
{
	/// <summary>Gets the saga instance identifier.</summary>
	/// <value>The saga id.</value>
	public Guid SagaId { get; init; }

	/// <summary>Gets the saga type name.</summary>
	/// <value>The saga type name.</value>
	public string SagaType { get; init; } = string.Empty;

	/// <summary>Gets a value indicating whether the saga has completed.</summary>
	/// <value><see langword="true"/> when completed; otherwise <see langword="false"/>.</value>
	public bool IsCompleted { get; init; }

	/// <summary>Gets the completion instant, or <see langword="null"/> while running.</summary>
	/// <value>The completion timestamp, or <see langword="null"/>.</value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>Gets the owning tenant, or <see langword="null"/> when not tenant-scoped.</summary>
	/// <value>The tenant id, or <see langword="null"/>.</value>
	public string? TenantId { get; init; }

	/// <summary>Gets the optimistic-concurrency version.</summary>
	/// <value>The saga state version.</value>
	public long Version { get; init; }
}

/// <summary>
/// Maps the saga read endpoints over <see cref="ISagaStoreAdmin"/>. Fails open: when no queryable saga
/// admin store is configured the endpoints return not-configured/empty payloads rather than a 500.
/// </summary>
internal sealed class SagaDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "saga";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(options);

		var defaultPageSize = options.DefaultPageSize;
		var maxPageSize = options.MaxPageSize;
		var exposeSensitive = options.ExposeSensitiveData;

		group.MapGet("/saga", static async (ISagaStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(
					new SagaView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					SagaJsonContext.Default.SagaView);
			}

			var stats = await admin.GetStatisticsAsync(ct).ConfigureAwait(false);
			return Results.Json(
				new SagaView
				{
					Configured = true,
					Running = stats.RunningCount,
					Completed = stats.CompletedCount,
					Total = stats.TotalCount,
					CapturedAt = stats.CapturedAt,
				},
				SagaJsonContext.Default.SagaView);
		});

		group.MapGet("/saga/instances", async (
			ISagaStoreAdmin? admin,
			bool? completed,
			string? tenant,
			int? skip,
			int? limit,
			CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(Array.Empty<SagaInstanceView>(), SagaJsonContext.Default.SagaInstanceViewArray);
			}

			var filter = new SagaQueryFilter
			{
				IsCompleted = completed,
				TenantId = tenant,
				Skip = Math.Max(0, skip ?? 0),
				MaxResults = Math.Clamp(limit ?? defaultPageSize, 1, maxPageSize),
			};

			var summaries = await admin.QuerySagasAsync(filter, ct).ConfigureAwait(false);
			return Results.Json(
				summaries.Select(s => ToView(s, exposeSensitive)).ToArray(),
				SagaJsonContext.Default.SagaInstanceViewArray);
		});

		// "Stuck" sagas = sagas with an overdue (due) timeout that is still pending, excluding any that have
		// since completed. Capability-gated on ISagaTimeoutStore (its GetDueTimeoutsAsync is read-only); when
		// no timeout store is configured the endpoint fails open with Available=false rather than a 500. The
		// handler is a separate method to keep MapEndpoints' type-coupling within bounds (CA1506).
		group.MapGet("/saga/stuck", async (
			ISagaTimeoutStore? timeouts,
			ISagaStoreAdmin? admin,
			TimeProvider? clock,
			int? limit,
			CancellationToken ct) =>
			await GetStuckAsync(timeouts, admin, clock, limit, defaultPageSize, maxPageSize, ct).ConfigureAwait(false));

		group.MapGet("/saga/{id:guid}", async (Guid id, ISagaStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.NotFound();
			}

			var summary = await admin.GetSummaryAsync(id, ct).ConfigureAwait(false);
			return summary is null
				? Results.NotFound()
				: Results.Json(ToView(summary, exposeSensitive), SagaJsonContext.Default.SagaInstanceView);
		});
	}

	private static async Task<IResult> GetStuckAsync(
		ISagaTimeoutStore? timeouts,
		ISagaStoreAdmin? admin,
		TimeProvider? clock,
		int? limit,
		int defaultPageSize,
		int maxPageSize,
		CancellationToken ct)
	{
		if (timeouts is null)
		{
			return Results.Json(new StuckSagaResult { Available = false }, SagaJsonContext.Default.StuckSagaResult);
		}

		// Clamp the page size the same way every sibling list endpoint does, then bound the scan: only
		// the first pageSize due timeouts are examined. This caps both the response size AND the number
		// of per-item admin.GetSummaryAsync completion checks (the N+1), which were previously unbounded.
		var pageSize = Math.Clamp(limit ?? defaultPageSize, 1, maxPageSize);
		var now = (clock ?? TimeProvider.System).GetUtcNow();
		var due = await timeouts.GetDueTimeoutsAsync(now, ct).ConfigureAwait(false);

		var stuck = new List<StuckSagaView>(Math.Min(due.Count, pageSize));
		foreach (var t in due.Take(pageSize))
		{
			// Exclude sagas that have completed since the timeout became due (when the admin surface is
			// available to check); a completed saga is not stuck.
			if (admin is not null && Guid.TryParse(t.SagaId, out var sagaId))
			{
				var summary = await admin.GetSummaryAsync(sagaId, ct).ConfigureAwait(false);
				if (summary is { IsCompleted: true })
				{
					continue;
				}
			}

			stuck.Add(new StuckSagaView
			{
				SagaId = t.SagaId,
				SagaType = t.SagaType,
				TimeoutId = t.TimeoutId,
				DueAt = t.DueAt,
				OverdueSeconds = Math.Max(0, (now - t.DueAt).TotalSeconds),
			});
		}

		return Results.Json(
			new StuckSagaResult { Available = true, Stuck = stuck },
			SagaJsonContext.Default.StuckSagaResult);
	}

	// TenantId is redacted (omitted) unless the host opts in via DashboardOptions.ExposeSensitiveData
	// behind an authorized boundary — an open read must not leak cross-tenant ownership.
	private static SagaInstanceView ToView(SagaInstanceSummary s, bool exposeSensitive) => new()
	{
		SagaId = s.SagaId,
		SagaType = s.SagaType,
		IsCompleted = s.IsCompleted,
		CompletedAt = s.CompletedAt,
		TenantId = exposeSensitive ? s.TenantId : null,
		Version = s.Version,
	};
}

/// <summary>The stuck-saga view: whether stuck-detection is available (a timeout store is configured) and the stuck instances.</summary>
public sealed class StuckSagaResult
{
	/// <summary>Gets a value indicating whether stuck-detection is available (an <c>ISagaTimeoutStore</c> is configured).</summary>
	/// <value><see langword="true"/> when a timeout store is configured; otherwise <see langword="false"/>.</value>
	public bool Available { get; init; }

	/// <summary>Gets the stuck saga instances (running sagas with an overdue, still-pending timeout).</summary>
	/// <value>The stuck instances; empty when none or when unavailable.</value>
	public IReadOnlyList<StuckSagaView> Stuck { get; init; } = [];
}

/// <summary>A read-only view of a stuck saga: the overdue timeout that flags it and how far overdue it is.</summary>
public sealed class StuckSagaView
{
	/// <summary>Gets the identifier of the stuck saga.</summary>
	/// <value>The saga id.</value>
	public string SagaId { get; init; } = string.Empty;

	/// <summary>Gets the saga type name.</summary>
	/// <value>The saga type name.</value>
	public string SagaType { get; init; } = string.Empty;

	/// <summary>Gets the identifier of the overdue timeout flagging this saga.</summary>
	/// <value>The timeout id.</value>
	public string TimeoutId { get; init; } = string.Empty;

	/// <summary>Gets the instant the timeout was due.</summary>
	/// <value>The due timestamp.</value>
	public DateTimeOffset DueAt { get; init; }

	/// <summary>Gets how many seconds past due the timeout is.</summary>
	/// <value>The overdue duration in seconds.</value>
	public double OverdueSeconds { get; init; }
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SagaView))]
[JsonSerializable(typeof(SagaInstanceView))]
[JsonSerializable(typeof(SagaInstanceView[]))]
[JsonSerializable(typeof(StuckSagaResult))]
internal sealed partial class SagaJsonContext : JsonSerializerContext;
