// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class ErasureOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new ErasureOptions();

        options.DefaultGracePeriod.ShouldBe(TimeSpan.FromHours(72));
        options.MinimumGracePeriod.ShouldBe(TimeSpan.FromHours(1));
        options.MaximumGracePeriod.ShouldBe(TimeSpan.FromDays(30));
        options.EnableAutoDiscovery.ShouldBeTrue();
        options.RequireVerification.ShouldBeTrue();
        options.VerificationMethods.ShouldBe(VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem);
        options.NotifyOnCompletion.ShouldBeTrue();
        options.Retention.CertificateRetentionPeriod.ShouldBe(TimeSpan.FromDays(365 * 7));
        options.AllowImmediateErasure.ShouldBeFalse();
        options.Retention.SigningKeyId.ShouldBeNull();
        options.Execution.BatchSize.ShouldBe(100);
        options.Execution.MaxRetryAttempts.ShouldBe(3);
        options.Execution.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
