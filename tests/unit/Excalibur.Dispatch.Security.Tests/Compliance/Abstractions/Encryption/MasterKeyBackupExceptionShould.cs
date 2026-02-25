// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MasterKeyBackupExceptionShould
{
    [Fact]
    public void ConstructWithParameterlessConstructor()
    {
        var ex = new MasterKeyBackupException();
        ex.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void ConstructWithMessage()
    {
        var ex = new MasterKeyBackupException("backup failed");
        ex.Message.ShouldBe("backup failed");
    }

    [Fact]
    public void ConstructWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new MasterKeyBackupException("outer", inner);

        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void SupportInitProperties()
    {
        var ex = new MasterKeyBackupException("fail")
        {
            KeyId = "key-1",
            BackupId = "backup-1",
            ErrorCode = MasterKeyBackupErrorCode.IntegrityCheckFailed
        };

        ex.KeyId.ShouldBe("key-1");
        ex.BackupId.ShouldBe("backup-1");
        ex.ErrorCode.ShouldBe(MasterKeyBackupErrorCode.IntegrityCheckFailed);
    }

    [Theory]
    [InlineData(MasterKeyBackupErrorCode.Unknown, 0)]
    [InlineData(MasterKeyBackupErrorCode.KeyNotFound, 1)]
    [InlineData(MasterKeyBackupErrorCode.CryptographicError, 12)]
    public void HaveExpectedEnumValues(MasterKeyBackupErrorCode code, int expectedValue)
    {
        ((int)code).ShouldBe(expectedValue);
    }
}
