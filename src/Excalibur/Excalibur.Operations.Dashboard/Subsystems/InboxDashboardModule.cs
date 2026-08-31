// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>A read-only view of the inbox: entry counts by status and whether it is configured.</summary>
public sealed class InboxView
{
	/// <summary>Gets a value indicating whether an inbox admin store is configured in the host.</summary>
	/// <value><see langword="true"/> when the inbox admin store is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the total number of inbox entries.</summary>
	/// <value>The total entry count.</value>
	public int Total { get; init; }

	/// <summary>Gets the number of successfully processed entries.</summary>
	/// <value>The processed entry count.</value>
	public int Processed { get; init; }

	/// <summary>Gets the number of failed entries.</summary>
	/// <value>The failed entry count.</value>
	public int Failed { get; init; }

	/// <summary>Gets the number of pending entries.</summary>
	/// <value>The pending entry count.</value>
	public int Pending { get; init; }

	/// <summary>Gets the UTC instant these statistics were captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Maps the inbox read endpoint. Fails open: when no inbox admin store is configured the endpoint
/// returns a not-configured view rather than a 500.
/// </summary>
internal sealed class InboxDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "inbox";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);

		group.MapGet("/inbox", static async (IInboxStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(
					new InboxView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					InboxJsonContext.Default.InboxView);
			}

			var stats = await admin.GetAllTenantsStatisticsAsync(ct).ConfigureAwait(false);
			var view = new InboxView
			{
				Configured = true,
				Total = stats.TotalEntries,
				Processed = stats.ProcessedEntries,
				Failed = stats.FailedEntries,
				Pending = stats.PendingEntries,
				CapturedAt = DateTimeOffset.UtcNow,
			};
			return Results.Json(view, InboxJsonContext.Default.InboxView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(InboxView))]
internal sealed partial class InboxJsonContext : JsonSerializerContext;
