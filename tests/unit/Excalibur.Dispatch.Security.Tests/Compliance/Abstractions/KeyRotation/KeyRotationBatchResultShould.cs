// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.KeyRotation;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationBatchResultShould
{
    [Fact]
    public void ReportAllSucceededWhenNoFailures()
    {
        var result = new KeyRotationBatchResult
        {
            KeysChecked = 10,
            KeysDueForRotation = 3,
            KeysRotated = 3,
            KeysFailed = 0
        };

        result.AllSucceeded.ShouldBeTrue();
    }

    [Fact]
    public void ReportNotAllSucceededWhenSomeFailures()
    {
        var result = new KeyRotationBatchResult
        {
            KeysChecked = 10,
            KeysDueForRotation = 3,
            KeysRotated = 2,
            KeysFailed = 1
        };

        result.AllSucceeded.ShouldBeFalse();
    }

    [Fact]
    public void CalculateDuration()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        var end = DateTimeOffset.UtcNow;

        var result = new KeyRotationBatchResult
        {
            StartedAt = start,
            CompletedAt = end
        };

        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void CreateEmptyResult()
    {
        var result = KeyRotationBatchResult.Empty();

        result.KeysChecked.ShouldBe(0);
        result.KeysDueForRotation.ShouldBe(0);
        result.KeysRotated.ShouldBe(0);
        result.KeysFailed.ShouldBe(0);
        result.AllSucceeded.ShouldBeTrue();
    }

    [Fact]
    public void HaveDefaultEmptyCollections()
    {
        var result = new KeyRotationBatchResult();

        result.Results.ShouldBeEmpty();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void CreateKeyRotationError()
    {
        var error = new KeyRotationError
        {
            KeyId = "key-1",
            Message = "Rotation failed",
            Exception = new InvalidOperationException("test")
        };

        error.KeyId.ShouldBe("key-1");
        error.Message.ShouldBe("Rotation failed");
        error.Exception.ShouldNotBeNull();
    }
}
