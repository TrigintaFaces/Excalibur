// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlValidationShould
{
    [Fact]
    public void CreateControlValidationResult()
    {
        var result = new ControlValidationResult
        {
            ControlId = "CC6.6-ENC",
            IsConfigured = true,
            IsEffective = true,
            EffectivenessScore = 95
        };

        result.ControlId.ShouldBe("CC6.6-ENC");
        result.IsConfigured.ShouldBeTrue();
        result.IsEffective.ShouldBeTrue();
        result.EffectivenessScore.ShouldBe(95);
        result.ConfigurationIssues.ShouldBeEmpty();
        result.Evidence.ShouldBeEmpty();
    }

    [Fact]
    public void CreateControlTestParameters()
    {
        var now = DateTimeOffset.UtcNow;
        var parameters = new ControlTestParameters
        {
            SampleSize = 50,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            IncludeDetailedEvidence = false
        };

        parameters.SampleSize.ShouldBe(50);
        parameters.IncludeDetailedEvidence.ShouldBeFalse();
    }

    [Fact]
    public void HaveDefaultSampleSizeOf25()
    {
        var parameters = new ControlTestParameters();
        parameters.SampleSize.ShouldBe(25);
        parameters.IncludeDetailedEvidence.ShouldBeTrue();
    }

    [Fact]
    public void CreateControlTestResult()
    {
        var parameters = new ControlTestParameters
        {
            SampleSize = 25,
            PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
            PeriodEnd = DateTimeOffset.UtcNow
        };

        var result = new ControlTestResult
        {
            ControlId = "CC6.6-ENC",
            Parameters = parameters,
            ItemsTested = 25,
            ExceptionsFound = 1,
            Outcome = TestOutcome.MinorExceptions,
            Exceptions = new[]
            {
                new TestException
                {
                    ItemId = "item-5",
                    Description = "Encryption not applied",
                    Severity = GapSeverity.Medium,
                    OccurredAt = DateTimeOffset.UtcNow.AddDays(-30)
                }
            }
        };

        result.ExceptionsFound.ShouldBe(1);
        result.Outcome.ShouldBe(TestOutcome.MinorExceptions);
        result.Exceptions.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(TestOutcome.NoExceptions)]
    [InlineData(TestOutcome.MinorExceptions)]
    [InlineData(TestOutcome.SignificantExceptions)]
    [InlineData(TestOutcome.ControlFailure)]
    public void SupportAllTestOutcomes(TestOutcome outcome)
    {
        var result = new ControlTestResult
        {
            ControlId = "test",
            Parameters = new ControlTestParameters(),
            ItemsTested = 10,
            ExceptionsFound = 0,
            Outcome = outcome
        };

        result.Outcome.ShouldBe(outcome);
    }
}
