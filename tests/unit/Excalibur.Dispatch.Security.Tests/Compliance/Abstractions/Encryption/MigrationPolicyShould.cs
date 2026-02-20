// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationPolicyShould
{
    [Fact]
    public void HaveDefaultPolicyWith90DayMaxKeyAge()
    {
        var policy = MigrationPolicy.Default;

        policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(90));
    }

    [Fact]
    public void CreatePolicyForAlgorithm()
    {
        var policy = MigrationPolicy.ForAlgorithm(EncryptionAlgorithm.Aes256Gcm);

        policy.TargetAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
    }

    [Fact]
    public void CreatePolicyForDeprecatedKeys()
    {
        var policy = MigrationPolicy.ForDeprecatedKeys("old-key-1", "old-key-2");

        policy.DeprecatedKeyIds.ShouldNotBeNull();
        policy.DeprecatedKeyIds.ShouldContain("old-key-1");
        policy.DeprecatedKeyIds.ShouldContain("old-key-2");
    }

    [Fact]
    public void SupportAllProperties()
    {
        var policy = new MigrationPolicy
        {
            MaxKeyAge = TimeSpan.FromDays(30),
            MinKeyVersion = 5,
            TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm,
            DeprecatedAlgorithms = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac },
            DeprecatedKeyIds = new HashSet<string> { "k1" },
            EncryptedBefore = DateTimeOffset.UtcNow,
            RequireFipsCompliance = true,
            TenantIds = new HashSet<string> { "t1" }
        };

        policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(30));
        policy.MinKeyVersion.ShouldBe(5);
        policy.RequireFipsCompliance.ShouldBeTrue();
        policy.TenantIds.ShouldContain("t1");
    }
}
