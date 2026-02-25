// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new BatchMigrationOptions();

        options.MaxDegreeOfParallelism.ShouldBe(4);
        options.BatchSize.ShouldBe(100);
        options.ContinueOnError.ShouldBeTrue();
        options.ItemTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        options.TotalTimeout.ShouldBeNull();
        options.TrackProgress.ShouldBeTrue();
        options.MigrationId.ShouldBeNull();
        options.Progress.ShouldBeNull();
    }

    [Fact]
    public void ReturnDefaultInstance()
    {
        var defaults = BatchMigrationOptions.Default;
        defaults.ShouldNotBeNull();
        defaults.MaxDegreeOfParallelism.ShouldBe(4);
    }

    [Fact]
    public void CalculatePercentCompleteForProgress()
    {
        var progress = new EncryptionMigrationProgress
        {
            TotalItems = 200,
            CompletedItems = 100,
            SucceededItems = 95,
            FailedItems = 5
        };

        progress.PercentComplete.ShouldBe(50.0);
    }

    [Fact]
    public void ReturnZeroPercentCompleteForZeroTotal()
    {
        var progress = new EncryptionMigrationProgress
        {
            TotalItems = 0,
            CompletedItems = 0,
            SucceededItems = 0,
            FailedItems = 0
        };

        progress.PercentComplete.ShouldBe(0.0);
    }
}
