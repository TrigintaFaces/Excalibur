// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Shouldly;

namespace Excalibur.Tests.Cdc;

public class CdcReasonCodesShould
{
    #region Constant Values

    [Fact]
    public void HaveCorrectPositionPurgedValue()
    {
        CdcReasonCodes.PositionPurged.ShouldBe("POSITION_PURGED");
    }

    [Fact]
    public void HaveCorrectBackupRestoreValue()
    {
        CdcReasonCodes.BackupRestore.ShouldBe("BACKUP_RESTORE");
    }

    [Fact]
    public void HaveCorrectCdcReenabledValue()
    {
        CdcReasonCodes.CdcReenabled.ShouldBe("CDC_REENABLED");
    }

    [Fact]
    public void HaveCorrectPositionOutOfRangeValue()
    {
        CdcReasonCodes.PositionOutOfRange.ShouldBe("POSITION_OUT_OF_RANGE");
    }

    [Fact]
    public void HaveCorrectTokenExpiredValue()
    {
        CdcReasonCodes.TokenExpired.ShouldBe("TOKEN_EXPIRED");
    }

    [Fact]
    public void HaveCorrectSourceDroppedValue()
    {
        CdcReasonCodes.SourceDropped.ShouldBe("SOURCE_DROPPED");
    }

    [Fact]
    public void HaveCorrectSourceRenamedValue()
    {
        CdcReasonCodes.SourceRenamed.ShouldBe("SOURCE_RENAMED");
    }

    [Fact]
    public void HaveCorrectPartitionChangedValue()
    {
        CdcReasonCodes.PartitionChanged.ShouldBe("PARTITION_CHANGED");
    }

    [Fact]
    public void HaveCorrectStreamInvalidatedValue()
    {
        CdcReasonCodes.StreamInvalidated.ShouldBe("STREAM_INVALIDATED");
    }

    [Fact]
    public void HaveCorrectUnknownValue()
    {
        CdcReasonCodes.Unknown.ShouldBe("UNKNOWN");
    }

    #endregion

    #region IsRecoverable - Null and Empty

    [Fact]
    public void ReturnFalseForNullReasonCode()
    {
        CdcReasonCodes.IsRecoverable(null).ShouldBeFalse();
    }

    [Fact]
    public void ReturnFalseForEmptyReasonCode()
    {
        CdcReasonCodes.IsRecoverable(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void ReturnFalseForWhitespaceReasonCode()
    {
        // Whitespace is not null or empty, but it doesn't match any known codes
        // and doesn't contain underscore, so falls through to default case
        CdcReasonCodes.IsRecoverable("   ").ShouldBeTrue();
    }

    #endregion

    #region IsRecoverable - Standard Recoverable Codes

    [Theory]
    [InlineData("POSITION_PURGED")]
    [InlineData("BACKUP_RESTORE")]
    [InlineData("CDC_REENABLED")]
    [InlineData("POSITION_OUT_OF_RANGE")]
    [InlineData("TOKEN_EXPIRED")]
    [InlineData("PARTITION_CHANGED")]
    [InlineData("STREAM_INVALIDATED")]
    public void ReturnTrueForRecoverableCodes(string reasonCode)
    {
        CdcReasonCodes.IsRecoverable(reasonCode).ShouldBeTrue();
    }

    #endregion

    #region IsRecoverable - Non-Recoverable Codes

    [Fact]
    public void ReturnFalseForUnknownCode()
    {
        // UNKNOWN is the only code without underscore that returns false
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.Unknown).ShouldBeFalse();
    }

    #endregion

    #region IsRecoverable - Non-Recoverable Standard Codes (Require Intervention)

    [Theory]
    [InlineData("SOURCE_DROPPED")]
    [InlineData("SOURCE_RENAMED")]
    public void ReturnFalseForNonRecoverableStandardCodes(string reasonCode)
    {
        // These codes require manual intervention:
        // SOURCE_DROPPED - The source is gone and requires intervention
        // SOURCE_RENAMED - Requires configuration update
        CdcReasonCodes.IsRecoverable(reasonCode).ShouldBeFalse();
    }

    #endregion

    #region IsRecoverable - Provider-Prefixed Codes

    [Theory]
    [InlineData("MONGODB_OPLOG_ROLLOVER")]
    [InlineData("COSMOSDB_TOKEN_EXPIRED")]
    [InlineData("DYNAMODB_SHARD_SPLIT")]
    [InlineData("Postgres_WAL_RECYCLED")]
    [InlineData("SQLSERVER_CDC_CLEANUP")]
    [InlineData("FIRESTORE_LISTEN_REMOVED")]
    public void ReturnTrueForProviderPrefixedCodes(string reasonCode)
    {
        CdcReasonCodes.IsRecoverable(reasonCode).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForUnknownPrefixedCodes()
    {
        // Codes starting with UNKNOWN_ skip the underscore check
        // but fall through to the default case which returns true
        CdcReasonCodes.IsRecoverable("UNKNOWN_ERROR").ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForUnknownPrefixedCodesCaseInsensitive()
    {
        // The StartsWith check is case-insensitive, so this skips
        // the underscore check and falls through to default (true)
        CdcReasonCodes.IsRecoverable("unknown_something").ShouldBeTrue();
    }

    #endregion

    #region IsRecoverable - Default Case

    [Theory]
    [InlineData("CUSTOM_RECOVERABLE_CODE")]
    [InlineData("PROVIDER_SPECIFIC_ERROR")]
    [InlineData("CUSTOM_CODE_123")]
    public void ReturnTrueForUnknownCodesWithUnderscore(string reasonCode)
    {
        // Unknown codes with underscore are generally recoverable (provider-specific)
        CdcReasonCodes.IsRecoverable(reasonCode).ShouldBeTrue();
    }

    [Theory]
    [InlineData("SOMECUSTOMCODE")]
    [InlineData("customcode")]
    [InlineData("X")]
    public void ReturnTrueForUnknownCodesWithoutUnderscore(string reasonCode)
    {
        // Unknown codes without underscore fall through to default case which returns true
        CdcReasonCodes.IsRecoverable(reasonCode).ShouldBeTrue();
    }

    #endregion

    #region IsRecoverable - Edge Cases with Constants

    [Fact]
    public void ReturnTrueForPositionPurgedConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.PositionPurged).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForBackupRestoreConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.BackupRestore).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForCdcReenabledConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.CdcReenabled).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForPositionOutOfRangeConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.PositionOutOfRange).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForTokenExpiredConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.TokenExpired).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForPartitionChangedConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.PartitionChanged).ShouldBeTrue();
    }

    [Fact]
    public void ReturnTrueForStreamInvalidatedConstant()
    {
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.StreamInvalidated).ShouldBeTrue();
    }

    [Fact]
    public void ReturnFalseForSourceDroppedConstant()
    {
        // SOURCE_DROPPED is not recoverable - the source no longer exists
        // and requires manual intervention to resolve
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.SourceDropped).ShouldBeFalse();
    }

    [Fact]
    public void ReturnFalseForSourceRenamedConstant()
    {
        // SOURCE_RENAMED is not recoverable - the source was renamed
        // and requires configuration update to use the new name
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.SourceRenamed).ShouldBeFalse();
    }

    [Fact]
    public void ReturnFalseForUnknownConstantOnly()
    {
        // UNKNOWN has no underscore, so it reaches the switch and returns false
        CdcReasonCodes.IsRecoverable(CdcReasonCodes.Unknown).ShouldBeFalse();
    }

    #endregion
}
