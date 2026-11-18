// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;

using Excalibur.Data;

using Dapper;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>Closed mappings for the fixed Oracle reserved-message projection.</summary>
internal static class OracleOutboxRowReader
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
		ScheduledAt = DateTimeOffset(reader, 9),
		PartitionKey = StaticDbValue.String(reader, 10),
		GroupKey = StaticDbValue.String(reader, 11),
		SequenceNumber = StaticDbValue.Int64(reader, 12),
		TargetTransports = StaticDbValue.String(reader, 13),
		IsMultiTransport = StaticDbValue.Boolean(reader, 14),
		CreatedAt = DateTimeOffset(reader, 15) ?? default,
		Attempts = StaticDbValue.Int32(reader, 16),
		LastError = StaticDbValue.String(reader, 17),
		DispatcherId = StaticDbValue.String(reader, 18),
		DispatcherTimeout = DateTimeOffset(reader, 19),
	};

	internal static DateTimeOffset? DateTimeOffset(DbDataReader reader, int ordinal) =>
		reader.IsDBNull(ordinal) ? null : reader is OracleDataReader oracleReader
			? oracleReader.GetDateTimeOffset(ordinal)
			: reader.GetFieldValue<DateTimeOffset>(ordinal);

	internal static void Initialize(DbCommand command)
	{
		if (command is OracleCommand oracleCommand)
		{
			// These are the effective settings applied by Dapper 2.1.89 CommandDefinition.GetInit.
			oracleCommand.BindByName = true;
			oracleCommand.InitialLONGFetchSize = -1;
			if (SqlMapper.Settings.FetchSize >= 0)
			{
				oracleCommand.FetchSize = SqlMapper.Settings.FetchSize;
			}
		}
	}
}
