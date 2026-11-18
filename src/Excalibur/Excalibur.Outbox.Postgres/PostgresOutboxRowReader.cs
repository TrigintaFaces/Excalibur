// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>Closed mappings for the fixed Postgres reserved-message projection.</summary>
internal static class PostgresOutboxRowReader
{
	internal static Excalibur.Outbox.OutboxMessage Read(DbDataReader reader) => new()
	{
		MessageId = StaticDbValue.String(reader, 0)!,
		MessageType = StaticDbValue.String(reader, 1)!,
		MessageMetadata = StaticDbValue.String(reader, 2)!,
		MessageBody = StaticDbValue.Bytes(reader, 3)!,
		TenantId = StaticDbValue.String(reader, 4),
		Destination = StaticDbValue.String(reader, 5),
		CorrelationId = StaticDbValue.String(reader, 6),
		CausationId = StaticDbValue.String(reader, 7),
		Priority = StaticDbValue.Int32(reader, 8),
		ScheduledAt = StaticDbValue.DateTimeOffset(reader, 9),
		PartitionKey = StaticDbValue.String(reader, 10),
		GroupKey = StaticDbValue.String(reader, 11),
		SequenceNumber = StaticDbValue.Int64(reader, 12),
		TargetTransports = StaticDbValue.String(reader, 13),
		IsMultiTransport = StaticDbValue.Boolean(reader, 14),
		CreatedAt = StaticDbValue.DateTimeOffset(reader, 15) ?? default,
		Attempts = StaticDbValue.Int32(reader, 16),
		LastError = StaticDbValue.String(reader, 17),
		DispatcherId = StaticDbValue.String(reader, 18),
		DispatcherTimeout = StaticDbValue.DateTimeOffset(reader, 19),
	};
}
