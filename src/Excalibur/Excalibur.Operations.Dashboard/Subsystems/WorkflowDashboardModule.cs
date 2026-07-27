// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Workflows;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>A read-only view of the durable-workflow store: running/completed/faulted/total counts and whether it is queryable.</summary>
public sealed class WorkflowView
{
	/// <summary>
	/// Gets a value indicating whether a queryable workflow admin store is configured in the host.
	/// </summary>
	/// <remarks>
	/// This is the feature-presence signal that distinguishes "no workflow admin store is registered"
	/// (<see langword="false"/> — the listing capability is absent, so an empty instance list means
	/// <em>unavailable</em>, not zero) from "a store is registered but currently holds no instances"
	/// (<see langword="true"/> with <see cref="Total"/> = 0). A consumer should read this before treating
	/// an empty <c>/workflows/instances</c> response as "zero workflows."
	/// </remarks>
	/// <value><see langword="true"/> when the workflow admin store is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the number of running workflow instances.</summary>
	/// <value>The running instance count.</value>
	public long Running { get; init; }

	/// <summary>Gets the number of completed workflow instances.</summary>
	/// <value>The completed instance count.</value>
	public long Completed { get; init; }

	/// <summary>Gets the number of faulted workflow instances.</summary>
	/// <value>The faulted instance count.</value>
	public long Faulted { get; init; }

	/// <summary>Gets the total number of workflow instances.</summary>
	/// <value>The total instance count.</value>
	public long Total { get; init; }

	/// <summary>Gets the UTC instant these statistics were captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>A read-only projection of a durable-workflow instance for dashboard listing.</summary>
public sealed class WorkflowInstanceView
{
	/// <summary>Gets the workflow instance identifier.</summary>
	/// <value>The workflow instance id.</value>
	public string InstanceId { get; init; } = string.Empty;

	/// <summary>Gets the registered workflow name the instance is running.</summary>
	/// <value>The workflow name.</value>
	public string WorkflowName { get; init; } = string.Empty;

	/// <summary>Gets the lifecycle status of the instance.</summary>
	/// <value>The status name (<c>Running</c>, <c>Completed</c>, or <c>Faulted</c>).</value>
	public string Status { get; init; } = string.Empty;

	/// <summary>Gets the instant the instance was started.</summary>
	/// <value>The start timestamp.</value>
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>Gets the completion instant, or <see langword="null"/> while running.</summary>
	/// <value>The completion timestamp, or <see langword="null"/>.</value>
	public DateTimeOffset? CompletedAt { get; init; }

	/// <summary>Gets the owning tenant, or <see langword="null"/> when not tenant-scoped or redacted.</summary>
	/// <value>The tenant id, or <see langword="null"/>.</value>
	public string? TenantId { get; init; }
}

/// <summary>
/// Maps the durable-workflow read endpoints over the optional <see cref="IWorkflowStoreAdmin"/> extension
/// point. A workflow store enables this panel by implementing and registering that interface; when none is
/// registered the endpoints fail open — the summary reports <c>configured:false</c> and the list is empty —
/// rather than returning a 500. No workflow store is required for the dashboard to function.
/// </summary>
internal sealed class WorkflowDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "workflows";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(options);

		var defaultPageSize = options.DefaultPageSize;
		var maxPageSize = options.MaxPageSize;
		var exposeSensitive = options.ExposeSensitiveData;

		group.MapGet("/workflows", static async (IWorkflowStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(
					new WorkflowView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					WorkflowJsonContext.Default.WorkflowView);
			}

			var stats = await admin.GetStatisticsAsync(ct).ConfigureAwait(false);
			return Results.Json(
				new WorkflowView
				{
					Configured = true,
					Running = stats.Running,
					Completed = stats.Completed,
					Faulted = stats.Faulted,
					Total = stats.Total,
					CapturedAt = DateTimeOffset.UtcNow,
				},
				WorkflowJsonContext.Default.WorkflowView);
		});

		group.MapGet("/workflows/instances", async (
			IWorkflowStoreAdmin? admin,
			string? status,
			string? workflow,
			int? skip,
			int? limit,
			CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(
					Array.Empty<WorkflowInstanceView>(), WorkflowJsonContext.Default.WorkflowInstanceViewArray);
			}

			var filter = new WorkflowQueryFilter
			{
				Status = Enum.TryParse<WorkflowStatus>(status, ignoreCase: true, out var parsed) ? parsed : null,
				WorkflowName = string.IsNullOrWhiteSpace(workflow) ? null : workflow,
				Skip = Math.Max(0, skip ?? 0),
				Take = Math.Clamp(limit ?? defaultPageSize, 1, maxPageSize),
			};

			var summaries = await admin.QueryWorkflowsAsync(filter, ct).ConfigureAwait(false);
			return Results.Json(
				summaries.Select(s => ToView(s, exposeSensitive)).ToArray(),
				WorkflowJsonContext.Default.WorkflowInstanceViewArray);
		});

		group.MapGet("/workflows/{instanceId}", async (
			string instanceId, IWorkflowStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.NotFound();
			}

			var summary = await admin.GetSummaryAsync(instanceId, ct).ConfigureAwait(false);
			return summary is null
				? Results.NotFound()
				: Results.Json(ToView(summary, exposeSensitive), WorkflowJsonContext.Default.WorkflowInstanceView);
		});
	}

	// TenantId is redacted (omitted) unless the host opts in via DashboardOptions.ExposeSensitiveData behind
	// an authorized boundary — an open read must not leak cross-tenant ownership.
	private static WorkflowInstanceView ToView(WorkflowInstanceSummary s, bool exposeSensitive) => new()
	{
		InstanceId = s.InstanceId,
		WorkflowName = s.WorkflowName,
		Status = s.Status.ToString(),
		StartedAt = s.StartedAt,
		CompletedAt = s.CompletedAt,
		TenantId = exposeSensitive ? s.TenantId : null,
	};
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkflowView))]
[JsonSerializable(typeof(WorkflowInstanceView))]
[JsonSerializable(typeof(WorkflowInstanceView[]))]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext;
