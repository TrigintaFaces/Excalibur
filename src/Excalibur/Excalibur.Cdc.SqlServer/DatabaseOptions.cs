// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
/// </remarks>
public sealed class DatabaseOptions : IDatabaseOptions
{
	private readonly Collection<CdcTableConfig> _tables = [];
	private readonly int _queueSize = CdcDefaultQueueSize;
	private readonly int _producerBatchSize = CdcDefaultProducerBatchSize;
	private readonly int _consumerBatchSize = CdcDefaultConsumerBatchSize;
	private (string[] Instances, IReadOnlyDictionary<string, string> Map)? _derived;

	/// <inheritdoc />
	[Required]
	public required string DatabaseName { get; init; }

	/// <inheritdoc />
	[Required]
	public required string DatabaseConnectionIdentifier { get; init; }

	/// <inheritdoc />
	[Required]
	public required string StateConnectionIdentifier { get; init; }

	/// <inheritdoc />
	public bool StopOnMissingTableHandler { get; init; } = CdcDefaultStopOnMissingTableHandler;

	/// <summary>
	/// Gets the tables tracked for CDC in this database.
	/// </summary>
	/// <value>
	/// The tracked tables. Each entry supplies a logical <see cref="CdcTableConfig.TableName"/> and an
	/// optional <see cref="CdcTableConfig.CaptureInstance"/>. <see cref="CaptureInstances"/> and
	/// <see cref="CaptureInstanceToTableNameMap"/> are derived from this collection.
	/// </value>
	public Collection<CdcTableConfig> Tables
	{
		get => _tables;
		init
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
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(QueueSize));
			_queueSize = value;
		}
	}

	/// <inheritdoc />
	public int ProducerBatchSize
	{
		get => _producerBatchSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ProducerBatchSize));
			_producerBatchSize = value;
		}
	}

	/// <inheritdoc />
	public int ConsumerBatchSize
	{
		get => _consumerBatchSize;
		init
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(ConsumerBatchSize));
			_consumerBatchSize = value;
		}
	}

	/// <inheritdoc />
	public CdcRecoveryOptions? RecoveryOptions { get; init; }

	/// <inheritdoc />
	/// <remarks>Derived from <see cref="Tables"/>.</remarks>
	public IReadOnlyDictionary<string, string> CaptureInstanceToTableNameMap => Derived().Map;

	private (string[] Instances, IReadOnlyDictionary<string, string> Map) Derived() =>
		_derived ??= CdcCaptureInstanceDeriver.Derive(_tables);
}
