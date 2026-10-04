// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// Gets the measurement's availability, which is what a reader must branch on — <see cref="Configured"/>
	/// alone cannot distinguish a clean measurement from a contradictory one.
	/// </summary>
	/// <remarks>
	/// <b>Do not render this view from <see cref="Configured"/> and <see cref="Streams"/> alone.</b>
	/// Configured plus an empty Streams means no subscription is behind. Configured plus a value here of
	/// <c>CheckpointAheadOfHead</c> means a checkpoint reports a position above the head, which is
	/// impossible while the head source and the event store describe the same stream — a configuration
	/// fault, not an absence of lag. Those two states look identical through the other two properties, and
	/// the second one previously reported as the first.
	/// </remarks>
	/// <value>The availability of the underlying measurement.</value>
	public string Availability { get; init; } = string.Empty;

	/// <summary>
	/// Gets the per-subscription lag entries. Empty when <see cref="Configured"/> is
	/// <see langword="true"/> and no subscription is behind, and always empty when it is
	/// <see langword="false"/> — in which case the emptiness carries no information about lag.
	/// </summary>
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

			var report = await readModel.GetLagAsync(ct).ConfigureAwait(false);

			// `Configured` is reported from the AVAILABILITY, not from the read model merely resolving.
			// Its own documentation says "true when an event-store head source is available", and this
			// line used to set it true whenever the service resolved -- so a host that registered the read
			// model WITHOUT a head source served `configured: true` with no streams, which is
			// indistinguishable from "every projection is caught up". The field now means what it says.
			return Results.Json(
				new ProjectionLagView
				{
					// A contradiction counts as CONFIGURED: a head source answered, so this is not the
					// "nothing is registered" case, and collapsing it into that would hide a wiring fault
					// behind the same flag that means "no head source". The availability value below is
					// what distinguishes them, and it is what a reader must branch on.
					Configured = report.Availability != ProjectionLagAvailability.NoHeadSource,
					Availability = report.Availability.ToString(),
					Streams = report.Streams,
					CapturedAt = timeProvider.GetUtcNow(),
				},
				ProjectionLagJsonContext.Default.ProjectionLagView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectionLagView))]
internal sealed partial class ProjectionLagJsonContext : JsonSerializerContext;
