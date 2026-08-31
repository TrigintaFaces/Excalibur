// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Excalibur.Operations.Dashboard;

/// <summary>
/// A read-only view of the transactional outbox: message counts by status and the age of the oldest
/// pending and failed messages, plus whether the outbox is configured in the running host.
/// </summary>
public sealed class OutboxView
{
	/// <summary>Gets a value indicating whether an outbox store is configured in the host.</summary>
	/// <value><see langword="true"/> when the outbox admin store is registered; otherwise <see langword="false"/>.</value>
	public bool Configured { get; init; }

	/// <summary>Gets the number of staged messages awaiting delivery.</summary>
	/// <value>The staged message count.</value>
	public int Staged { get; init; }

	/// <summary>Gets the number of messages currently being sent.</summary>
	/// <value>The in-flight send count.</value>
	public int Sending { get; init; }

	/// <summary>Gets the number of successfully sent messages.</summary>
	/// <value>The sent message count.</value>
	public int Sent { get; init; }

	/// <summary>Gets the number of messages that failed delivery.</summary>
	/// <value>The failed message count.</value>
	public int Failed { get; init; }

	/// <summary>Gets the number of messages scheduled for future delivery.</summary>
	/// <value>The scheduled message count.</value>
	public int Scheduled { get; init; }

	/// <summary>Gets the age, in seconds, of the oldest unsent message, or <see langword="null"/> when none.</summary>
	/// <value>The oldest-unsent age in seconds.</value>
	public double? OldestUnsentSeconds { get; init; }

	/// <summary>Gets the age, in seconds, of the oldest failed message, or <see langword="null"/> when none.</summary>
	/// <value>The oldest-failed age in seconds.</value>
	public double? OldestFailedSeconds { get; init; }

	/// <summary>Gets the UTC instant these statistics were captured.</summary>
	/// <value>The capture timestamp.</value>
	public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Maps the outbox read endpoint. Fails open: when no outbox store is configured, the endpoint returns
/// a not-configured view rather than a 500.
/// </summary>
internal sealed class OutboxDashboardModule : IDashboardEndpointModule
{
	/// <inheritdoc />
	public string Subsystem => "outbox";

	/// <inheritdoc />
	public void MapEndpoints(RouteGroupBuilder group, DashboardOptions options)
	{
		ArgumentNullException.ThrowIfNull(group);

		group.MapGet("/outbox", static async (IOutboxStoreAdmin? admin, CancellationToken ct) =>
		{
			if (admin is null)
			{
				return Results.Json(
					new OutboxView { Configured = false, CapturedAt = DateTimeOffset.UtcNow },
					OutboxJsonContext.Default.OutboxView);
			}

			var stats = await admin.GetAllTenantsStatisticsAsync(ct).ConfigureAwait(false);
			var view = new OutboxView
			{
				Configured = true,
				Staged = stats.StagedMessageCount,
				Sending = stats.SendingMessageCount,
				Sent = stats.SentMessageCount,
				Failed = stats.FailedMessageCount,
				Scheduled = stats.ScheduledMessageCount,
				OldestUnsentSeconds = stats.OldestUnsentMessageAge?.TotalSeconds,
				OldestFailedSeconds = stats.OldestFailedMessageAge?.TotalSeconds,
				CapturedAt = stats.CapturedAt,
			};
			return Results.Json(view, OutboxJsonContext.Default.OutboxView);
		});
	}
}

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OutboxView))]
internal sealed partial class OutboxJsonContext : JsonSerializerContext;
