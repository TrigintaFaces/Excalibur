// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationEstimateShould
{
    [Fact]
    public void CreateWithRequiredProperties()
    {
        var estimate = new MigrationEstimate
        {
            EstimatedItemCount = 1000,
            EstimatedDataSizeBytes = 1024 * 1024,
            EstimatedDuration = TimeSpan.FromMinutes(10)
        };

        estimate.EstimatedItemCount.ShouldBe(1000);
        estimate.EstimatedDataSizeBytes.ShouldBe(1024 * 1024);
        estimate.EstimatedDuration.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void SupportOptionalBreakdowns()
    {
        var estimate = new MigrationEstimate
        {
            EstimatedItemCount = 100,
            EstimatedDataSizeBytes = 5000,
            EstimatedDuration = TimeSpan.FromSeconds(30),
            ByAlgorithm = new Dictionary<EncryptionAlgorithm, int>
            {
                [EncryptionAlgorithm.Aes256Gcm] = 80,
                [EncryptionAlgorithm.Aes256CbcHmac] = 20
            },
            ByKeyId = new Dictionary<string, int> { ["key-1"] = 60, ["key-2"] = 40 },
            ByTenant = new Dictionary<string, int> { ["t1"] = 100 },
            Warnings = new[] { "Some data is very old" }
        };

        estimate.ByAlgorithm.ShouldNotBeNull();
        estimate.ByAlgorithm.Count.ShouldBe(2);
        estimate.Warnings.ShouldNotBeNull();
    }

    [Fact]
    public void SetEstimatedAtAutomatically()
    {
        var before = DateTimeOffset.UtcNow;
        var estimate = new MigrationEstimate
        {
            EstimatedItemCount = 1,
            EstimatedDataSizeBytes = 1,
            EstimatedDuration = TimeSpan.FromSeconds(1)
        };
        var after = DateTimeOffset.UtcNow;

        estimate.EstimatedAt.ShouldBeGreaterThanOrEqualTo(before);
        estimate.EstimatedAt.ShouldBeLessThanOrEqualTo(after);
    }
}
