// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Configuration options for the Postgres Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// Postgres CDC uses logical replication with the pgoutput protocol.
/// Requires a publication and replication slot to be created on the server.
/// </para>
/// <para>
/// Server requirements:
/// <list type="bullet">
/// <item><description>wal_level = logical</description></item>
/// <item><description>max_replication_slots &gt;= 1</description></item>
/// <item><description>max_wal_senders &gt;= 1</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PostgresCdcOptions
{
	/// <summary>
	/// Gets or sets the connection string for the Postgres database.
	/// </summary>
	/// <value>The Postgres connection string.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the publication to subscribe to.
	/// </summary>
	/// <remarks>
	/// The publication must be created on the server using CREATE PUBLICATION.
	/// </remarks>
	/// <value>The publication name. Defaults to "excalibur_cdc_publication".</value>
	[Required]
	public string PublicationName { get; set; } = "excalibur_cdc_publication";

	/// <summary>
	/// Gets or sets the name of the replication slot.
	/// </summary>
	/// <remarks>
	/// The replication slot will be created automatically if it doesn't exist.
	/// </remarks>
	/// <value>The replication slot name. Defaults to "excalibur_cdc_slot".</value>
	[Required]
	public string ReplicationSlotName { get; set; } = "excalibur_cdc_slot";

	/// <summary>
	/// Gets or sets the table names to capture changes for.
	/// </summary>
	/// <remarks>
	/// If empty, all tables in the publication will be captured.
	/// Table names should be fully qualified (schema.table) or just table name for public schema.
	/// </remarks>
	/// <value>The array of table names to capture.</value>
	public string[] TableNames { get; set; } = [];

	/// <summary>
	/// Gets or sets a unique identifier for this CDC processor instance.
	/// </summary>
	/// <remarks>
	/// Used to track position state when multiple processors are running.
	/// </remarks>
	/// <value>The processor identifier. Defaults to the machine name.</value>
	[Required]
	public string ProcessorId { get; set; } = Environment.MachineName;

	/// <summary>
	/// Gets or sets the polling interval when no changes are available.
	/// </summary>
	/// <value>The polling interval. Defaults to 1 second.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of changes to process in a single batch.
	/// </summary>
	/// <value>The batch size. Defaults to 1000.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the timeout for replication operations.
	/// </summary>
	/// <value>The timeout. Defaults to 30 seconds.</value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to create the replication slot if it doesn't exist.
	/// </summary>
	/// <value><see langword="true"/> to auto-create the slot; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool AutoCreateSlot { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use binary protocol for logical replication.
	/// </summary>
	/// <remarks>
	/// Binary protocol is more efficient but may have compatibility issues with some data types.
	/// </remarks>
	/// <value><see langword="true"/> to use binary protocol; otherwise, <see langword="false"/>. Defaults to <see langword="false"/>.</value>
	public bool UseBinaryProtocol { get; set; }

	/// <summary>
	/// Gets or sets the recovery options for handling stale WAL position scenarios.
	/// </summary>
	/// <value>
	/// The recovery options, or <see langword="null"/> to use default behavior (throw on stale position).
	/// </value>
	/// <remarks>
	/// <para>
	/// Configure this property to control how the processor handles scenarios where the saved
	/// WAL position is no longer available (e.g., due to WAL segment removal or slot invalidation).
	/// </para>
	/// <para>
	/// When <see langword="null"/>, the processor uses the legacy behavior of throwing an exception.
	/// </para>
	/// </remarks>
	public PostgresCdcRecoveryOptions? RecoveryOptions { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing or invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(PublicationName))
		{
			throw new InvalidOperationException("PublicationName is required.");
		}

		if (string.IsNullOrWhiteSpace(ReplicationSlotName))
		{
			throw new InvalidOperationException("ReplicationSlotName is required.");
		}

		if (BatchSize <= 0)
		{
			throw new InvalidOperationException("BatchSize must be greater than 0.");
		}

		RecoveryOptions?.Validate();
	}
}
