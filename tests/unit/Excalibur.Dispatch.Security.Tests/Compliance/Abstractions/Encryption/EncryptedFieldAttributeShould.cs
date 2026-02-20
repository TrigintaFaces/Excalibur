// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptedFieldAttributeShould
{
    [Fact]
    public void HaveDefaultNullPurpose()
    {
        var attr = new EncryptedFieldAttribute();

        attr.Purpose.ShouldBeNull();
        attr.RequireFipsCompliance.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingPurpose()
    {
        var attr = new EncryptedFieldAttribute { Purpose = "pii" };
        attr.Purpose.ShouldBe("pii");
    }

    [Fact]
    public void AllowSettingFipsCompliance()
    {
        var attr = new EncryptedFieldAttribute { RequireFipsCompliance = true };
        attr.RequireFipsCompliance.ShouldBeTrue();
    }

    [Fact]
    public void BeApplicableOnlyToProperties()
    {
        var usage = typeof(EncryptedFieldAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        usage.ValidOn.ShouldBe(AttributeTargets.Property);
        usage.AllowMultiple.ShouldBeFalse();
        usage.Inherited.ShouldBeTrue();
    }
}
