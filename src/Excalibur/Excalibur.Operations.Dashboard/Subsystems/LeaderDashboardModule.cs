// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// A read-only, instance-local snapshot of leader-election state: whether this instance holds
/// leadership, its candidate id, the observed current leader, and (optionally) the current fencing
/// token for a named protected resource.
/// </summary>
/// <remarks>
/// Leadership state on <see cref="ILeaderElection"/> is instance-local; this view reflects the state
/// of the responding host only. Cross-instance lease-TTL is a provider-specific concern surfaced
/// separately when available.
/// </remarks>
public sealed class LeaderView
{
	/// <summary>Gets a value indicating whether leader election is configured in the host.</summary>
	/// <value><see langword="true"/> when leader election is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets a value indicating whether this instance is currently the leader.</summary>
	/// <value><see langword="true"/> when this instance holds leadership; otherwise <see langword="false"/>.</value>
	public bool IsLeader { get; init; }

	/// <summary>Gets this instance's election candidate identifier.</summary>
	/// <value>The candidate id, or <see langword="null"/> when not configured.</value>
	public string? CandidateId { get; init; }

	/// <summary>Gets the observed current leader identifier.</summary>
	/// <value>The current leader id, or <see langword="null"/> when unknown.</value>
	public string? CurrentLeaderId { get; init; }

	/// <summary>Gets the current fencing token for the requested resource, if one was requested and exists.</summary>
	/// <value>The fencing token, or <see langword="null"/>.</value>
	public long? FencingToken { get; init; }

	/// <summary>Gets the protected resource id the fencing token applies to, if requested.</summary>
	/// <value>The resource id, or <see langword="null"/>.</value>
	public string? Resource { get; init; }

	/// <summary>Gets the UTC instant this snapshot was captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Maps the leader-election read endpoint. Fails open: when leader election is not configured the
/// endpoint returns a not-configured snapshot rather than a 500.
/// </summary>
internal sealed class LeaderDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "leader";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);

		group.MapGet("/leader", static async (
			ILeaderElection? election,
			IFencingTokenProvider? fencing,
			string? resource,
			CancellationToken ct) =>
		{
			if (election is null)
			{
				return Results.Json(
					new LeaderView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					LeaderJsonContext.Default.LeaderView);
			}

			long? token = null;
			if (fencing is not null && !string.IsNullOrWhiteSpace(resource))
			{
				token = await fencing.GetTokenAsync(resource, ct).ConfigureAwait(false);
			}

			var view = new LeaderView
			{
				Configured = true,
				IsLeader = election.IsLeader,
				CandidateId = election.CandidateId,
				CurrentLeaderId = election.CurrentLeaderId,
				FencingToken = token,
				Resource = string.IsNullOrWhiteSpace(resource) ? null : resource,
				CapturedAt = DateTimeOffset.UtcNow,
			};
			return Results.Json(view, LeaderJsonContext.Default.LeaderView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LeaderView))]
internal sealed partial class LeaderJsonContext : JsonSerializerContext;
