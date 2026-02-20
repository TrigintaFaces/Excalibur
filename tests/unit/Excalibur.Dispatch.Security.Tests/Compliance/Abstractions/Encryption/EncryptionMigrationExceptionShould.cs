// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionMigrationExceptionShould
{
    [Fact]
    public void ConstructWithParameterlessConstructor()
    {
        var ex = new EncryptionMigrationException();
        ex.ShouldBeAssignableTo<EncryptionException>();
    }

    [Fact]
    public void ConstructWithMessage()
    {
        var ex = new EncryptionMigrationException("migration failed");
        ex.Message.ShouldBe("migration failed");
    }

    [Fact]
    public void ConstructWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new EncryptionMigrationException("outer", inner);

        ex.Message.ShouldBe("outer");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void SupportInitProperties()
    {
        var ex = new EncryptionMigrationException("fail")
        {
            MigrationId = "m-1",
            ItemId = "item-5",
            SucceededCount = 10,
            FailedCount = 2
        };

        ex.MigrationId.ShouldBe("m-1");
        ex.ItemId.ShouldBe("item-5");
        ex.SucceededCount.ShouldBe(10);
        ex.FailedCount.ShouldBe(2);
    }
}
