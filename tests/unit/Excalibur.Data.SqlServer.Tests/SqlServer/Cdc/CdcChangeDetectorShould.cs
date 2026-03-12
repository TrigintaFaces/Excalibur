// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcChangeDetector"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcChangeDetectorShould : UnitTestBase
{
	[Fact]
	public void ByteArrayToHex_FormatsCorrectly()
	{
		var bytes = new byte[] { 0x12, 0xAB, 0xCD };

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0x12ABCD");
	}

	[Fact]
	public void ByteArrayToHex_HandlesEmptyArray()
	{
		var bytes = Array.Empty<byte>();

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0x");
	}

	[Fact]
	public void ByteArrayToHex_HandlesSingleByte()
	{
		var bytes = new byte[] { 0xFF };

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0xFF");
	}

	[Fact]
	public void ByteArrayToHex_HandlesAllZeros()
	{
		var bytes = new byte[] { 0x00, 0x00, 0x00 };

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0x000000");
	}

	[Fact]
	public void ChangeProcessingState_CanBeCreated_WithRequiredProperties()
	{
		var state = new CdcChangeDetector.ChangeProcessingState
		{
			TableName = "dbo_orders",
			Lsn = new byte[] { 0x01, 0x02 },
			PendingUpdateBefore = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
			PendingUpdateAfter = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
		};

		state.TableName.ShouldBe("dbo_orders");
		state.Lsn.ShouldBe(new byte[] { 0x01, 0x02 });
		state.SequenceValue.ShouldBeNull();
		state.LastOperation.ShouldBe(default);
		state.TotalRowsReadInThisLsn.ShouldBe(0);
		state.PendingUpdateBefore.ShouldBeEmpty();
		state.PendingUpdateAfter.ShouldBeEmpty();
	}

	[Fact]
	public void ChangeProcessingState_MutableProperties_CanBeUpdated()
	{
		var state = new CdcChangeDetector.ChangeProcessingState
		{
			TableName = "dbo_orders",
			Lsn = new byte[] { 0x01 },
			PendingUpdateBefore = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
			PendingUpdateAfter = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
		};

		state.Lsn = new byte[] { 0x02 };
		state.SequenceValue = new byte[] { 0x03 };
		state.LastOperation = CdcOperationCodes.Insert;
		state.TotalRowsReadInThisLsn = 42;

		state.Lsn.ShouldBe(new byte[] { 0x02 });
		state.SequenceValue.ShouldBe(new byte[] { 0x03 });
		state.LastOperation.ShouldBe(CdcOperationCodes.Insert);
		state.TotalRowsReadInThisLsn.ShouldBe(42);
	}

	[Fact]
	public void ChangeProcessingState_PendingUpdates_TrackBySequenceValue()
	{
		var state = new CdcChangeDetector.ChangeProcessingState
		{
			TableName = "dbo_orders",
			Lsn = new byte[] { 0x01 },
			PendingUpdateBefore = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
			PendingUpdateAfter = new Dictionary<byte[], CdcRow>(new ByteArrayEqualityComparer()),
		};

		var seqVal = new byte[] { 0xAA };
		var row = new CdcRow
		{
			TableName = "dbo_orders",
			Lsn = new byte[] { 0x01 },
			SeqVal = seqVal,
			OperationCode = CdcOperationCodes.UpdateBefore,
			CommitTime = DateTime.UtcNow,
			Changes = new Dictionary<string, object>(StringComparer.Ordinal),
			DataTypes = new Dictionary<string, Type>(StringComparer.Ordinal),
		};

		state.PendingUpdateBefore[seqVal] = row;

		state.PendingUpdateBefore.ContainsKey(seqVal).ShouldBeTrue();
		state.PendingUpdateBefore[seqVal].ShouldBe(row);
	}
}
