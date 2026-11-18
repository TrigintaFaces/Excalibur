// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>Closed mappings for the fixed SQL Server outbox projections.</summary>
internal static class SqlServerOutboxRowReader
{
	internal static OutboxMessageRow Read(DbDataReader reader, bool includeDispatcher) => new()
	{
		Id = StaticDbValue.String(reader, 0) ?? string.Empty,
		MessageType = StaticDbValue.String(reader, 1) ?? string.Empty,
		Payload = StaticDbValue.Bytes(reader, 2) ?? [],
		Headers = StaticDbValue.String(reader, 3),
		Destination = StaticDbValue.String(reader, 4) ?? string.Empty,
		CreatedAt = StaticDbValue.DateTimeOffset(reader, 5) ?? default,
		ScheduledAt = StaticDbValue.DateTimeOffset(reader, 6),
		SentAt = StaticDbValue.DateTimeOffset(reader, 7),
		Status = StaticDbValue.Int32(reader, 8),
		RetryCount = StaticDbValue.Int32(reader, 9),
		LastError = StaticDbValue.String(reader, 10),
		LastAttemptAt = StaticDbValue.DateTimeOffset(reader, 11),
		CorrelationId = StaticDbValue.String(reader, 12),
		CausationId = StaticDbValue.String(reader, 13),
		TenantId = StaticDbValue.String(reader, 14),
		Priority = StaticDbValue.Int32(reader, 15),
		TargetTransports = StaticDbValue.String(reader, 16),
		IsMultiTransport = StaticDbValue.Boolean(reader, 17),
		PartitionKey = StaticDbValue.String(reader, 18),
		GroupKey = StaticDbValue.String(reader, 19),
		SequenceNumber = StaticDbValue.Int64(reader, 20),
		DispatcherId = includeDispatcher ? StaticDbValue.String(reader, 21) : null,
	};
}
