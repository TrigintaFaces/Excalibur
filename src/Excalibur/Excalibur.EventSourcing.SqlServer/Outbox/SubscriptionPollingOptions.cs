// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.SqlServer.Outbox;

/// <summary>
/// Configuration options for the SQL Server outbox subscription-based poller.
/// </summary>
public sealed class SubscriptionPollingOptions
{
	/// <summary>
	/// Gets or sets the polling interval when no query notifications are available.
	/// </summary>
	/// <value>The poll interval. Defaults to 5 seconds.</value>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum number of messages to retrieve per polling cycle.
	/// </summary>
	/// <value>The batch size. Defaults to 100.</value>
	[Range(1, 10000)]
	public int BatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets a value indicating whether to use SQL Server Query Notifications
	/// for event-driven polling instead of fixed-interval polling.
	/// </summary>
	/// <remarks>
	/// When enabled, the poller subscribes to SQL Server Query Notifications and wakes up
	/// immediately when outbox messages are inserted. Falls back to <see cref="PollInterval"/>
	/// when Query Notifications are unavailable or fail.
	/// Requires SQL Server Service Broker to be enabled on the database.
	/// </remarks>
	/// <value><see langword="true"/> to use query notifications; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool UseQueryNotifications { get; set; }

	/// <summary>
	/// Gets or sets the SQL Server connection string for the outbox database.
	/// </summary>
	/// <value>The connection string.</value>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the schema name for the outbox table.
	/// </summary>
	/// <value>The schema name. Defaults to "dbo".</value>
	[Required]
	public string SchemaName { get; set; } = "dbo";

	/// <summary>
	/// Gets or sets the outbox table name.
	/// </summary>
	/// <value>The table name. Defaults to "EventSourcedOutbox".</value>
	[Required]
	public string TableName { get; set; } = "EventSourcedOutbox";

	/// <summary>
	/// Gets or sets the timeout for query notification subscriptions.
	/// </summary>
	/// <value>The notification timeout. Defaults to 60 seconds.</value>
	public TimeSpan NotificationTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
