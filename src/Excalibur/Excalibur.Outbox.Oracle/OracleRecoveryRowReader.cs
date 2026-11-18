// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>Closed projection for the existing failed and dead-letter recovery queries.</summary>
internal static class OracleRecoveryRowReader
{
	internal static DeadLetterRecord Read(DbDataReader reader, bool includePriority) => new()
	{
		MessageId = StaticDbValue.String(reader, 0) ?? string.Empty,
		TenantId = StaticDbValue.String(reader, 1) ?? string.Empty,
		MessageType = StaticDbValue.String(reader, 2) ?? string.Empty,
		MessageMetadata = StaticDbValue.String(reader, 3) ?? string.Empty,
		MessageBody = StaticDbValue.Bytes(reader, 4) ?? [],
		OccurredOn = OracleOutboxRowReader.DateTimeOffset(reader, 5) ?? default,
		Attempts = StaticDbValue.Int32(reader, 6),
		ErrorMessage = StaticDbValue.String(reader, 7),
		Priority = includePriority ? StaticDbValue.Int32(reader, 8) : 0,
	};
}
