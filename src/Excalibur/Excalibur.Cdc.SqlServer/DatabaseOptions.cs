// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

using Excalibur.Cdc;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Represents the configuration settings required for CDC (Change Data Capture) processing.
/// </summary>
/// <remarks>
/// The set of tracked tables is the single source of truth: <see cref="CaptureInstances"/> and
/// <see cref="CaptureInstanceToTableNameMap"/> are derived from <see cref="Tables"/>. Configure
/// each table with its logical <see cref="CdcTableConfig.TableName"/> and, when the SQL Server
/// capture instance differs (e.g. the default <c>{schema}_{table}</c>), its
/// <see cref="CdcTableConfig.CaptureInstance"/>.
/// <para>
/// These mutable options support configuration binding. A processor captures its own execution
/// settings during construction; later option changes apply to newly constructed processors.
/// Keep the input configuration stable while constructing a processor.
/// </para>
/// </remarks>
public sealed class DatabaseOptions : IDatabaseOptions
{
	private Collection<CdcTableConfig> _tables = [];
	private int _queueSize = CdcDefaultQueueSize;
	private int _producerBatchSize = CdcDefaultProducerBatchSize;
	private int _consumerBatchSize = CdcDefaultConsumerBatchSize;

	/// <summary>Initializes database configuration with the standard processing defaults.</summary>
	public DatabaseOptions()
	{
	}

	/// <summary>
	/// Initializes database configuration with its three required identifiers.
	/// </summary>
	/// <param name="databaseName">The name of the database being processed.</param>
	/// <param name="databaseConnectionIdentifier">The source database connection identifier.</param>
	/// <param name="stateConnectionIdentifier">The CDC state database connection identifier.</param>
	/// <remarks>
	/// The parameterless constructor and public setters support generated configuration binding.
	/// The containing job options retain their startup validation.
	/// </remarks>
	public DatabaseOptions(string databaseName, string databaseConnectionIdentifier, string stateConnectionIdentifier)
	{
		DatabaseName = databaseName;
		DatabaseConnectionIdentifier = databaseConnectionIdentifier;
		StateConnectionIdentifier = stateConnectionIdentifier;
	}

	/// <inheritdoc />
	[Required]
	public string DatabaseName { get; set; } = string.Empty;

	/// <inheritdoc />
	[Required]
	public string DatabaseConnectionIdentifier { get; set; } = string.Empty;

	/// <inheritdoc />
	[Required]
	public string StateConnectionIdentifier { get; set; } = string.Empty;

	/// <inheritdoc />
	public bool StopOnMissingTableHandler { get; set; } = CdcDefaultStopOnMissingTableHandler;

	/// <summary>
	/// Gets or sets the tables tracked for CDC in this database.
	/// </summary>
	/// <value>
	/// The tracked tables. Each entry supplies a logical <see cref="CdcTableConfig.TableName"/> and an
	/// optional <see cref="CdcTableConfig.CaptureInstance"/>. <see cref="CaptureInstances"/> and
	/// <see cref="CaptureInstanceToTableNameMap"/> are derived from this collection.
	/// </value>
	public Collection<CdcTableConfig> Tables
	{
		get => _tables;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_tables = value;
		}
	}

	/// <inheritdoc />
	/// <remarks>Derived from <see cref="Tables"/>.</remarks>
	public string[] CaptureInstances => Derived().Instances;

	/// <inheritdoc />
	public int QueueSize
	{
		get => _queueSize;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(QueueSize));
			_queueSize = value;
		}
	}

	/// <inheritdoc />
	public int ProducerBatchSize
	{
		get => _producerBatchSize;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ProducerBatchSize));
			_producerBatchSize = value;
		}
	}

	/// <inheritdoc />
	public int ConsumerBatchSize
	{
		get => _consumerBatchSize;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ConsumerBatchSize));
			_consumerBatchSize = value;
		}
	}

	/// <inheritdoc />
	public CdcRecoveryOptions? RecoveryOptions { get; set; }

	/// <inheritdoc />
	/// <remarks>Derived from <see cref="Tables"/>.</remarks>
	public IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap => Derived().Map;

	private (string[] Instances, IReadOnlyDictionary<string, string> Map) Derived() =>
		CdcCaptureInstanceDeriver.Derive(_tables);
}
