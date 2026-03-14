// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Configuration options for the Postgres Change Data Capture (CDC) processor.
/// </summary>
public sealed class PostgresCdcOptions
{
	/// <summary>
	/// Gets or sets the connection string for the Postgres database.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the name of the publication to subscribe to.
	/// </summary>
	[Required]
	public string PublicationName { get; set; } = "excalibur_cdc_publication";

	/// <summary>
	/// Gets or sets the name of the replication slot.
	/// </summary>
	[Required]
	public string ReplicationSlotName { get; set; } = "excalibur_cdc_slot";

	/// <summary>
	/// Gets or sets the table names to capture changes for.
	/// </summary>
	public string[] TableNames { get; set; } = [];

	/// <summary>
	/// Gets or sets a unique identifier for this CDC processor instance.
	/// </summary>
	[Required]
	public string ProcessorId { get; set; } = Environment.MachineName;

	/// <summary>
	/// Gets or sets the polling interval when no changes are available.
	/// </summary>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum number of changes to process in a single batch.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the timeout for replication operations.
	/// </summary>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the recovery options for handling stale WAL position scenarios.
	/// </summary>
	public PostgresCdcRecoveryOptions? RecoveryOptions { get; set; }

	/// <summary>
	/// Gets or sets the replication configuration options.
	/// </summary>
	public PostgresCdcReplicationOptions Replication { get; set; } = new();

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
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
