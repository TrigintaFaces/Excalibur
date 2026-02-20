// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BatchMigrationResultShould
{
    [Fact]
    public void CalculateSuccessRateForNonZeroTotal()
    {
        var result = new EncryptionBatchMigrationResult
        {
            Success = false,
            MigrationId = "m-1",
            TotalItems = 100,
            SucceededCount = 75,
            FailedCount = 25,
            Duration = TimeSpan.FromMinutes(5),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.SuccessRate.ShouldBe(75.0);
    }

    [Fact]
    public void ReturnZeroSuccessRateForZeroTotal()
    {
        var result = new EncryptionBatchMigrationResult
        {
            Success = true,
            MigrationId = "m-1",
            TotalItems = 0,
            SucceededCount = 0,
            FailedCount = 0,
            Duration = TimeSpan.Zero,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.SuccessRate.ShouldBe(0.0);
    }

    [Fact]
    public void ReportPartialSuccessWhenNotSuccessButSomeSucceeded()
    {
        var result = new EncryptionBatchMigrationResult
        {
            Success = false,
            MigrationId = "m-1",
            TotalItems = 10,
            SucceededCount = 7,
            FailedCount = 3,
            Duration = TimeSpan.FromSeconds(30),
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.IsPartialSuccess.ShouldBeTrue();
    }

    [Fact]
    public void NotReportPartialSuccessWhenSuccessIsTrue()
    {
        var result = new EncryptionBatchMigrationResult
        {
            Success = true,
            MigrationId = "m-1",
            TotalItems = 10,
            SucceededCount = 10,
            FailedCount = 0,
            Duration = TimeSpan.FromSeconds(10),
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.IsPartialSuccess.ShouldBeFalse();
    }

    [Fact]
    public void NotReportPartialSuccessWhenNoneSucceeded()
    {
        var result = new EncryptionBatchMigrationResult
        {
            Success = false,
            MigrationId = "m-1",
            TotalItems = 10,
            SucceededCount = 0,
            FailedCount = 10,
            Duration = TimeSpan.FromSeconds(10),
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.IsPartialSuccess.ShouldBeFalse();
    }
}
