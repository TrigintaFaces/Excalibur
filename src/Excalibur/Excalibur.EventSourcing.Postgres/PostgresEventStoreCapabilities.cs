// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>Constructs optional capabilities without taking ownership of the store's data source.</summary>
internal sealed class PostgresEventStoreCapabilities
{
	private readonly PostgresAuthoritativeEventReader _reader;
	private readonly EventStoreSourceIdentity _sourceIdentity;
	private readonly PostgresArchiveEventReader _archiveReader;
	private readonly PostgresArchiveScanner _archiveScanner;

	internal PostgresEventStoreCapabilities(NpgsqlDataSource dataSource, string schema, string table,
		ITenantContext tenantContext)
		: this(dataSource, schema, table, tenantContext, new EventStoreSourceIdentity())
	{
	}

	internal PostgresEventStoreCapabilities(NpgsqlDataSource dataSource, string schema, string table,
		ITenantContext tenantContext, EventStoreSourceIdentity sourceIdentity)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(tenantContext);
		_sourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
		_archiveReader = new PostgresArchiveEventReader(dataSource, schema, table);
		_archiveScanner = new PostgresArchiveScanner(dataSource, schema, table);
		_reader = PostgresAuthoritativeEventReader.CreateConfined(dataSource, schema, table, tenantContext);
	}

	internal TimeProvider ArchiveTimeProvider
	{
		get => _archiveScanner.TimeProvider;
		set => _archiveScanner.TimeProvider = value;
	}

	internal object? GetService(Type serviceType) => serviceType == typeof(EventStoreSourceIdentity)
		? _sourceIdentity : serviceType == typeof(IEventStoreAuthoritativeReader) ? _reader
		: serviceType == typeof(IEventStoreArchiveReader) ? _archiveReader
		: serviceType == typeof(IEventStoreArchiveScanner) ? _archiveScanner : null;
}
