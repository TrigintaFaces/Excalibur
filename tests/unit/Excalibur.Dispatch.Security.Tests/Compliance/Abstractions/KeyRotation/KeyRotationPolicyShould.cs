// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.KeyRotation;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationPolicyShould
{
    private static KeyMetadata CreateKey(
        KeyStatus status = KeyStatus.Active,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? lastRotatedAt = null) =>
        new()
        {
            KeyId = "key-1",
            Version = 1,
            Algorithm = EncryptionAlgorithm.Aes256Gcm,
            Status = status,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-100),
            LastRotatedAt = lastRotatedAt,
            ExpiresAt = null
        };

    [Fact]
    public void HaveDefaultPolicyWith90DayRotation()
    {
        var policy = KeyRotationPolicy.Default;

        policy.Name.ShouldBe("Default");
        policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(90));
        policy.AutoRotateEnabled.ShouldBeTrue();
    }

    [Fact]
    public void HaveHighSecurityPolicyWith30DayRotation()
    {
        var policy = KeyRotationPolicy.HighSecurity;

        policy.Name.ShouldBe("HighSecurity");
        policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(30));
        policy.RequireFipsCompliance.ShouldBeTrue();
        policy.WarningDaysBeforeRotation.ShouldBe(7);
        policy.RetainedVersionCount.ShouldBe(5);
    }

    [Fact]
    public void HaveArchivalPolicyWithAnnualRotation()
    {
        var policy = KeyRotationPolicy.Archival;

        policy.Name.ShouldBe("Archival");
        policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(365));
        policy.RetainedVersionCount.ShouldBe(10);
    }

    [Fact]
    public void DetectRotationDueForOldKey()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            AutoRotateEnabled = true
        };
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-100));

        policy.IsRotationDue(key).ShouldBeTrue();
    }

    [Fact]
    public void NotDetectRotationDueForRecentKey()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            AutoRotateEnabled = true
        };
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-10));

        policy.IsRotationDue(key).ShouldBeFalse();
    }

    [Fact]
    public void NotDetectRotationDueWhenAutoRotateDisabled()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            AutoRotateEnabled = false
        };
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-100));

        policy.IsRotationDue(key).ShouldBeFalse();
    }

    [Fact]
    public void NotDetectRotationDueForNonActiveKey()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            AutoRotateEnabled = true
        };
        var key = CreateKey(status: KeyStatus.DecryptOnly, createdAt: DateTimeOffset.UtcNow.AddDays(-100));

        policy.IsRotationDue(key).ShouldBeFalse();
    }

    [Fact]
    public void UseLastRotatedAtWhenAvailable()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            AutoRotateEnabled = true
        };
        // Created long ago, but rotated recently
        var key = CreateKey(
            createdAt: DateTimeOffset.UtcNow.AddDays(-365),
            lastRotatedAt: DateTimeOffset.UtcNow.AddDays(-10));

        policy.IsRotationDue(key).ShouldBeFalse();
    }

    [Fact]
    public void CalculateNextRotationTime()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90)
        };
        var createdAt = DateTimeOffset.UtcNow.AddDays(-30);
        var key = CreateKey(createdAt: createdAt);

        var nextRotation = policy.GetNextRotationTime(key);

        nextRotation.ShouldBe(createdAt.AddDays(90));
    }

    [Fact]
    public void WarnWhenRotationApproaching()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            NotifyBeforeRotation = true,
            WarningDaysBeforeRotation = 14
        };
        // Key created 80 days ago - 10 days until rotation, within 14-day warning window
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-80));

        policy.ShouldWarn(key).ShouldBeTrue();
    }

    [Fact]
    public void NotWarnWhenRotationFarAway()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            NotifyBeforeRotation = true,
            WarningDaysBeforeRotation = 14
        };
        // Key created 10 days ago - 80 days until rotation
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-10));

        policy.ShouldWarn(key).ShouldBeFalse();
    }

    [Fact]
    public void NotWarnWhenNotifyDisabled()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            NotifyBeforeRotation = false,
            WarningDaysBeforeRotation = 14
        };
        var key = CreateKey(createdAt: DateTimeOffset.UtcNow.AddDays(-80));

        policy.ShouldWarn(key).ShouldBeFalse();
    }

    [Fact]
    public void NotWarnForNonActiveKey()
    {
        var policy = new KeyRotationPolicy
        {
            Name = "Test",
            MaxKeyAge = TimeSpan.FromDays(90),
            NotifyBeforeRotation = true,
            WarningDaysBeforeRotation = 14
        };
        var key = CreateKey(status: KeyStatus.Suspended, createdAt: DateTimeOffset.UtcNow.AddDays(-80));

        policy.ShouldWarn(key).ShouldBeFalse();
    }
}
