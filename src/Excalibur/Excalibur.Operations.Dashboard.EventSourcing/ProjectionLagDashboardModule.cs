// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.EventSourcing.Projections;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard.EventSourcing;

/// <summary>A read-only view of per-subscription projection lag against the global stream head.</summary>
internal sealed class ProjectionLagView
{
	/// <summary>Gets a value indicating whether projection-lag reporting is configured in the host.</summary>
	/// <value><see langword="true"/> when an event-store head source is available; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the per-subscription lag entries. Empty when configured but no checkpoints exist.</summary>
	/// <value>The lag entries.</value>
	public IReadOnlyList<ProjectionLag> Streams { get; init; } = [];

	/// <summary>Gets the UTC instant this view was captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Maps the projection/CDC-lag read endpoint <c>/projections/lag</c>. Fails open: when no projection-lag
/// read model is configured the endpoint returns a not-configured payload rather than a 500.
/// </summary>
internal sealed class ProjectionLagDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "projections";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(options);

		group.MapGet("/projections/lag", static async (IProjectionLagReadModel? readModel, TimeProvider timeProvider, CancellationToken ct) =>
		{
			if (readModel is null)
			{
				return Results.Json(
					new ProjectionLagView { Configured = false, CapturedAt = timeProvider.GetUtcNow() },
					ProjectionLagJsonContext.Default.ProjectionLagView);
			}

			var lag = await readModel.GetLagAsync(ct).ConfigureAwait(false);

			return Results.Json(
				new ProjectionLagView { Configured = true, Streams = lag, CapturedAt = timeProvider.GetUtcNow() },
				ProjectionLagJsonContext.Default.ProjectionLagView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectionLagView))]
internal sealed partial class ProjectionLagJsonContext : JsonSerializerContext;
