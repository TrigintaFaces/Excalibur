// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>Constructs optional capabilities from one store's captured source and tenant context.</summary>
internal sealed class SqlServerEventStoreCapabilities
{
	private readonly SqlServerAuthoritativeEventReader? _reader;
	private readonly EventStoreSourceIdentity _sourceIdentity;
	private readonly SqlServerArchiveEventReader _archiveReader;
	private readonly SqlServerArchiveScanner _archiveScanner;

	internal SqlServerEventStoreCapabilities(Func<SqlConnection> connectionFactory, string schema, string table,
		ITenantContext tenantContext, bool ownsPrimaryConnections)
		: this(connectionFactory, schema, table, tenantContext, ownsPrimaryConnections, new EventStoreSourceIdentity())
	{
	}

	internal SqlServerEventStoreCapabilities(Func<SqlConnection> connectionFactory, string schema, string table,
		ITenantContext tenantContext, bool ownsPrimaryConnections, EventStoreSourceIdentity sourceIdentity)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(tenantContext);
		_sourceIdentity = sourceIdentity ?? throw new ArgumentNullException(nameof(sourceIdentity));
		_archiveReader = new SqlServerArchiveEventReader(connectionFactory, schema, table);
		_archiveScanner = new SqlServerArchiveScanner(connectionFactory, schema, table);
		_reader = ownsPrimaryConnections
			? SqlServerAuthoritativeEventReader.CreateConfined(connectionFactory, schema, table, tenantContext)
			: null;
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
