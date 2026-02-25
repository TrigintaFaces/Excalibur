// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Cdc;

namespace Excalibur.Data.Tests.Postgres.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresStalePositionReasonCodesShould
{
	[Fact]
	public void HaveCorrectConstants()
	{
		PostgresStalePositionReasonCodes.WalPositionStale.ShouldBe("WAL_POSITION_STALE");
		PostgresStalePositionReasonCodes.ReplicationSlotInvalid.ShouldBe("REPLICATION_SLOT_INVALID");
		PostgresStalePositionReasonCodes.LogicalDecodingError.ShouldBe("LOGICAL_DECODING_ERROR");
		PostgresStalePositionReasonCodes.PublicationInvalid.ShouldBe("PUBLICATION_INVALID");
		PostgresStalePositionReasonCodes.ReplicationStreamDisconnected.ShouldBe("REPLICATION_STREAM_DISCONNECTED");
		PostgresStalePositionReasonCodes.Unknown.ShouldBe("UNKNOWN");
	}

	[Theory]
	[InlineData("58P01", "WAL_POSITION_STALE")]
	[InlineData("55000", "REPLICATION_SLOT_INVALID")]
	[InlineData("42704", "PUBLICATION_INVALID")]
	[InlineData("08006", "REPLICATION_STREAM_DISCONNECTED")]
	[InlineData("08P01", "LOGICAL_DECODING_ERROR")]
	[InlineData("0A000", "LOGICAL_DECODING_ERROR")]
	[InlineData("99999", "UNKNOWN")]
	[InlineData(null, "UNKNOWN")]
	public void MapSqlStateToReasonCode(string? sqlState, string expectedReason)
	{
		PostgresStalePositionReasonCodes.FromSqlState(sqlState).ShouldBe(expectedReason);
	}

	[Theory]
	[InlineData("WAL segment has been removed", "WAL_POSITION_STALE")]
	[InlineData("Requested WAL segment is not available", "WAL_POSITION_STALE")]
	[InlineData("WAL file not found", "WAL_POSITION_STALE")]
	[InlineData("WAL position is beyond reach", "WAL_POSITION_STALE")]
	[InlineData("Replication slot does not exist", "REPLICATION_SLOT_INVALID")]
	[InlineData("Replication slot is invalid", "REPLICATION_SLOT_INVALID")]
	[InlineData("Replication slot was dropped", "REPLICATION_SLOT_INVALID")]
	[InlineData("Publication does not exist", "PUBLICATION_INVALID")]
	[InlineData("Publication was dropped recently", "PUBLICATION_INVALID")]
	[InlineData("Logical decoding error in pgoutput", "LOGICAL_DECODING_ERROR")]
	[InlineData("pgoutput plugin failed", "LOGICAL_DECODING_ERROR")]
	[InlineData("Output plugin mismatch", "LOGICAL_DECODING_ERROR")]
	[InlineData("Connection to server lost", "REPLICATION_STREAM_DISCONNECTED")]
	[InlineData("Connection was closed unexpectedly", "REPLICATION_STREAM_DISCONNECTED")]
	[InlineData("Replication connection terminated", "REPLICATION_STREAM_DISCONNECTED")]
	[InlineData("Some random error", "UNKNOWN")]
	[InlineData(null, "UNKNOWN")]
	[InlineData("", "UNKNOWN")]
	[InlineData("   ", "UNKNOWN")]
	public void MapErrorMessageToReasonCode(string? errorMessage, string expectedReason)
	{
		PostgresStalePositionReasonCodes.FromErrorMessage(errorMessage).ShouldBe(expectedReason);
	}
}
