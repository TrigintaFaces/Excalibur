// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class StalePositionReasonCodesShould
{
	[Theory]
	[InlineData(22037, "LSN_OUT_OF_RANGE")]
	[InlineData(22029, "LSN_OUT_OF_RANGE")]
	[InlineData(22911, "CDC_REENABLED")]
	[InlineData(22985, "CAPTURE_INSTANCE_DROPPED")]
	public void MapKnownErrorNumbersToReasonCodes(int errorNumber, string expectedCode)
	{
		StalePositionReasonCodes.FromSqlError(errorNumber).ShouldBe(expectedCode);
	}

	[Fact]
	public void ReturnUnknownForUnrecognizedErrorNumber()
	{
		StalePositionReasonCodes.FromSqlError(99999).ShouldBe(StalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void ExposeCdcCleanupConstant()
	{
		StalePositionReasonCodes.CdcCleanup.ShouldBe("CDC_CLEANUP");
	}

	[Fact]
	public void ExposeBackupRestoreConstant()
	{
		StalePositionReasonCodes.BackupRestore.ShouldBe("BACKUP_RESTORE");
	}

	[Fact]
	public void ExposeLsnOutOfRangeConstant()
	{
		StalePositionReasonCodes.LsnOutOfRange.ShouldBe("LSN_OUT_OF_RANGE");
	}

	[Fact]
	public void ExposeCaptureInstanceDroppedConstant()
	{
		StalePositionReasonCodes.CaptureInstanceDropped.ShouldBe("CAPTURE_INSTANCE_DROPPED");
	}

	[Fact]
	public void ExposeCdcReenabledConstant()
	{
		StalePositionReasonCodes.CdcReenabled.ShouldBe("CDC_REENABLED");
	}
}
